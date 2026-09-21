using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;
namespace XelLauncher.Forms
{
    public partial class GamePage : UserControl
    {
        private bool _isGameRunning;

        private void InitializeGameRunningMonitor()
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => UpdateGameRunningState();
            HandleCreated += (s, e) =>
            {
                UpdateGameRunningState();
                timer.Start();
            };
            Disposed += (s, e) => timer.Dispose();
        }

        private void UpdateGameRunningState()
        {
            if (IsDisposed || GameStart.IsDisposed) return;
            bool running;
            try
            {
                var launchDirectory = GetExpectedGameLaunchDirectory();
                using var process = GameLauncher.FindRunningGameProcess(
                    launchDirectory,
                    GameChannelCatalog.IsFamily(_game.IconName, GameFamily.Endfield));
                running = process != null;
            }
            catch { return; }

            if (_isGameRunning == running) return;
            _isGameRunning = running;
            RefreshGameStartButton();
        }

        private string GetExpectedGameLaunchDirectory()
        {
            var rootPath = GetConfiguredGamePath();
            if (string.IsNullOrWhiteSpace(rootPath)) return rootPath;

            try
            {
                var cfg = ConfigHelper.Load();
                if (!cfg.UseHardLink) return rootPath;

                var resolution = SharedRootManager.Resolve(
                    cfg,
                    _game.IconName,
                    rootPath,
                    detectBaseChannel: false,
                    out _);
                if (resolution.Mode != SharedRootMode.Shared ||
                    resolution.Base == null ||
                    resolution.Target == null ||
                    string.Equals(
                        resolution.Base.Channel,
                        resolution.Target.Channel,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return rootPath;
                }

                return LinkedRuntimeService.GetRuntimePath(
                    resolution.GameId,
                    resolution.RootPath,
                    resolution.Target.Channel);
            }
            catch
            {
                return rootPath;
            }
        }

        private static bool IsGameClientRunningAt(
            string launchDirectory,
            bool isEndfield) =>
            GameLauncher.IsGameRunningFromDirectory(
                launchDirectory, isEndfield);

        private static string CloseGameClientMessage =>
            Localizer.GetRequiredString("App.Game.CloseClientBeforeSwitch");

        private async Task<bool> CheckGameStatusAsync()
        {
            try { _service = new EndfieldService(_game.IconName); }
            catch { return false; }

            _ = RefreshRemoteCoverAsync();
            _ = RefreshLauncherNoticeAsync();

            try
            {
                var currentPath = GetConfiguredGamePath();
                if (_gameState == GameState.Repairing || GameRepairManager.IsRepairing(currentPath))
                {
                    _repairingPath = currentPath;
                    _gameState = GameState.Repairing;
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                string path = entry?.RootPath ?? _game.RootPath;
                if (string.IsNullOrEmpty(path)) return false;
                var operationTarget = SharedRootManager.ResolveMaintenanceTarget(
                    _game.IconName, path, allowUninstalledTarget: true);
                var effectiveIconName = operationTarget.EffectiveIconName;

                var activeUpdate = GameUpdateManager.Find(path);
                if (activeUpdate != null)
                {
                    _activeUpdate = activeUpdate;
                    _gameState = activeUpdate.IsCancellationRequested ? GameState.Paused : GameState.Downloading;
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                if (GameUpdateManager.IsPaused(path))
                {
                    _gameState = GameState.Paused;
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                if (GameUpdateManager.IsRecentlyCompleted(effectiveIconName, path))
                {
                    _gameState = GameState.Ready;
                    var completedCfg = ConfigHelper.Load();
                    completedCfg.GameStatusCache[_game.IconName] = new CachedGameStatus
                    {
                        IsInstalled = true,
                        HasUpdate = false,
                        HasPreload = false,
                        PreloadCompleted = false,
                        LocalVersion = entry?.LocalVersion ?? "",
                        RemoteVersion = entry?.LocalVersion ?? "",
                        PreloadVersion = "",
                        InstallPath = path
                    };
                    ConfigHelper.Save(completedCfg);
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                using var statusService = new EndfieldService(effectiveIconName);
                var status = await statusService.CheckStatusAsync(path);
                if (status == null) return false;

                activeUpdate = GameUpdateManager.Find(path);
                if (activeUpdate != null)
                {
                    _activeUpdate = activeUpdate;
                    _gameState = activeUpdate.IsCancellationRequested ? GameState.Paused : GameState.Downloading;
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                var hasUpdate = cfg.CheckGameUpdates &&
                                status.HasUpdate &&
                                !string.Equals(status.LocalVersion, status.RemoteVersion,
                                    StringComparison.OrdinalIgnoreCase);
                var hasPreload = cfg.CheckGameUpdates &&
                                 status.IsInstalled &&
                                 !hasUpdate &&
                                 status.HasPreload;
                cfg.GameStatusCache.TryGetValue(_game.IconName, out var previousCached);
                var preloadVersion = status.PreloadVersion ?? "";
                var preloadCompleted = hasPreload &&
                                       IsPreloadCompletedForPath(cfg.GameStatusCache.Values, path, preloadVersion);
                _preloadCompleted = preloadCompleted;

                _gameState = !status.IsInstalled ? GameState.NotInstalled
                           : hasUpdate           ? GameState.HasUpdate
                           : hasPreload          ? GameState.HasPreload
                                                 : GameState.Ready;

                // Write cache after live check completes
                var cfgToUpdate = ConfigHelper.Load();
                cfgToUpdate.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = status.IsInstalled,
                    HasUpdate = hasUpdate,
                    HasPreload = hasPreload,
                    PreloadCompleted = preloadCompleted,
                    LocalVersion = status.LocalVersion ?? "",
                    RemoteVersion = status.RemoteVersion ?? "",
                    PreloadVersion = preloadVersion,
                    InstallPath = path
                };
                var entryToUpdate = cfgToUpdate.Games.Find(g => g.IconName == _game.IconName);
                if (entryToUpdate != null && !string.IsNullOrEmpty(status.LocalVersion))
                    entryToUpdate.LocalVersion = status.LocalVersion;
                ConfigHelper.Save(cfgToUpdate);

                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        RefreshGameStartButton();
                        RefreshGameInfoBadge();
                    });

                return true;
            }
            catch { return false; }
        }

        private void RefreshGameStartButton()
        {
            ((GameLaunchButton)GameStart).SetRunningAppearance(_isGameRunning);
            if (_isGameRunning)
            {
                GameStart.Loading = false;
                GameStart.Text = Localizer.GetRequiredString("App.Game.Running");
                GameStart.IconSvg = "PlayCircleOutlined";
                GameStart.Enabled = false;
                RefreshPreloadButton();
                return;
            }

            GameStart.Enabled = true;
            if (GameRepairManager.IsRepairing(GetConfiguredGamePath()))
                _gameState = GameState.Repairing;

            switch (_gameState)
            {
                case GameState.NotInstalled:
                    GameStart.Text = Localizer.GetRequiredString("App.Game.Install");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                case GameState.HasUpdate:
                    GameStart.Text = Localizer.GetRequiredString("App.Game.Update");
                    GameStart.IconSvg = "SyncOutlined";
                    break;
                case GameState.Downloading:
                    var updateProgress = _activeUpdate?.LastProgress;
                    if (_activeUpdate?.CanPause == true)
                    {
                        GameStart.Text = Localizer.GetRequiredString("App.Game.Pause");
                        GameStart.IconSvg = "PauseOutlined";
                    }
                    else
                    {
                        GameStart.Text = updateProgress == null
                            ? Localizer.GetRequiredString("App.Game.Install.Updating")
                            : FormatInstallProgress(updateProgress.State, updateProgress.Downloaded, updateProgress.Total)
                              ?? Localizer.GetRequiredString("App.Game.Install.Updating");
                        GameStart.IconSvg = "LoadingOutlined";
                        GameStart.Enabled = false;
                    }
                    break;
                case GameState.Paused:
                    GameStart.Text = Localizer.GetRequiredString("App.Game.Resume");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                case GameState.Repairing:
                    GameStart.Text = Localizer.GetRequiredString("App.Game.Repair.Running");
                    GameStart.IconSvg = "SafetyCertificateOutlined";
                    GameStart.Enabled = false;
                    break;
                default:
                    GameStart.Text = Localizer.GetRequiredString("App.Game.Start");
                    GameStart.IconSvg = "PoweroffOutlined";
                    break;
            }

            RefreshPreloadButton();
        }

        private void InstallOrUpdateGame()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            if (string.IsNullOrEmpty(path))
            {
                path = Helpers.DialogHelper.BrowseFolder(
                    _overview?.IsHandleCreated == true ? _overview.Handle : IntPtr.Zero,
                    Localizer.GetRequiredString("App.Game.SelectInstallDir"));
                if (path == null) return;
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null)
                {
                    try
                    {
                        LinkedClientPolicy.UpdatePath(cfg2, e2, path);
                    }
                    catch (InvalidOperationException ex)
                    {
                        AntdUI.Message.warn(_overview, ex.Message);
                        return;
                    }
                    ConfigHelper.Save(cfg2);
                }
            }

            try
            {
                LinkedClientPolicy.ThrowIfSharedClient(_game.IconName, path);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            var capturedPath = path;
            ActiveGameUpdate update;
            bool started;
            try
            {
                update = GameUpdateManager.StartOrAttach(
                    _game.IconName, capturedPath, out started);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            _gameState = GameState.Downloading;
            _activeUpdate = update;
            RefreshGameStartButton();
            AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.Game.Install.Init"), async config =>
            {
                long lastTick = 0;
                InstallProgressState? lastLoggedState = null;
                InstallProgressState? lastButtonState = null;
                Action<GameUpdateProgress> progressHandler = progress =>
                {
                    if (!update.IsCancellationRequested && _gameState != GameState.Downloading)
                    {
                        _gameState = GameState.Downloading;
                        if (IsHandleCreated && !IsDisposed)
                            BeginInvoke(() =>
                            {
                                if (!GameStart.IsDisposed) RefreshGameStartButton();
                            });
                    }

                    if (lastLoggedState != progress.State)
                    {
                        lastLoggedState = progress.State;
                        LogHelper.Log($"Game update state: {_game.IconName} | {capturedPath} | {progress.State}");
                    }

                    if (lastButtonState != progress.State)
                    {
                        lastButtonState = progress.State;
                        if (IsHandleCreated && !IsDisposed)
                            BeginInvoke(() =>
                            {
                                if (!GameStart.IsDisposed) RefreshGameStartButton();
                            });
                    }

                    var label = FormatInstallProgress(progress.State, progress.Downloaded, progress.Total);
                    if (string.IsNullOrWhiteSpace(label)) return;

                    long now = Environment.TickCount64;
                    if (now - lastTick < 800) return;
                    lastTick = now;
                    config.Text = label;
                    config.Refresh();
                };

                try
                {
                    update.ProgressChanged += progressHandler;
                    if (!started && update.LastProgress != null)
                        progressHandler(update.LastProgress);
                    else if (!started)
                    {
                        config.Text = Localizer.GetRequiredString("App.Game.Install.Updating");
                        config.Refresh();
                    }

                    await update.Task;
                    if (update.IsCancellationRequested)
                    {
                        _gameState = GameState.Paused;
                        config.OK(Localizer.GetRequiredString("App.Game.Install.Paused"));
                    }
                    else
                    {
                        if (!LinkedClientPolicy.ShouldSkipServerPayloadSwitch(
                                update.IconName, capturedPath) &&
                            IsServerPayloadAutoUpdateEnabled(update.IconName))
                        {
                            config.Text = Localizer.GetRequiredString("App.PayloadUpdate.AutoUpdating");
                            config.Refresh();
                            await UpdateServerPayloadAfterGameUpdateAsync(
                                update.IconName);
                        }

                        MarkGameReadyAfterInstall(capturedPath);
                        config.OK(Localizer.GetRequiredString("App.Game.Install.Success"));
                    }
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    _gameState = GameState.Paused;
                    LogHelper.LogError(ex, $"Game update paused in UI: {_game.IconName} | {capturedPath}");
                    config.OK(Localizer.GetRequiredString("App.Game.Install.Paused"));
                }
                catch (Exception ex)
                {
                    _gameState = GameState.Unknown;
                    LogHelper.LogError(ex, $"Game update failed in UI: {_game.IconName} | {capturedPath}");
                    _ = CheckGameStatusAsync();
                    config.Error(ex.Message);
                }
                finally
                {
                    update.ProgressChanged -= progressHandler;
                    if (ReferenceEquals(_activeUpdate, update))
                        _activeUpdate = null;

                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            if (!GameStart.IsDisposed) RefreshGameStartButton();
                        });
                    }
                }
            });
        }

        private void PreloadGame()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;
            GameOperationTarget operationTarget;

            try
            {
                operationTarget = SharedRootManager.ResolveMaintenanceTarget(
                    _game.IconName, path, allowUninstalledTarget: false);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }
            var effectiveIconName = operationTarget.EffectiveIconName;
            path = operationTarget.RootPath;

            try
            {
                LinkedClientPolicy.ThrowIfSharedClient(effectiveIconName, path);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.Game.WarnSelectDir"));
                return;
            }

            if (GameUpdateManager.Find(path) != null)
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.Game.Preload.UpdateRunning"));
                return;
            }

            if (_preloadRunning)
            {
                AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.Preload.Running"));
                return;
            }

            if (_preloadCompleted && _gameState == GameState.HasPreload)
            {
                AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.Preload.Success"));
                return;
            }

            if (!LinkedClientOperationCoordinator.TryAcquire(
                    effectiveIconName, path, out var operationLease))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                return;
            }

            try
            {
                // BaseChannel may have changed while this page was waiting for
                // the path lease. Re-resolve under the lease before choosing the
                // preload manifest.
                operationTarget = SharedRootManager.ResolveMaintenanceTarget(
                    _game.IconName, path, allowUninstalledTarget: false);
                effectiveIconName = operationTarget.EffectiveIconName;
                path = operationTarget.RootPath;
                LinkedClientPolicy.ThrowIfSharedClient(effectiveIconName, path);
            }
            catch (InvalidOperationException ex)
            {
                operationLease.Dispose();
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            _preloadCompleted = false;
            _preloadRunning = true;
            RefreshGameStartButton();

            AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.Game.Preload.Init"), async config =>
            {
                long lastTick = 0;
                InstallProgressState? lastLoggedState = null;

                try
                {
                    await LinkedRuntimeService.PrepareSharedRootForMutationAsync(
                        effectiveIconName,
                        path,
                        SharedRootMutationKind.Preload);
                    using var service = new EndfieldService(effectiveIconName);
                    LogHelper.Log(
                        $"Game preload started: requested={_game.IconName} | " +
                        $"effective={effectiveIconName} | {path}");
                    await service.PreloadAsync(path, (state, downloaded, total) =>
                    {
                        if (lastLoggedState != state)
                        {
                            lastLoggedState = state;
                            LogHelper.Log($"Game preload state: {_game.IconName} | {path} | {state}");
                        }

                        var label = FormatInstallProgress(state, downloaded, total);
                        if (string.IsNullOrWhiteSpace(label)) return;

                        long now = Environment.TickCount64;
                        if (now - lastTick < 800) return;
                        lastTick = now;
                        config.Text = label;
                        config.Refresh();
                    });

                    LogHelper.Log(
                        $"Game preload completed: requested={_game.IconName} | " +
                        $"effective={effectiveIconName} | {path}");
                    _preloadCompleted = true;
                    MarkPreloadCompleted(path);
                    config.OK(Localizer.GetRequiredString("App.Game.Preload.Success"));
                    _ = CheckGameStatusAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex, $"Game preload failed: {_game.IconName} | {path}");
                    if (IsNoPreloadPackage(ex))
                    {
                        ClearPreloadAvailability(path);
                        config.OK(Localizer.GetRequiredString("App.Game.Preload.None"));
                    }
                    else
                    {
                        config.Error(ex.Message);
                    }

                    _ = CheckGameStatusAsync();
                }
                finally
                {
                    operationLease.Dispose();
                    _preloadRunning = false;
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            if (!GameStart.IsDisposed) RefreshGameStartButton();
                        });
                    }
                }
            });
        }

        private void ClearPreloadAvailability(string installPath)
        {
            try
            {
                var cfg = ConfigHelper.Load();
                if (cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached) &&
                    IsSameInstallPath(cached.InstallPath, installPath))
                {
                    cached.HasPreload = false;
                    cached.PreloadCompleted = false;
                    cached.PreloadVersion = "";
                    cfg.GameStatusCache[_game.IconName] = cached;
                    ConfigHelper.Save(cfg);
                }
            }
            catch { }

            if (_gameState == GameState.HasPreload)
                _gameState = GameState.Ready;
            _preloadCompleted = false;
        }

        private void MarkPreloadCompleted(string installPath)
        {
            try
            {
                var cfg = ConfigHelper.Load();
                cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached);
                var preloadVersion = cached?.PreloadVersion ?? "";

                cfg.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = cached?.IsInstalled ?? true,
                    HasUpdate = cached?.HasUpdate ?? false,
                    HasPreload = true,
                    PreloadCompleted = true,
                    LocalVersion = cached?.LocalVersion ?? "",
                    RemoteVersion = cached?.RemoteVersion ?? "",
                    PreloadVersion = preloadVersion,
                    InstallPath = installPath
                };

                if (!string.IsNullOrWhiteSpace(preloadVersion))
                {
                    foreach (var key in cfg.GameStatusCache.Keys.ToList())
                    {
                        var existing = cfg.GameStatusCache[key];
                        if (!IsSameInstallPath(existing.InstallPath, installPath) ||
                            !string.Equals(existing.PreloadVersion ?? "", preloadVersion,
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        existing.PreloadCompleted = true;
                        cfg.GameStatusCache[key] = existing;
                    }
                }

                ConfigHelper.Save(cfg);
            }
            catch { }
        }

        private static bool IsNoPreloadPackage(Exception ex)
        {
            if (ex is InvalidOperationException &&
                ex.Message.Contains("No preload package", StringComparison.OrdinalIgnoreCase))
                return true;

            return ex is AggregateException aex && aex.InnerExceptions.Any(IsNoPreloadPackage);
        }

        private static bool IsCancellation(Exception ex) =>
            ex is OperationCanceledException ||
            (ex is AggregateException aex && aex.InnerExceptions.Count > 0 &&
             aex.InnerExceptions[0] is OperationCanceledException);

        private void RepairGameIntegrity()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.Game.WarnSelectDir"));
                return;
            }

            GameOperationTarget operationTarget;
            try
            {
                operationTarget = SharedRootManager.ResolveMaintenanceTarget(
                    _game.IconName, path, allowUninstalledTarget: false);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }
            var effectiveIconName = operationTarget.EffectiveIconName;
            path = operationTarget.RootPath;

            try
            {
                LinkedClientPolicy.ThrowIfSharedClient(effectiveIconName, path);
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            if (GameUpdateManager.Find(path) != null)
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.Game.Repair.UpdateRunning"));
                return;
            }

            if (_gameState == GameState.Repairing || IsSameInstallPath(_repairingPath, path))
            {
                AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.Repair.AlreadyRunning"));
                return;
            }

            try
            {
                if (!GameRepairManager.TryStart(effectiveIconName, path))
                {
                    AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.Repair.AlreadyRunning"));
                    return;
                }
            }
            catch (InvalidOperationException ex)
            {
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            try
            {
                // GameRepairManager now owns the physical-root lease. Resolve
                // once more so verification cannot use a channel that was base
                // before a just-completed server switch.
                operationTarget = SharedRootManager.ResolveMaintenanceTarget(
                    _game.IconName, path, allowUninstalledTarget: false);
                effectiveIconName = operationTarget.EffectiveIconName;
                path = operationTarget.RootPath;
                LinkedClientPolicy.ThrowIfSharedClient(effectiveIconName, path);
            }
            catch (InvalidOperationException ex)
            {
                GameRepairManager.Complete(path);
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            var previousState = _gameState;
            _repairingPath = path;
            _gameState = GameState.Repairing;
            RefreshGameStartButton();

            AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.Game.Repair.Init"), async config =>
            {
                long lastTick = 0;
                InstallProgressState? lastLoggedState = null;

                try
                {
                    await LinkedRuntimeService.PrepareSharedRootForMutationAsync(
                        effectiveIconName,
                        path,
                        SharedRootMutationKind.Repair);
                    using var service = new EndfieldService(effectiveIconName);
                    LogHelper.Log(
                        $"Game repair started: requested={_game.IconName} | " +
                        $"effective={effectiveIconName} | {path}");
                    await service.RepairAsync(path, (state, downloaded, total) =>
                    {
                        if (lastLoggedState != state)
                        {
                            lastLoggedState = state;
                            LogHelper.Log($"Game repair state: {_game.IconName} | {path} | {state}");
                        }

                        var label = FormatInstallProgress(state, downloaded, total);
                        if (string.IsNullOrWhiteSpace(label)) return;

                        long now = Environment.TickCount64;
                        if (now - lastTick < 800) return;
                        lastTick = now;
                        config.Text = label;
                        config.Refresh();
                    });

                    SharedRootManager.RecordBaseChannel(
                        effectiveIconName,
                        path,
                        "integrity-repair-completed");
                    LogHelper.Log(
                        $"Game repair completed: requested={_game.IconName} | " +
                        $"effective={effectiveIconName} | {path}");
                    config.OK(Localizer.GetRequiredString("App.Game.Repair.Success"));
                    _ = CheckGameStatusAsync();
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    LogHelper.LogError(ex, $"Game repair canceled: {_game.IconName} | {path}");
                    config.OK(Localizer.GetRequiredString("App.Game.Repair.Canceled"));
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex, $"Game repair failed: {_game.IconName} | {path}");
                    config.Error(ex.Message);
                }
                finally
                {
                    GameRepairManager.Complete(path);
                    _repairingPath = null;
                    if (_gameState == GameState.Repairing)
                        _gameState = previousState;

                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            if (!GameStart.IsDisposed) RefreshGameStartButton();
                        });
                    }
                }
            });
        }

        private static string FormatInstallProgress(InstallProgressState state, long downloaded, long total)
        {
            string label;
            if (state.HasFlag(InstallProgressState.Completed))
                label = Localizer.GetRequiredString("App.Game.Install.Completed");
            else if (state.HasFlag(InstallProgressState.Download))
                label = FormatDownloadProgress(downloaded, total);
            else if (state.HasFlag(InstallProgressState.Install))
                label = Localizer.GetRequiredString("App.Game.Install.Installing");
            else if (state.HasFlag(InstallProgressState.Updating))
                label = Localizer.GetRequiredString("App.Game.Install.Updating");
            else if (state.HasFlag(InstallProgressState.Verify))
                label = Localizer.GetRequiredString("App.Game.Install.Verifying");
            else if (state.HasFlag(InstallProgressState.Removing))
                label = Localizer.GetRequiredString("App.Game.Install.Removing");
            else
                return null;

            if ((state.HasFlag(InstallProgressState.Install) ||
                 state.HasFlag(InstallProgressState.Updating) ||
                 state.HasFlag(InstallProgressState.Verify)) &&
                total > 0)
            {
                label = FormatStageProgress(label, downloaded, total);
            }

            return label;
        }

        private void MarkGameReadyAfterInstall(string installPath)
        {
            _gameState = GameState.Ready;

            try
            {
                var cfg = ConfigHelper.Load();
                cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached);

                cfg.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = true,
                    HasUpdate = false,
                    HasPreload = false,
                    PreloadCompleted = false,
                    LocalVersion = cached?.RemoteVersion ?? cached?.LocalVersion ?? "",
                    RemoteVersion = cached?.RemoteVersion ?? "",
                    PreloadVersion = "",
                    InstallPath = installPath
                };

                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                if (entry != null)
                {
                    if (!string.IsNullOrEmpty(installPath))
                        LinkedClientPolicy.UpdatePath(cfg, entry, installPath);
                    if (!string.IsNullOrEmpty(cached?.RemoteVersion))
                        entry.LocalVersion = cached.RemoteVersion;
                }

                ConfigHelper.Save(cfg);
            }
            catch { }

            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() =>
                {
                    if (!GameStart.IsDisposed) RefreshGameStartButton();
                    RefreshGameInfoBadge();
                });
        }

        private static async Task UpdateServerPayloadAfterGameUpdateAsync(string iconName)
        {
            var profile = ServerPayloadUpdater.GetProfile(iconName);
            if (profile == null) return;

            try
            {
                await ServerPayloadUpdater.UpdateAsync(
                    profile,
                    force: false,
                    progress: null,
                    CancellationToken.None);
                LogHelper.Log(
                    $"Server payload auto update completed: {iconName}");
            }
            catch (Exception ex)
            {
                // The game itself is already updated. Keep it usable and leave the
                // manual updater as the recovery path when the CDN is unavailable.
                LogHelper.LogError(
                    ex, $"Server payload auto update failed: {iconName}");
            }
        }

        private static bool IsServerPayloadAutoUpdateEnabled(string iconName)
        {
            return ConfigHelper.Load()
                .ServerPayloadAutoUpdateProfiles
                .Contains(iconName, StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatDownloadProgress(long downloaded, long total)
        {
            if (total <= 0) return null;
            double pct   = (double)downloaded / total * 100;
            double dlMB  = downloaded / 1048576.0;
            double totMB = total / 1048576.0;
            return $"{dlMB:F1} / {totMB:F1} MB  ({pct:F0}%)";
        }

        private static string FormatStageProgress(string stage, long current, long total)
        {
            var progress = FormatDownloadProgress(current, total);
            return string.IsNullOrWhiteSpace(progress) ? stage : $"{stage} {progress}";
        }

        private string GetConfiguredGamePath()
        {
            try
            {
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                return entry?.RootPath ?? _game.RootPath;
            }
            catch
            {
                return _game.RootPath;
            }
        }

        private async void GameStart_Click(object sender, EventArgs e)
        {
            UpdateGameRunningState();
            if (_isGameRunning) return;
            if (GameStart.Loading) return;
            if (_gameState == GameState.Repairing || GameRepairManager.IsRepairing(GetConfiguredGamePath()))
            {
                _gameState = GameState.Repairing;
                RefreshGameStartButton();
                AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.Repair.UpdateRunning"));
                return;
            }

            if (_gameState == GameState.NotInstalled || _gameState == GameState.HasUpdate || _gameState == GameState.Paused)
            {
                InstallOrUpdateGame();
                return;
            }

            if (_gameState == GameState.Downloading)
            {
                if (_activeUpdate?.Cancel() == true)
                    _gameState = GameState.Paused;
                RefreshGameStartButton();
                return;
            }

            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            bool isEndfield = GameChannelCatalog.IsFamily(
                _game.IconName, GameFamily.Endfield);
            var targetChannel = GameChannelCatalog.Get(_game.IconName);

            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.Game.WarnSelectDir"));
                path = Helpers.DialogHelper.BrowseFolder(
                    _overview?.IsHandleCreated == true ? _overview.Handle : IntPtr.Zero,
                    Localizer.GetRequiredString("App.Game.SelectDirTitle").Replace("{0}", _game.GetLocalizedName()));
                if (path == null) return;
                string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
                var executableExists = File.Exists(Path.Combine(path, exeName));
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null)
                {
                    try
                    {
                        LinkedClientPolicy.UpdatePath(cfg2, e2, path);
                    }
                    catch (InvalidOperationException ex)
                    {
                        AntdUI.Message.warn(_overview, ex.Message);
                        return;
                    }

                    if (!executableExists)
                    {
                        e2.LocalVersion = "";
                        cfg2.GameStatusCache[_game.IconName] = new CachedGameStatus
                        {
                            IsInstalled = false,
                            HasUpdate = false,
                            HasPreload = false,
                            PreloadCompleted = false,
                            LocalVersion = "",
                            RemoteVersion = "",
                            PreloadVersion = "",
                            InstallPath = path
                        };
                    }

                    ConfigHelper.Save(cfg2);
                }

                _game.RootPath = path;
                if (!executableExists)
                {
                    _game.LocalVersion = "";
                    _preloadCompleted = false;
                    _gameState = GameState.NotInstalled;
                    RefreshGameStartButton();
                    RefreshGameInfoBadge();
                    return;
                }
            }

            SharedRootResolution sharedRootResolution;
            try
            {
                sharedRootResolution = SharedRootManager.ResolveAndPersist(
                    _game.IconName, path, detectBaseChannel: true);
                if (sharedRootResolution.Mode == SharedRootMode.Conflict)
                {
                    AntdUI.Message.warn(
                        _overview,
                        "同一游戏目录配置了不兼容的渠道。请为这些渠道设置不同目录后再启动。");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Resolve shared root for launch");
                AntdUI.Message.warn(_overview, ex.Message);
                return;
            }

            IDisposable launchOperationLease = null;
            if (targetChannel?.PayloadProfile != null &&
                !LinkedClientOperationCoordinator.TryAcquire(
                    _game.IconName, path, out launchOperationLease))
            {
                AntdUI.Message.warn(
                    _overview,
                    Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                return;
            }

            if (launchOperationLease != null)
            {
                try
                {
                    // Resolve from disk again after acquiring the Shared Root
                    // lease. This prevents a queued launch from acting on an old
                    // BaseChannel recorded before another channel switch.
                    sharedRootResolution = SharedRootManager.ResolveAndPersist(
                        _game.IconName, path, detectBaseChannel: true);
                    if (sharedRootResolution.Mode == SharedRootMode.Conflict)
                    {
                        launchOperationLease.Dispose();
                        AntdUI.Message.warn(
                            _overview,
                            "同一游戏目录配置了不兼容的渠道。请为这些渠道设置不同目录后再启动。");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    launchOperationLease.Dispose();
                    LogHelper.LogError(ex, "Re-resolve shared root for launch");
                    AntdUI.Message.warn(_overview, ex.Message);
                    return;
                }
            }

            if (LinkedClientPolicy.IsArknightsChannel(_game.IconName))
            {
                var launchConfig = ConfigHelper.Load();
                var launchEntry = LinkedClientPolicy.FindEntry(
                    launchConfig, _game.IconName, path);
                var markerExists = ArknightsLinkedClientService
                    .HasLinkedClientMarker(path);
                var groupExists = !string.IsNullOrWhiteSpace(
                    launchEntry?.LinkedClientGroupId);
                if (ArknightsLinkedClientService.IsPendingClient(
                        launchConfig, _game.IconName, path) ||
                    ArknightsLinkedClientService.IsPendingDetachClient(
                        launchConfig, _game.IconName, path) ||
                    markerExists != groupExists)
                {
                    launchOperationLease?.Dispose();
                    AntdUI.Message.warn(
                        _overview,
                        Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                    return;
                }
            }

            bool independentClient;
            string payloadDir;
            bool needSwitch;
            var useLinkedRuntime =
                cfg.UseHardLink &&
                sharedRootResolution.Mode == SharedRootMode.Shared;
            try
            {
                if (useLinkedRuntime)
                {
                    independentClient = false;
                    payloadDir = null;
                    needSwitch = false;
                }
                else
                {
                    independentClient = LinkedClientPolicy.ShouldSkipServerPayloadSwitch(
                        _game.IconName, path);
                    payloadDir = independentClient
                        ? null
                        : GameLauncher.GetPayloadDirPath(_game.IconName);
                    needSwitch = payloadDir != null && Directory.Exists(payloadDir);
                }
            }
            catch (Exception ex)
            {
                launchOperationLease?.Dispose();
                Helpers.LogHelper.LogError(ex, "GameStartPreparation");
                AntdUI.Message.error(_overview, ex.Message);
                return;
            }

            if (!useLinkedRuntime && needSwitch &&
                IsGameClientRunningAt(path, isEndfield))
            {
                launchOperationLease?.Dispose();
                AntdUI.Message.warn(_overview, CloseGameClientMessage);
                return;
            }

            GameStart.LoadingWaveValue = 0;
            GameStart.Loading = true;
            var callbackOperationLease = launchOperationLease;
            launchOperationLease = null;
            try
            {
                AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.Game.Loading"), async (config) =>
                {
                    try
                    {
                        if (_game.IconName == "Arknights")
                        {
                            string selectedAccountId = accountSelect.SelectedValue as string;
                            if (!string.IsNullOrEmpty(selectedAccountId))
                            {
                                config.Text = Localizer.GetRequiredString("App.Game.SwitchingAccount");
                                config.Refresh();
                                await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                            }
                        }
                        else if (_game.IconName == "Endfield")
                        {
                            string selectedAccountId = accountSelect.SelectedValue as string;
                            if (!string.IsNullOrEmpty(selectedAccountId))
                            {
                                config.Text = Localizer.GetRequiredString("App.Game.SwitchingAccount");
                                config.Refresh();
                                await Helpers.GameLauncher.RestoreEndfieldAccount(selectedAccountId);
                            }
                        }
                        else if (_game.IconName == "GlobalEndfield")
                        {
                            string selectedAccountId = accountSelect.SelectedValue as string;
                            if (!string.IsNullOrEmpty(selectedAccountId))
                            {
                                config.Text = Localizer.GetRequiredString("App.Game.SwitchingAccount");
                                config.Refresh();
                                await Helpers.GameLauncher.RestoreGlobalEndfieldAccount(selectedAccountId);
                            }
                        }
                        var launchPath = path;
                        var traditionalSwitchPerformed = false;
                        if (useLinkedRuntime)
                        {
                            var targetIsBase = sharedRootResolution.Base != null &&
                                               string.Equals(
                                                   sharedRootResolution.Base.Channel,
                                                   sharedRootResolution.Target.Channel,
                                                   StringComparison.OrdinalIgnoreCase);
                            if (targetIsBase)
                            {
                                LogHelper.Log(
                                    $"Shared Root direct launch: GameId={sharedRootResolution.GameId} | " +
                                    $"SharedRoot={path} | BaseChannel={sharedRootResolution.Base.Channel} | " +
                                    $"TargetChannel={sharedRootResolution.Target.Channel}");
                            }
                            else
                            {
                                try
                                {
                                    if (sharedRootResolution.Base == null)
                                        throw new InvalidDataException(
                                            "无法识别 Shared Root 当前 BaseChannel。");

                                    var runtime = await LinkedRuntimeService
                                        .EnsureLinkedRuntimeAsync(
                                            sharedRootResolution,
                                            message =>
                                            {
                                                config.Text = message;
                                                config.Refresh();
                                            },
                                            operationAlreadyCoordinated:
                                                callbackOperationLease != null);
                                    launchPath = runtime.RuntimePath;
                                }
                                catch (Exception runtimeError)
                                {
                                    LogHelper.LogError(
                                        runtimeError,
                                        $"Linked Runtime fallback | " +
                                        $"GameId={sharedRootResolution.GameId} | " +
                                        $"SharedRoot={path} | " +
                                        $"BaseChannel={sharedRootResolution.Base?.Channel ?? "Unknown"} | " +
                                        $"TargetChannel={sharedRootResolution.Target.Channel}");
                                    config.Text = Localizer.GetRequiredString("App.LinkedRuntime.Fallback");
                                    config.Refresh();

                                    if (IsGameClientRunningAt(path, isEndfield))
                                        throw new InvalidOperationException(
                                            CloseGameClientMessage);
                                    await GameLauncher.SwitchServerWithResult(
                                        path,
                                        _game.IconName,
                                        message =>
                                        {
                                            config.Text = message;
                                            config.Refresh();
                                        },
                                        isEndfield,
                                        _ => { },
                                        operationAlreadyCoordinated:
                                            callbackOperationLease != null);
                                    traditionalSwitchPerformed = true;
                                    launchPath = path;
                                }
                            }
                        }
                        else if (needSwitch)
                        {
                            if (IsGameClientRunningAt(path, isEndfield))
                                throw new InvalidOperationException(
                                    CloseGameClientMessage);
                            await GameLauncher.SwitchServerWithResult(path, _game.IconName, msg =>
                            {
                                config.Text = msg;
                                config.Refresh();
                            }, isEndfield, _ => { },
                                operationAlreadyCoordinated:
                                    callbackOperationLease != null);
                            traditionalSwitchPerformed = true;
                        }
                        // Run the loading wave for a random 1-3 seconds.
                        if (traditionalSwitchPerformed && _game.IconName == "Arknights")
                        {
                            string selectedAccountId = accountSelect.SelectedValue as string;
                            if (!string.IsNullOrEmpty(selectedAccountId))
                            {
                                config.Text = Localizer.GetRequiredString("App.Game.SwitchingAccount");
                                config.Refresh();
                                await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                            }
                        }

                        var rng = new Random();
                        int totalMs = rng.Next(1000, 3001);
                        int steps = 100;
                        int stepMs = totalMs / steps;
                        for (int i = 0; i <= steps; i++)
                        {
                            GameStart.LoadingWaveValue = i / (float)steps;
                            await Task.Delay(stepMs);
                        }

                        GameLauncher.StartArknights(launchPath, _game.IconName);

                    // Show launch success only after the game process is detected.
                    config.Text = Localizer.GetRequiredString("App.Game.WaitingProcess");
                    config.Refresh();
                    Process gameProc = null;
                    for (int i = 0; i < 30 && gameProc == null; i++)
                    {
                        gameProc = GameLauncher.FindRunningGameProcess(
                            launchPath, isEndfield);
                        if (gameProc == null) await Task.Delay(1000);
                    }
                    config.OK(Localizer.GetRequiredString("App.Game.LaunchSuccess"));
                    var latestCfg = ConfigHelper.Load();
                    if (latestCfg.CloseAfterLaunch)
                    {
                        _overview.Invoke(new Action(() => Application.Exit()));
                    }
                    else if (latestCfg.HideToTrayOnLaunch)
                    {
                        _overview.Invoke(new Action(() => _overview.HideToTray()));
                        var overviewRef = _overview;
                        var capturedProc = gameProc;
                        _ = System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                if (capturedProc != null)
                                {
                                    try
                                    {
                                        capturedProc.EnableRaisingEvents = true;
                                        capturedProc.WaitForExit();
                                    }
                                    catch
                                    {
                                        // Fall back to polling when process exit events cannot be monitored.
                                        while (!capturedProc.HasExited)
                                            System.Threading.Thread.Sleep(3000);
                                    }
                                }
                            }
                            catch { }
                            finally
                            {
                                overviewRef.ShowFromTray();
                            }
                        });
                    }
                    else
                    {
                        _overview.Invoke(new Action(() =>
                            _overview.WindowState = FormWindowState.Minimized));
                    }
                    }
                    catch (Exception ex)
                    {
                        Helpers.LogHelper.LogError(ex, "GameStart");
                        config.Error(ex.Message);
                    }
                    finally
                    {
                        callbackOperationLease?.Dispose();
                        if (!GameStart.IsDisposed)
                        {
                            try
                            {
                                _overview.Invoke(new Action(() =>
                                {
                                    if (!GameStart.IsDisposed)
                                        GameStart.Loading = false;
                                }));
                            }
                            catch
                            {
                                if (!GameStart.IsDisposed)
                                    GameStart.Loading = false;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                callbackOperationLease?.Dispose();
                if (!GameStart.IsDisposed) GameStart.Loading = false;
                Helpers.LogHelper.LogError(ex, "GameStartLoadingDialog");
                AntdUI.Message.error(_overview, ex.Message);
            }
        }
    }
}
