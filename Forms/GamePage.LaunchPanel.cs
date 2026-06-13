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
            tooltip.SetTip(btnArknightsWiki, AntdUI.Localization.Get("App.Game.Toolbox", "小工具"));
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
            TopTooltip().SetTip(btnAccountManage, AntdUI.Localization.Get("App.Game.AccountManage", "账号管理"));
            btnAccountManage.Click += btnAccountManage_Click;

            accountSelect = new AntdUI.Select
            {
                Location = new Point(56, 0),
                Size = new Size(164, 52),
                Radius = 24,
                BorderWidth = 1F,
                PlaceholderText = AntdUI.Localization.Get("App.Game.SelectAccount", "  选择账号"),
                Font = new Font("Microsoft YaHei UI", 11F),
                DropDownRadius = 8,
                Placement = AntdUI.TAlignFrom.TL,
            };

            GameStart = new AntdUI.Button
            {
                BackExtend = "135, #6253E1, #04BEFE",
                IconSvg = "PoweroffOutlined",
                Text = AntdUI.Localization.Get("App.Game.Start", "开始游戏"),
                Location = new Point(224, 0),
                Size = new Size(168, 52),
                BorderWidth = 0,
                Radius = 24,
                WaveSize = 4,
                LoadingWaveColor = Color.FromArgb(60, 255, 255, 255),
                Type = AntdUI.TTypeMini.Primary,
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            };
            GameStart.Click += GameStart_Click;

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
                DropDownArrow = false,
                DropDownRadius = 8,
            };

            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Sign", "森空岛签到"), "sign").SetIcon(LoadMenuIcon("Skland_Sign.ico")));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "sign")
                {
                    BeginInvoke(() =>
                    {
                        ResetFloatMenuVisualState();
                        AntdUI.Modal.open(new AntdUI.Modal.Config(_overview, new SignHubForm(_overview, 0))
                        {
                            OkText = null,
                            CancelText = null,
                            BtnHeight = 0,
                            MaskClosable = true,
                        });
                    });
                }
            };

            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Setting", "游戏设置"), "setting").SetIcon("SettingOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "setting")
                {
                    BeginInvoke(() =>
                    {
                        ResetFloatMenuVisualState();
                        var drawer = AntdUI.Drawer.open(_overview, new GameSettingForm(_game, _overview, UpdateAccountControlsVisibility, () => _ = CheckGameStatusAsync(), this), AntdUI.TAlignMini.Right);
                        if (drawer != null)
                            drawer.Disposed += (sender, args) => BeginInvokeResetFloatMenuVisualState();
                    });
                }
            };
            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Repair", "校验游戏完整性"), "repair").SetIcon("SafetyCertificateOutlined"));
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

            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Deledwonload", "清理下载缓存"), "Deledwonload").SetIcon("DeleteOutlined"));
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
                                AntdUI.Message.success(_overview, AntdUI.Localization.Get("App.Game.ClearCacheSuccess", "下载缓存已清理"));
                            }
                            catch (Exception ex)
                            {
                                AntdUI.Message.error(_overview, string.Format(AntdUI.Localization.Get("App.Game.ClearCacheFailed", "清理下载缓存失败: {0}"), ex.Message));
                            }
                        }
                        else
                        {
                            AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.NoCache", "未找到下载缓存"));
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
            panelLaunch.Controls.Add(GameStart);
            panelLaunch.Controls.Add(floatMenu);

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
