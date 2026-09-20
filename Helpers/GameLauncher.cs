using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class GameLauncher
    {
        private const string ArknightsOfficialSdkDataDirName = "sdk_data_70613ebd03f0610da8808f16040ee1b3";

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
                        AntdUI.Localization.Get(
                            "App.LinkedClient.Error.GroupBusy",
                            "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
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
                                AntdUI.Localization.Get(
                                    "App.Switch.NoPayload",
                                    "未找到切服资源（文件夹或 ZIP 均不存在）"));
                        }

                        onProgress(AntdUI.Localization.Get(
                            "App.Switch.Copying", "切服中..."));

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

                string doneMsg = AntdUI.Localization.Get(
                    "App.Switch.DoneCopy", "游戏启动中···");
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

        public static void StartArknights(string rootPath, string iconName)
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

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = launchArgs,
                WorkingDirectory = rootPath,
                UseShellExecute = true
            });
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

            var processName = isEndfield ? "Endfield" : "Arknights";
            Process match = null;
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var actualPath = process.MainModule?.FileName;
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
            AntdUI.Localization.Get(
                "App.Game.CloseClientBeforeSwitch",
                "渠道文件正在被占用，请关闭游戏客户端后重试。");

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

        public static async Task BackupAccount(string accountId)
        {
            string target = Path.Combine(ConfigHelper.AccountBackupDir, accountId);

            var sdkDir = GetArknightsOfficialSdkDataDirectory(createIfMissing: false);
            if (sdkDir == null) return;

            if (Directory.Exists(target)) Directory.Delete(target, true);
            await CopyDirectory(sdkDir, target);
        }

        public static async Task RestoreAccount(string accountId)
        {
            string backupDir = Path.Combine(ConfigHelper.AccountBackupDir, accountId);
            if (!Directory.Exists(backupDir))
                throw new Exception($"账号备份不存在，请先点击「保存账号」记录该账号。");

            var sdkDir = GetArknightsOfficialSdkDataDirectory(createIfMissing: true);
            if (sdkDir == null)
                throw new Exception("未找到 sdk_data_* 目录，请先启动一次游戏。");

            if (Directory.Exists(sdkDir)) Directory.Delete(sdkDir, true);
            await CopyDirectory(backupDir, sdkDir);
        }

        private static string GetArknightsOfficialSdkDataDirectory(bool createIfMissing)
        {
            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Arknights"
            );

            string officialDir = Path.Combine(sdkPath, ArknightsOfficialSdkDataDirName);
            if (Directory.Exists(officialDir) || createIfMissing)
                return officialDir;

            return null;
        }

        public static async Task BackupEndfieldAccount(string accountId)
        {
            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            string target = Path.Combine(ConfigHelper.EndAccountBackupDir, accountId);

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null) return;

            if (Directory.Exists(target)) Directory.Delete(target, true);
            await CopyDirectory(sdkDir, target);
        }

        public static async Task RestoreEndfieldAccount(string accountId)
        {
            string backupDir = Path.Combine(ConfigHelper.EndAccountBackupDir, accountId);
            if (!Directory.Exists(backupDir))
                throw new Exception($"账号备份不存在，请先点击「保存账号」记录该账号。");

            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null)
                throw new Exception("未找到 sdk_data_* 目录，请先启动一次游戏。");

            if (Directory.Exists(sdkDir)) Directory.Delete(sdkDir, true);
            await CopyDirectory(backupDir, sdkDir);
        }

        public static async Task BackupGlobalEndfieldAccount(string accountId)
        {
            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            string sdkDir = Path.Combine(sdkPath, "sdk_data_e64e200fc9f5ea3996533c6a5d5c026e");
            if (!Directory.Exists(sdkDir)) return;

            string target = Path.Combine(ConfigHelper.GlobalEndAccountBackupDir, accountId);
            if (Directory.Exists(target)) Directory.Delete(target, true);
            await CopyDirectory(sdkDir, target);
        }

        public static async Task RestoreGlobalEndfieldAccount(string accountId)
        {
            string backupDir = Path.Combine(ConfigHelper.GlobalEndAccountBackupDir, accountId);
            if (!Directory.Exists(backupDir))
                throw new Exception($"账号备份不存在，请先点击「保存账号」记录该账号。");

            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            string sdkDir = Path.Combine(sdkPath, "sdk_data_e64e200fc9f5ea3996533c6a5d5c026e");
            if (Directory.Exists(sdkDir)) Directory.Delete(sdkDir, true);
            await CopyDirectory(backupDir, sdkDir);
        }

        public static async Task CopyDirectory(string sourceDir, string targetDir, int maxRetries = 5)
        {
            sourceDir = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar);
            targetDir = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar);

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        File.Copy(file, destFile, true);
                        break;
                    }
                    catch (IOException) when (i < maxRetries - 1)
                    {
                        await Task.Delay(1000);
                    }
                }
            }
        }
    }
}
