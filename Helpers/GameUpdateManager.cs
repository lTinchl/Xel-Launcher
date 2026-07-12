using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management;

namespace XelLauncher.Helpers
{
    public sealed class GameUpdateProgress
    {
        public InstallProgressState State { get; init; }
        public long Downloaded { get; init; }
        public long Total { get; init; }
    }

    public sealed class ActiveGameUpdate
    {
        private readonly CancellationTokenSource _cts = new();
        private volatile GameUpdateProgress _lastProgress;

        internal ActiveGameUpdate(string iconName, string installPath)
        {
            IconName = iconName;
            InstallPath = installPath;
        }

        public string IconName { get; }
        public string InstallPath { get; }
        public Task Task { get; internal set; }
        public GameUpdateProgress LastProgress => _lastProgress;
        public bool IsCancellationRequested => _cts.IsCancellationRequested;
        public bool CanPause => _lastProgress?.State.HasFlag(InstallProgressState.Download) == true;
        public event Action<GameUpdateProgress> ProgressChanged;
        internal event Action<ActiveGameUpdate> CancelRequested;

        public bool Cancel()
        {
            // Cancelling while archives are being extracted, patched, removed, or
            // committed can leave the installation half-applied. Only the download
            // stage uses resumable .tmp files and is safe to pause.
            if (!CanPause || _cts.IsCancellationRequested)
                return false;

            CancelRequested?.Invoke(this);
            _cts.Cancel();
            return true;
        }

        internal CancellationToken Token => _cts.Token;

        internal void Report(InstallProgressState state, long downloaded, long total)
        {
            var progress = new GameUpdateProgress
            {
                State = state,
                Downloaded = downloaded,
                Total = total
            };
            _lastProgress = progress;
            ProgressChanged?.Invoke(progress);
        }
    }

    public static class GameUpdateManager
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, ActiveGameUpdate> ActiveUpdates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PausedUpdatePaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTimeOffset> CompletedUpdates = new(StringComparer.OrdinalIgnoreCase);

        public static ActiveGameUpdate StartOrAttach(string iconName, string installPath, out bool started)
        {
            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                if (ActiveUpdates.TryGetValue(key, out var existing) && !existing.Task.IsCompleted)
                {
                    started = false;
                    return existing;
                }

                var update = new ActiveGameUpdate(iconName, installPath);
                PausedUpdatePaths.Remove(key);
                update.CancelRequested += MarkPaused;
                ActiveUpdates[key] = update;
                update.Task = RunAsync(key, update);
                LogHelper.Log($"Game update started: {iconName} | {installPath}");
                started = true;
                return update;
            }
        }

        public static ActiveGameUpdate Find(string installPath)
        {
            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                return ActiveUpdates.TryGetValue(key, out var update) && !update.Task.IsCompleted
                    ? update
                    : null;
            }
        }

        public static bool IsPaused(string installPath)
        {
            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                return PausedUpdatePaths.Contains(key);
            }
        }

        public static void ClearPaused(string installPath)
        {
            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                PausedUpdatePaths.Remove(key);
            }
        }

        public static bool IsRecentlyCompleted(string iconName, string installPath)
        {
            var key = GetCompletedKey(iconName, installPath);
            lock (SyncRoot)
            {
                if (!CompletedUpdates.TryGetValue(key, out var completedAt))
                    return false;

                if (DateTimeOffset.UtcNow - completedAt <= TimeSpan.FromMinutes(5))
                    return true;

                CompletedUpdates.Remove(key);
                return false;
            }
        }

        private static void MarkPaused(ActiveGameUpdate update)
        {
            var key = GetKey(update.InstallPath);
            lock (SyncRoot)
            {
                PausedUpdatePaths.Add(key);
            }
            LogHelper.Log($"Game update pause requested: {update.IconName} | {update.InstallPath}");
        }

        private static async Task RunAsync(string key, ActiveGameUpdate update)
        {
            try
            {
                using var service = new EndfieldService(update.IconName);
                await service.InstallOrUpdateAsync(update.InstallPath, update.Report, update.Token)
                    .ConfigureAwait(false);
                if (update.Token.IsCancellationRequested)
                {
                    LogHelper.Log($"Game update paused: {update.IconName} | {update.InstallPath}");
                    return;
                }
                lock (SyncRoot)
                {
                    PausedUpdatePaths.Remove(key);
                    CompletedUpdates[GetCompletedKey(update.IconName, update.InstallPath)] = DateTimeOffset.UtcNow;
                }
                await PersistCompletedStatusAsync(update.IconName, update.InstallPath).ConfigureAwait(false);
                LogHelper.Log($"Game update completed: {update.IconName} | {update.InstallPath}");
            }
            catch (OperationCanceledException ex)
            {
                LogHelper.LogError(ex, $"Game update paused: {update.IconName} | {update.InstallPath}");
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"Game update failed: {update.IconName} | {update.InstallPath}");
                throw;
            }
            finally
            {
                update.CancelRequested -= MarkPaused;
                lock (SyncRoot)
                {
                    if (ActiveUpdates.TryGetValue(key, out var current) && ReferenceEquals(current, update))
                        ActiveUpdates.Remove(key);
                }
            }
        }

        private static async Task PersistCompletedStatusAsync(string iconName, string installPath)
        {
            try
            {
                using var service = new EndfieldService(iconName);
                var status = await service.CheckStatusAsync(installPath).ConfigureAwait(false);
                var cfg = ConfigHelper.Load();
                var localVersion = status?.LocalVersion ?? status?.RemoteVersion ?? "";
                var remoteVersion = status?.RemoteVersion ?? status?.LocalVersion ?? "";

                var entry = cfg.Games.Find(g => string.Equals(g.IconName, iconName, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    entry.RootPath = installPath;
                    if (!string.IsNullOrEmpty(localVersion))
                        entry.LocalVersion = localVersion;
                }

                cfg.GameStatusCache[iconName] = new Models.CachedGameStatus
                {
                    IsInstalled = status?.IsInstalled ?? true,
                    HasUpdate = false,
                    HasPreload = status?.HasPreload ?? false,
                    PreloadCompleted = false,
                    LocalVersion = localVersion,
                    RemoteVersion = remoteVersion,
                    PreloadVersion = status?.PreloadVersion ?? "",
                    InstallPath = installPath
                };

                ConfigHelper.Save(cfg);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GameUpdateManager.PersistCompletedStatusAsync({iconName})");
            }
        }

        private static string GetKey(string installPath)
        {
            var fullPath = Path.GetFullPath(installPath ?? "");
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string GetCompletedKey(string iconName, string installPath) =>
            $"{iconName}|{GetKey(installPath)}";
    }
}
