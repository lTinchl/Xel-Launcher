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
        private void BuildLaunchPanel()
        {
            btnArknightsWiki = new AntdUI.Button
            {
                IconSvg = "PlusOutlined",
                Type = AntdUI.TTypeMini.Default,
                BackColor = AntdUI.Config.IsDark ? AppTheme.DarkSurfaceActive : Color.White,
                Size = new Size(52, 52),
                Location = new Point(0, 0),
                BorderWidth = 0,
                Radius = 26,
                WaveSize = 4,
            };

            var tooltip = new AntdUI.TooltipComponent();
            tooltip.SetTip(btnArknightsWiki, Localizer.GetRequiredString("App.Game.Toolbox"));
            btnArknightsWiki.Click += btnArknightsWiki_Click;

            btnAccountManage = new AntdUI.Button
            {
                IconSvg = "UserOutlined",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(44, 44),
                Location = new Point(4, 4),
                BorderWidth = 0,
                Radius = 22,
                WaveSize = 0,
            };
            TopTooltip().SetTip(btnAccountManage, Localizer.GetRequiredString("App.Game.AccountManage"));
            btnAccountManage.Click += btnAccountManage_Click;

            accountSelect = new AntdUI.Select
            {
                Location = new Point(56, 0),
                Size = new Size(164, 52),
                Radius = 24,
                BorderWidth = 1F,
                PlaceholderText = Localizer.GetRequiredString("App.Game.SelectAccount"),
                Font = new Font("Microsoft YaHei UI", 11F),
                DropDownRadius = 8,
                Placement = AntdUI.TAlignFrom.TL,
                ColorScheme = AntdUI.TAMode.Dark,
            };

            btnPreload = new AntdUI.Button
            {
                IconSvg = "CloudDownloadOutlined",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(52, 52),
                Location = new Point(224, 0),
                BorderWidth = 0,
                Radius = 26,
                WaveSize = 4,
                Visible = false,
            };
            TopTooltip().SetTip(btnPreload, Localizer.GetRequiredString("App.Game.Preload"));
            btnPreload.Click += (s, e) => PreloadGame();

            GameStart = new GameLaunchButton
            {
                BackExtend = "135, #6253E1, #04BEFE",
                IconSvg = "PoweroffOutlined",
                Text = Localizer.GetRequiredString("App.Game.Start"),
                Location = new Point(224, 0),
                Size = new Size(164, 52),
                BorderWidth = 0,
                Radius = 24,
                WaveSize = 0,
                Padding = new Padding(4),
                LoadingWaveColor = Color.FromArgb(60, 255, 255, 255),
                Type = AntdUI.TTypeMini.Primary,
                Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
                TabStop = false,
            };
            GameStart.Click += GameStart_Click;
            GameStart.GotFocus += (s, e) => _overview.Focus();
            GameStart.MouseUp += (s, e) => _overview.Focus();

            floatMenu = new AntdUI.Dropdown
            {
                BackExtend = "135, #04BEFE, #3a7bd5",
                IconSvg = "MenuOutlined",
                Location = new Point(400, 2),
                Size = new Size(48, 48),
                BorderWidth = 0,
                Radius = 24,
                WaveSize = 0,
                Type = AntdUI.TTypeMini.Primary,
                Placement = AntdUI.TAlignFrom.TR,
                MaxCount = 5,
                DropDownArrow = false,
                DropDownRadius = 8,
                ColorScheme = AntdUI.TAMode.Dark,
            };

            AcrylicPopupHelper.Attach(accountSelect);
            AcrylicPopupHelper.Attach(floatMenu);

            floatMenu.Items.Add(new AntdUI.SelectItem(Localizer.GetRequiredString("App.Game.Setting"), "setting").SetIcon("SettingOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "setting")
                {
                    BeginInvoke(() =>
                    {
                        var drawer = AntdUI.Drawer.open(_overview, new GameSettingForm(_game, _overview, UpdateAccountControlsVisibility, () => _ = CheckGameStatusAsync(), this), AntdUI.TAlignMini.Right);
                        if (drawer != null)
                            drawer.Disposed += (sender, args) => BeginInvokeResetFloatMenuVisualState();
                        else
                            ResetFloatMenuVisualState();
                    });
                }
            };
            floatMenu.Items.Add(new AntdUI.SelectItem(Localizer.GetRequiredString("App.Game.Repair"), "repair").SetIcon("SafetyCertificateOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "repair")
                {
                    BeginInvoke(() =>
                    {
                        ResetFloatMenuVisualState();
                        RepairGameIntegrity();
                    });
                }
            };

            floatMenu.Items.Add(new AntdUI.SelectItem(
                Localizer.GetRequiredString("App.Game.PayloadUpdate"),
                "payloadUpdate").SetIcon("CloudSyncOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "payloadUpdate")
                {
                    BeginInvoke(() =>
                    {
                        ResetFloatMenuVisualState();
                        var content = new ServerPayloadUpdateForm(_overview);
                        AntdUI.Modal.open(new AntdUI.Modal.Config(
                            _overview,
                            Localizer.GetRequiredString("App.PayloadUpdate.Title"),
                            content)
                        {
                            OkText = null,
                            CancelText = null,
                            BtnHeight = 0,
                            MaskClosable = false,
                            Keyboard = false,
                        });
                    });
                }
            };

            floatMenu.Items.Add(new AntdUI.SelectItem(Localizer.GetRequiredString("App.Game.Deledwonload"), "Deledwonload").SetIcon("DeleteOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "Deledwonload")
                {
                    BeginInvoke(() =>
                    {
                        ResetFloatMenuVisualState();
                        var cfg = ConfigHelper.Load();
                        var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                        string path = entry?.RootPath ?? _game.RootPath;
                        string cachePath = Path.Combine(path, "Diffs");
                        if (Directory.Exists(cachePath))
                        {
                            try
                            {
                                Directory.Delete(cachePath, true);
                                ResetInstallStateAfterDownloadCacheClear(path);
                                AntdUI.Message.success(_overview, Localizer.GetRequiredString("App.Game.ClearCacheSuccess"));
                            }
                            catch (Exception ex)
                            {
                                AntdUI.Message.error(_overview, string.Format(Localizer.GetRequiredString("App.Game.ClearCacheFailed"), ex.Message));
                            }
                        }
                        else
                        {
                            ResetInstallStateAfterDownloadCacheClear(path);
                            AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.Game.NoCache"));
                        }
                    });
                }
            };

            panelLaunch = new AntdUI.Panel
            {
                Radius = 26,
                Shadow = 0,
                ShadowOpacity = 0F,
                BackColor = Color.Transparent,
                Size = new Size(448, 52),
                Anchor = AnchorStyles.None,
            };

            panelLaunch.Controls.Add(btnAccountManage);
            panelLaunch.Controls.Add(accountSelect);
            panelLaunch.Controls.Add(btnPreload);
            panelLaunch.Controls.Add(GameStart);
            panelLaunch.Controls.Add(floatMenu);

            DpiChangedAfterParent += (s, e) => ApplyLaunchPanelLayout();

        }

        private void RefreshPreloadButton()
        {
            if (btnPreload == null || btnPreload.IsDisposed) return;

            var showPreload = _preloadRunning || _gameState == GameState.HasPreload;
            btnPreload.Visible = showPreload;
            btnPreload.Enabled = !_preloadRunning;
            btnPreload.Loading = _preloadRunning;
            btnPreload.IconSvg = _preloadCompleted && !_preloadRunning ? "CheckCircleOutlined" : "CloudDownloadOutlined";
            var tooltipKey = _preloadCompleted && !_preloadRunning
                ? "App.Game.Preload.Success"
                : "App.Game.Preload";
            TopTooltip().SetTip(btnPreload, Localizer.GetRequiredString(tooltipKey));
            ApplyLaunchPanelLayout();
        }

        private void ApplyLaunchPanelLayout()
        {
            if (panelLaunch == null || GameStart == null || floatMenu == null) return;

            var hasAccounts = btnAccountManage?.Visible == true && accountSelect?.Visible == true;
            var hasPreload = btnPreload?.Visible == true;
            var x = 0;
            var compactGap = ScaleLaunchPanelValue(4);
            var regularGap = ScaleLaunchPanelValue(8);

            if (hasAccounts)
            {
                btnAccountManage.Location = new Point(compactGap, compactGap);
                accountSelect.Location = new Point(btnAccountManage.Right + regularGap, 0);
                x = accountSelect.Right + compactGap;
            }

            if (hasPreload)
            {
                btnPreload.Location = new Point(x, 0);
                x = btnPreload.Right + compactGap;
            }

            GameStart.Location = new Point(x, 0);
            x = GameStart.Right + regularGap;

            floatMenu.Location = new Point(x, ScaleLaunchPanelValue(2));
            x = floatMenu.Right;

            panelLaunch.Width = x;
            PositionLaunchPanel();
            PositionNoticePanel();
        }

        private int ScaleLaunchPanelValue(int value)
        {
            var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            return Math.Max(1, (int)Math.Round(value * dpi / 96F));
        }

        private void ResetInstallStateAfterDownloadCacheClear(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            GameUpdateManager.ClearPaused(path);
            if (_gameState == GameState.Paused || _gameState == GameState.Downloading)
            {
                _activeUpdate = null;
                _gameState = GameState.NotInstalled;
                RefreshGameStartButton();
            }

            _ = CheckGameStatusAsync();
        }

        private void ResetFloatMenuVisualState()
        {
            if (floatMenu == null || floatMenu.IsDisposed) return;

            floatMenu.SelectedValue = null;
            floatMenu.ExtraMouseDown = false;
            if (floatMenu.Focused) _overview.Focus();
            floatMenu.Invalidate();
        }

        private void BeginInvokeResetFloatMenuVisualState()
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                BeginInvoke(ResetFloatMenuVisualState);
            }
            catch (InvalidOperationException)
            {
                // The game page may have been disposed while the drawer was closing.
            }
        }
    }
}
