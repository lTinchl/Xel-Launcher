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

                if (GameUpdateManager.IsRecentlyCompleted(_game.IconName, path))
                {
                    _gameState = GameState.Ready;
                    var completedCfg = ConfigHelper.Load();
                    completedCfg.GameStatusCache[_game.IconName] = new CachedGameStatus
                    {
                        IsInstalled = true,
                        HasUpdate = false,
                        LocalVersion = entry?.LocalVersion ?? "",
                        RemoteVersion = entry?.LocalVersion ?? "",
                        InstallPath = path
                    };
                    ConfigHelper.Save(completedCfg);
                    if (IsHandleCreated)
                        BeginInvoke(() => RefreshGameStartButton());
                    return true;
                }

                var status = await _service.CheckStatusAsync(path);
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

                _gameState = !status.IsInstalled ? GameState.NotInstalled
                           : hasUpdate           ? GameState.HasUpdate
                                                 : GameState.Ready;

                // Write cache after live check completes
                var cfgToUpdate = ConfigHelper.Load();
                cfgToUpdate.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = status.IsInstalled,
                    HasUpdate = hasUpdate,
                    LocalVersion = status.LocalVersion ?? "",
                    RemoteVersion = status.RemoteVersion ?? "",
                    InstallPath = path
                };
                var entryToUpdate = cfgToUpdate.Games.Find(g => g.IconName == _game.IconName);
                if (entryToUpdate != null && !string.IsNullOrEmpty(status.LocalVersion))
                    entryToUpdate.LocalVersion = status.LocalVersion;
                ConfigHelper.Save(cfgToUpdate);

                if (IsHandleCreated)
                    BeginInvoke(() => RefreshGameStartButton());

                return true;
            }
            catch { return false; }
        }

        private void RefreshGameStartButton()
        {
            GameStart.Enabled = true;
            if (GameRepairManager.IsRepairing(GetConfiguredGamePath()))
                _gameState = GameState.Repairing;

            switch (_gameState)
            {
                case GameState.NotInstalled:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Install", "安装游戏");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                case GameState.HasUpdate:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Update", "更新游戏");
                    GameStart.IconSvg = "SyncOutlined";
                    break;
                case GameState.Downloading:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Pause", "暂停");
                    GameStart.IconSvg = "PauseOutlined";
                    break;
                case GameState.Paused:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Resume", "继续");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                case GameState.Repairing:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Repair.Running", "校验中...");
                    GameStart.IconSvg = "SafetyCertificateOutlined";
                    GameStart.Enabled = false;
                    break;
                default:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Start", "开始游戏");
                    GameStart.IconSvg = "PoweroffOutlined";
                    break;
            }
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
                    AntdUI.Localization.Get("App.Game.SelectInstallDir", "选择游戏安装目录"));
                if (path == null) return;
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null) { e2.RootPath = path; ConfigHelper.Save(cfg2); }
            }

            _gameState = GameState.Downloading;
            RefreshGameStartButton();

            var capturedPath = path;
            var update = GameUpdateManager.StartOrAttach(_game.IconName, capturedPath, out var started);
            _activeUpdate = update;
            RefreshGameStartButton();
            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Install.Init", "初始化..."), async config =>
            {
                long lastTick = 0;
                InstallProgressState? lastLoggedState = null;
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
                        config.Text = AntdUI.Localization.Get("App.Game.Install.Updating", "更新中...");
                        config.Refresh();
                    }

                    await update.Task;
                    if (update.IsCancellationRequested)
                    {
                        _gameState = GameState.Paused;
                        config.OK(AntdUI.Localization.Get("App.Game.Install.Paused", "已暂停"));
                    }
                    else
                    {
                        MarkGameReadyAfterInstall(capturedPath);
                        config.OK(AntdUI.Localization.Get("App.Game.Install.Success", "安装/更新完成"));
                    }
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    _gameState = GameState.Paused;
                    LogHelper.LogError(ex, $"Game update paused in UI: {_game.IconName} | {capturedPath}");
                    config.OK(AntdUI.Localization.Get("App.Game.Install.Paused", "已暂停"));
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
                AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.Game.WarnSelectDir", "请先选择游戏根目录"));
                return;
            }

            if (GameUpdateManager.Find(path) != null)
            {
                AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.Game.Repair.UpdateRunning", "游戏正在更新中，无法同时校验"));
                return;
            }

            if (_gameState == GameState.Repairing || IsSameInstallPath(_repairingPath, path) || !GameRepairManager.TryStart(path))
            {
                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.Repair.AlreadyRunning", "游戏完整性校验正在进行中"));
                return;
            }

            var previousState = _gameState;
            _repairingPath = path;
            _gameState = GameState.Repairing;
            RefreshGameStartButton();

            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Repair.Init", "准备校验..."), async config =>
            {
                long lastTick = 0;
                InstallProgressState? lastLoggedState = null;

                try
                {
                    using var service = new EndfieldService(_game.IconName);
                    LogHelper.Log($"Game repair started: {_game.IconName} | {path}");
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

                    LogHelper.Log($"Game repair completed: {_game.IconName} | {path}");
                    config.OK(AntdUI.Localization.Get("App.Game.Repair.Success", "游戏完整性校验完成"));
                    _ = CheckGameStatusAsync();
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    LogHelper.LogError(ex, $"Game repair canceled: {_game.IconName} | {path}");
                    config.OK(AntdUI.Localization.Get("App.Game.Repair.Canceled", "校验已取消"));
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
                label = AntdUI.Localization.Get("App.Game.Install.Completed", "完成");
            else if (state.HasFlag(InstallProgressState.Download))
                label = FormatDownloadProgress(downloaded, total);
            else if (state.HasFlag(InstallProgressState.Install))
                label = AntdUI.Localization.Get("App.Game.Install.Installing", "安装中...");
            else if (state.HasFlag(InstallProgressState.Updating))
                label = AntdUI.Localization.Get("App.Game.Install.Updating", "更新中...");
            else if (state.HasFlag(InstallProgressState.Verify))
                label = AntdUI.Localization.Get("App.Game.Install.Verifying", "校验中...");
            else if (state.HasFlag(InstallProgressState.Removing))
                label = AntdUI.Localization.Get("App.Game.Install.Removing", "清理中...");
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
                    LocalVersion = cached?.RemoteVersion ?? cached?.LocalVersion ?? "",
                    RemoteVersion = cached?.RemoteVersion ?? "",
                    InstallPath = installPath
                };

                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                if (entry != null)
                {
                    if (!string.IsNullOrEmpty(installPath))
                        entry.RootPath = installPath;
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
                });
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
            if (GameStart.Loading) return;
            if (_gameState == GameState.Repairing || GameRepairManager.IsRepairing(GetConfiguredGamePath()))
            {
                _gameState = GameState.Repairing;
                RefreshGameStartButton();
                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.Repair.UpdateRunning", "游戏正在校验中，无法启动"));
                return;
            }

            if (_gameState == GameState.NotInstalled || _gameState == GameState.HasUpdate || _gameState == GameState.Paused)
            {
                InstallOrUpdateGame();
                return;
            }

            if (_gameState == GameState.Downloading)
            {
                _activeUpdate?.Cancel();
                _gameState = GameState.Paused;
                RefreshGameStartButton();
                return;
            }

            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            bool isEndfield = _game.IconName == "Endfield" || _game.IconName == "BiliEndfield" || _game.IconName == "GlobalEndfield" || _game.IconName == "PlayEndfield";
            string payloadDir = GameLauncher.GetPayloadDirPath(_game.IconName);
            bool needSwitch = payloadDir != null && Directory.Exists(payloadDir);

            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.Game.WarnSelectDir", "请先选择游戏根目录"));
                path = Helpers.DialogHelper.BrowseFolder(
                    _overview?.IsHandleCreated == true ? _overview.Handle : IntPtr.Zero,
                    AntdUI.Localization.Get("App.Game.SelectDirTitle", "选择「{0}」游戏根目录").Replace("{0}", _game.GetLocalizedName()));
                if (path == null) return;
                string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
                if (!File.Exists(Path.Combine(path, exeName)))
                {
                    AntdUI.Message.error(_overview, string.Format(AntdUI.Localization.Get("App.Game.ExeNotFound", "所选目录中未找到 {0}"), exeName));
                    return;
                }
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null) { e2.RootPath = path; ConfigHelper.Save(cfg2); }
            }

            await GameLauncher.KillArknightsProcesses(isEndfield);

            GameStart.LoadingWaveValue = 0;
            GameStart.Loading = true;
            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Loading", "加载中..."), async (config) =>
            {
                try
                {
                    if (_game.IconName == "Arknights")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                        }
                    }
                    else if (_game.IconName == "Endfield")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreEndfieldAccount(selectedAccountId);
                        }
                    }
                    else if (_game.IconName == "GlobalEndfield")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreGlobalEndfieldAccount(selectedAccountId);
                        }
                    }
                    if (needSwitch)
                    {
                        bool usedHardLink = false;
                        await GameLauncher.SwitchServerWithResult(path, _game.IconName, msg =>
                        {
                            config.Text = msg;
                            config.Refresh();
                        }, isEndfield, result => usedHardLink = result);

                        if (!usedHardLink)
                        {
                            _overview.BeginInvoke(new Action(() =>
                                AntdUI.Message.info(_overview,
                                    AntdUI.Localization.Get("App.Game.HardLinkTip",
                                        "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"))
                            ));
                        }
                    }
                    // Run the loading wave for a random 1-3 seconds.
                    if (needSwitch && _game.IconName == "Arknights")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "Switching account...");
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

                    GameLauncher.StartArknights(path, _game.IconName);

                    // Show launch success only after the game process is detected.
                    string procName = (isEndfield) ? "Endfield" : "Arknights";
                    config.Text = AntdUI.Localization.Get("App.Game.WaitingProcess", "等待游戏进程...");
                    config.Refresh();
                    System.Diagnostics.Process gameProc = null;
                    for (int i = 0; i < 30 && gameProc == null; i++)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(procName);
                        if (procs.Length > 0) gameProc = procs[0];
                        else await Task.Delay(1000);
                    }
                    config.OK(AntdUI.Localization.Get("App.Game.LaunchSuccess", "游戏启动成功"));
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
                        System.Threading.Tasks.Task.Run(() =>
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
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.LogError(ex, "GameStart");
                    config.Error(ex.Message);
                }
                if (!GameStart.IsDisposed)
                {
                    try
                    {
                        _overview.Invoke(new Action(() => { if (!GameStart.IsDisposed) GameStart.Loading = false; }));
                    }
                    catch
                    {
                        if (!GameStart.IsDisposed) GameStart.Loading = false;
                    }
                }
            });
        }
    }
}
