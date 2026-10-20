using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers
{
    public static class GameLauncher
    {
        private static readonly object RunningGamesLock = new();
        private static readonly Dictionary<string, Process> LaunchedGames =
            new(StringComparer.OrdinalIgnoreCase);

        public static string GetPayloadDirPath(string iconName)
        {
            return ServerPayloadUpdater.GetPayloadDirectory(iconName);
        }

        // 用硬链接（或复制）将 sourceDir 的文件部署到 targetDir
        // 返回 true 表示使用了硬链接，false 表示使用了文件复制
        public static Task<bool> HardLinkOrCopyDirectory(
            string sourceDir,
            string targetDir,
            bool preferHardLink,
            int maxRetries = 5) =>
            ServerPayloadDeployment.DeployDirectoryAsync(
                sourceDir, targetDir, preferHardLink, maxRetries);

        // 带结果回调的切服入口。渠道 Payload 包含 SDK、登录配置等
        // 可变文件，因此始终以独立文件部署；大资源复用由 Linked Runtime 负责。
        public static async Task SwitchServerWithResult(
            string rootPath,
            string iconName,
            Action<string> onProgress,
            bool isEndfield,
            Action<bool> onResult,
            bool operationAlreadyCoordinated = false)
        {
            IDisposable operationLease = null;
            try
            {
                LinkedClientPolicy.ThrowIfSharedClient(iconName, rootPath);
                var targetChannel = GameChannelCatalog.Get(iconName);
                if (!operationAlreadyCoordinated &&
                    targetChannel?.PayloadProfile != null &&
                    !LinkedClientOperationCoordinator.TryAcquire(
                        iconName, rootPath, out operationLease))
                {
                    throw new InvalidOperationException(
                        Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                }

                // Resolve the on-disk channel while holding the path lease. A
                // switch that completed immediately before this operation must
                // not leave us validating an obsolete source channel.
                var sharedRoot = SharedRootManager.ResolveAndPersist(
                    iconName, rootPath, detectBaseChannel: true);
                if (sharedRoot.Mode == SharedRootMode.Conflict)
                {
                    throw new InvalidOperationException(
                        "同一游戏目录配置了不兼容的渠道，不能执行传统切服。");
                }
                if (sharedRoot.Mode == SharedRootMode.Shared &&
                    sharedRoot.Base != null &&
                    !GameChannelCatalog.CanSwitchChannel(
                        sharedRoot.Base.IconName, iconName))
                {
                    throw new NotSupportedException(
                        $"不支持从 {sharedRoot.Base.Channel} 切换到 " +
                        $"{targetChannel?.Channel ?? iconName}。");
                }

                var profile = ServerPayloadUpdater.GetProfile(iconName) ??
                              throw new NotSupportedException(
                                  $"No legacy server payload is defined for {iconName}.");
                bool usedHardLink = await ServerPayloadUpdater.UsePayloadDirectoryAsync(
                    iconName,
                    async payloadDir =>
                    {
                        if (payloadDir == null || !Directory.Exists(payloadDir))
                        {
                            throw new FileNotFoundException(
                                Localizer.GetRequiredString("App.Switch.NoPayload"));
                        }

                        onProgress(Localizer.GetRequiredString("App.Switch.Copying"));

                        return await ServerPayloadDeployment.DeployProfileAsync(
                            profile, payloadDir, rootPath,
                            preferHardLink: false);
                    });
                onResult(usedHardLink);
                if (sharedRoot.Mode == SharedRootMode.Shared)
                {
                    SharedRootManager.RecordBaseChannel(
                        iconName, rootPath, "traditional-server-switch");
                }

                string doneMsg = Localizer.GetRequiredString("App.Switch.DoneCopy");
                onProgress(doneMsg);
            }
            catch (Exception ex) when (IsFileInUseException(ex))
            {
                throw new InvalidOperationException(
                    GetCloseGameClientMessage(), ex);
            }
            finally
            {
                operationLease?.Dispose();
            }
        }

        public static async Task<Process> StartArknightsAsync(string rootPath, string iconName)
        {
            // ── 联动启动 ──
            var cfg = ConfigHelper.Load();
            foreach (var g in cfg.Games)
                LogHelper.Log($"IconName={g.IconName}, RootPath={g.RootPath}, SyncEnabled={g.SyncLaunchEnabled}, SyncApps={g.SyncApps.Count}");

            var entry = cfg.Games.Find(g => g.IconName == iconName && g.RootPath == rootPath);
            entry ??= cfg.Games.Find(g => g.IconName == iconName);

            if (entry?.SyncLaunchEnabled == true && entry.SyncApps?.Count > 0)
            {
                foreach (var app in entry.SyncApps)
                {
                    if (File.Exists(app.Path))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(app.Path, app.Args ?? "")
                            {
                                UseShellExecute = true,
                                WorkingDirectory = Path.GetDirectoryName(app.Path)!
                            });
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogError(ex, "SyncLaunch");
                        }
                    }
                }
            }

            bool isEndfield = GameChannelCatalog.IsFamily(
                iconName, GameFamily.Endfield);
            string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
            string exePath = Path.Combine(rootPath, exeName);
            if (!File.Exists(exePath)) throw new Exception($"未找到 {exeName}");
            string launchArgs = "";
            if (iconName == "PlayEndfield")
            {
                var cfgToken = ConfigHelper.Load();
                var tokenEntry = cfgToken.Games.Find(g => g.IconName == iconName);
                string token = tokenEntry?.SessionToken;
                if (!string.IsNullOrEmpty(token))
                    launchArgs = $"-launcher_sub_channel=802 --g_session_token={token}";
            }

            // ── 自定义启动参数 ──
            if (entry?.CustomLaunchArgsEnabled == true && !string.IsNullOrWhiteSpace(entry.CustomLaunchArgs))
            {
                launchArgs = string.IsNullOrEmpty(launchArgs)
                    ? entry.CustomLaunchArgs.Trim()
                    : launchArgs + " " + entry.CustomLaunchArgs.Trim();
            }

            if (EndfieldLauncher.Supports(iconName))
            {
                var process = await EndfieldLauncher.StartAsync(rootPath, launchArgs, iconName);
                TrackLaunchedGame(exePath, process);
                return process;
            }

            using var started = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = launchArgs,
                WorkingDirectory = rootPath,
                UseShellExecute = true
            });
            // Other channels retain the existing path-based process discovery.
            return null;
        }

        public static Process FindRunningGameProcess(
            string rootPath,
            bool isEndfield)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return null;

            string expectedPath;
            try
            {
                var executable = isEndfield ? "Endfield.exe" : "Arknights.exe";
                expectedPath = Path.GetFullPath(Path.Combine(rootPath, executable));
            }
            catch
            {
                return null;
            }

            lock (RunningGamesLock)
            {
                if (LaunchedGames.TryGetValue(expectedPath, out var launched))
                {
                    try
                    {
                        if (!launched.HasExited)
                            return Process.GetProcessById(launched.Id);
                    }
                    catch (InvalidOperationException) { }
                    catch (ArgumentException) { }
                    LaunchedGames.Remove(expectedPath);
                    launched.Dispose();
                }
            }

            var processName = isEndfield ? "Endfield" : "Arknights";
            Process match = null;
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var actualPath = GetProcessImagePath(process);
                    if (!string.IsNullOrWhiteSpace(actualPath) &&
                        string.Equals(
                            Path.GetFullPath(actualPath),
                            expectedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (match == null)
                        {
                            match = process;
                            continue;
                        }
                    }
                }
                catch
                {
                    // Process information can become unavailable while it exits.
                }
                process.Dispose();
            }

            return match;
        }

        private static void TrackLaunchedGame(string executable, Process process)
        {
            var path = Path.GetFullPath(executable);
            lock (RunningGamesLock)
            {
                if (LaunchedGames.TryGetValue(path, out var previous))
                    previous.Dispose();
                // Keep our own process handle: the launch UI disposes its Process
                // after the game exits or when it closes.
                LaunchedGames[path] = Process.GetProcessById(process.Id);
            }
        }

        private static string GetProcessImagePath(Process process)
        {
            using var handle = OpenProcess(0x1000, false, process.Id);
            if (handle.IsInvalid) return null;
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            return QueryFullProcessImageName(handle, 0, path, ref length)
                ? path.ToString(0, length)
                : null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(
            uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(
            SafeProcessHandle process, uint flags, StringBuilder path, ref int size);

        public static bool IsGameRunningFromDirectory(
            string rootPath,
            bool isEndfield)
        {
            using var process = FindRunningGameProcess(rootPath, isEndfield);
            return process != null;
        }

        public static void EnsureGameClientClosed(
            string rootPath,
            bool isEndfield)
        {
            if (!IsGameRunningFromDirectory(rootPath, isEndfield)) return;

            throw new InvalidOperationException(
                GetCloseGameClientMessage());
        }

        private static string GetCloseGameClientMessage() =>
            Localizer.GetRequiredString("App.Game.CloseClientBeforeSwitch");

        private static bool IsFileInUseException(Exception exception)
        {
            if (exception is AggregateException aggregate)
                return aggregate.Flatten().InnerExceptions.Any(
                    IsFileInUseException);

            if (exception is IOException ioException)
            {
                var errorCode = ioException.HResult & 0xFFFF;
                if (errorCode is 32 or 33) return true;
            }

            return exception?.InnerException != null &&
                   IsFileInUseException(exception.InnerException);
        }

    }
}
