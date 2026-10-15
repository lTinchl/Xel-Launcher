using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers;

internal static class EndfieldLauncher
{
    internal static bool Supports(string iconName) =>
        iconName is "Endfield" or "BiliEndfield" or "GlobalEndfield";

    internal static Task<Process> StartAsync(string gamePath, string arguments, string iconName) =>
        ServerPayloadUpdater.UsePayloadDirectoryAsync(iconName, async directory =>
        {
            var profile = ServerPayloadUpdater.GetProfile(iconName) ??
                throw new InvalidOperationException($"Missing payload profile for {iconName}.");
            string gamesPath = Path.Combine(directory, "Games.exe");
            bool global = iconName == "GlobalEndfield";
            // A cache refresh takes the same payload lock, so the executable
            // cannot be replaced while we create the suspended parent.
            if (!File.Exists(gamesPath))
                await EndfieldGamesPayloadUpdater.EnsureAsync(
                    profile, directory, null, CancellationToken.None).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                if (File.GetAttributes(gamesPath).HasFlag(FileAttributes.ReparsePoint) ||
                    !OfficialLauncherTrust.IsTrusted(gamesPath, global))
                    throw new InvalidOperationException(
                        "官方 Games.exe 数字签名或发行者验证失败，请更新切服资源后重试。");

                using var imageLock = File.Open(gamesPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                SafeProcessHandle parent = ParentProcessLauncher.StartSuspended(gamesPath);
                try
                {
                    var process = ParentProcessLauncher.Start(parent,
                        Path.GetFullPath(Path.Combine(gamePath, "Endfield.exe")),
                        arguments, Path.GetFullPath(gamePath));
                    _ = new SuspendedParentLifetime(parent, process);
                    parent = null;
                    LogHelper.Log($"Endfield launch: channel={iconName}, process={process.Id}");
                    return process;
                }
                finally
                {
                    if (parent != null)
                    {
                        ParentProcessLauncher.Terminate(parent);
                        parent.Dispose();
                    }
                }
            }).ConfigureAwait(false);
        });

    private sealed class SuspendedParentLifetime
    {
        private SafeProcessHandle _parent;

        internal SuspendedParentLifetime(SafeProcessHandle parent, Process game)
        {
            _parent = parent;
            AppDomain.CurrentDomain.ProcessExit += OnLauncherExit;
            _ = WaitForGameExitAsync(game);
        }

        private async Task WaitForGameExitAsync(Process game)
        {
            try { await game.WaitForExitAsync().ConfigureAwait(false); }
            catch (Exception ex) { LogHelper.LogError(ex, "Endfield parent lifetime"); }
            finally { Stop(); }
        }

        private void OnLauncherExit(object sender, EventArgs args) => Stop();

        private void Stop()
        {
            var parent = Interlocked.Exchange(ref _parent, null);
            if (parent == null) return;
            AppDomain.CurrentDomain.ProcessExit -= OnLauncherExit;
            ParentProcessLauncher.Terminate(parent);
            parent.Dispose();
        }
    }
}
