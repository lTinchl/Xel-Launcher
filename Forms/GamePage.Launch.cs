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
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                string path = entry?.RootPath ?? _game.RootPath;
                if (string.IsNullOrEmpty(path)) return false;

                var status = await _service.CheckStatusAsync(path);
                if (status == null) return false;

                var hasUpdate = status.HasUpdate &&
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
                    RemoteVersion = status.RemoteVersion ?? ""
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

            _downloadCts?.Dispose();
            _downloadCts = new CancellationTokenSource();
            _gameState = GameState.Downloading;
            RefreshGameStartButton();

            var capturedPath = path;
            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Install.Init", "初始化..."), async config =>
            {
                long lastTick = 0;
                try
                {
                    await _service.InstallOrUpdateAsync(capturedPath, (state, downloaded, total) =>
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
                            return;

                        if ((state.HasFlag(InstallProgressState.Install) ||
                             state.HasFlag(InstallProgressState.Updating) ||
                             state.HasFlag(InstallProgressState.Verify)) &&
                            total > 0)
                        {
                            label = FormatStageProgress(label, downloaded, total);
                        }

                        long now = Environment.TickCount64;
                        if (now - lastTick < 800) return;
                        lastTick = now;
                        config.Text = label;
                        config.Refresh();
                    }, _downloadCts.Token);

                    MarkGameReadyAfterInstall(capturedPath);
                    await CheckGameStatusAsync();
                    config.OK(AntdUI.Localization.Get("App.Game.Install.Success", "安装/更新完成"));
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    _gameState = GameState.Paused;
                    config.OK(AntdUI.Localization.Get("App.Game.Install.Paused", "已暂停"));
                }
                catch (Exception ex)
                {
                    _gameState = GameState.Unknown;
                    _ = CheckGameStatusAsync();
                    config.Error(ex.Message);
                }
                finally
                {
                    BeginInvoke(() =>
                    {
                        if (!GameStart.IsDisposed) RefreshGameStartButton();
                    });
                }
            });
        }

        private static bool IsCancellation(Exception ex) =>
            ex is OperationCanceledException ||
            (ex is AggregateException aex && aex.InnerExceptions.Count > 0 &&
             aex.InnerExceptions[0] is OperationCanceledException);

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
                    RemoteVersion = cached?.RemoteVersion ?? ""
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

        private async void GameStart_Click(object sender, EventArgs e)
        {
            if (GameStart.Loading) return;

            if (_gameState == GameState.NotInstalled || _gameState == GameState.HasUpdate || _gameState == GameState.Paused)
            {
                InstallOrUpdateGame();
                return;
            }

            if (_gameState == GameState.Downloading)
            {
                _downloadCts?.Cancel();
                return;
            }

            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            var official = cfg.Games.Find(g => g.IconName == "Arknights");
            var bilibili = cfg.Games.Find(g => g.IconName == "BiliArknights");
            bool sameRoot = official != null && bilibili != null &&
                !string.IsNullOrEmpty(official.RootPath) && !string.IsNullOrEmpty(bilibili.RootPath) &&
                Path.GetFullPath(official.RootPath).Equals(
                    Path.GetFullPath(bilibili.RootPath),
                    StringComparison.OrdinalIgnoreCase);

            // Replace Endfield launch metadata when this game shares a root path with another Endfield variant.
            bool isEndfield = _game.IconName == "Endfield" || _game.IconName == "BiliEndfield" || _game.IconName == "GlobalEndfield" || _game.IconName == "PlayEndfield";
            bool endfieldSameRoot = false;
            if (isEndfield && !string.IsNullOrEmpty(path))
            {
                var endfieldIcons = new[] { "Endfield", "BiliEndfield", "GlobalEndfield","PlayEndfield" };
                foreach (var other in endfieldIcons)
                {
                    if (other == _game.IconName) continue;
                    var otherEntry = cfg.Games.Find(g => g.IconName == other);
                    if (otherEntry != null && !string.IsNullOrEmpty(otherEntry.RootPath) &&
                        Path.GetFullPath(path).Equals(Path.GetFullPath(otherEntry.RootPath), StringComparison.OrdinalIgnoreCase))
                    {
                        endfieldSameRoot = true;
                        break;
                    }
                }
            }

            bool needSwitch = isEndfield ? endfieldSameRoot : sameRoot;

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

                // Recalculate shared-root state after updating the game path.
                var cfg3 = ConfigHelper.Load();
                official = cfg3.Games.Find(g => g.IconName == "Arknights");
                bilibili = cfg3.Games.Find(g => g.IconName == "BiliArknights");
                sameRoot = official != null && bilibili != null &&
                    !string.IsNullOrEmpty(official.RootPath) && !string.IsNullOrEmpty(bilibili.RootPath) &&
                    Path.GetFullPath(official.RootPath).Equals(Path.GetFullPath(bilibili.RootPath), StringComparison.OrdinalIgnoreCase);

                endfieldSameRoot = false;
                if (isEndfield)
                {
                    var endfieldIcons2 = new[] { "Endfield", "BiliEndfield", "GlobalEndfield", "PlayEndfield" };
                    foreach (var other in endfieldIcons2)
                    {
                        if (other == _game.IconName) continue;
                        var otherEntry = cfg3.Games.Find(g => g.IconName == other);
                        if (otherEntry != null && !string.IsNullOrEmpty(otherEntry.RootPath) &&
                            Path.GetFullPath(path).Equals(Path.GetFullPath(otherEntry.RootPath), StringComparison.OrdinalIgnoreCase))
                        {
                            endfieldSameRoot = true;
                            break;
                        }
                    }
                }

                needSwitch = isEndfield ? endfieldSameRoot : sameRoot;
            }

            if (needSwitch)
            {
                await GameLauncher.KillArknightsProcesses(isEndfield);
            }

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
