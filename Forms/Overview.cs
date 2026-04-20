using AntdUI;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;


namespace XelLauncher.Forms
{
    public partial class Overview : AntdUI.Window
    {
        GameEntry _currentGame = null;
        readonly System.Collections.Generic.List<SidebarButton> _sidebarBtns = new();
        private NotifyIcon _trayIcon;
        private bool _minimizeToTray => ConfigHelper.Load().MinimizeToTray;
        private bool _forceClose = false;
        private GamePage _currentGamePage = null;

        // 侧边栏拖拽状态
        private SidebarButton _dragBtn = null;
        private System.Drawing.Point _dragStartPos;
        private bool _isDragging = false;

        public Overview(bool top)
        {
            InitializeComponent();
            this.Icon = Properties.Resources.icon;
            windowBar.Text = "Xel Launcher ";
            AntdUI.Config.DropDownMarginFurther = true;

            TopMost = top;
            var globals = new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Lang.Chinese", "中文"),"zh-CN"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Lang.English", "English"),"en-US")
            };
            btn_global.Items.AddRange(globals);
            btn_more.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.Help", "帮助"), "help").SetIcon("QuestionCircleOutlined"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.About", "关于"),"info").SetIcon("InfoCircleOutlined"),
                new AntdUI.SelectItem("Github","github").SetIcon("GithubOutlined"),
                new AntdUI.SelectItem("BiliBili","bilibili").SetIcon("BilibiliOutlined"),

            });
            btn_bgcolor.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.White", "纯白"),      "#FFFFFF"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Mint", "薄荷绿"),    "#F0F7F4"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Warm", "暖米色"),    "#FAF7F2"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Sky", "天空蓝"),    "#EFF6FB"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Custom", "自定义.."), "custom"),
            });
            var lang = AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) btn_global.SelectedValue = globals[1].Tag;
            else btn_global.SelectedValue = globals[0].Tag;

            RebuildSidebar();
            InitTrayIcon();
            RebuildFloatMenu();
            // 同步深色模式按钮状态
            btn_mode.Toggle = AntdUI.Config.IsDark;
            Load += (s, e) =>
            {
                if (!AntdUI.Config.IsDark)
                    ApplyBackgroundColor(ConfigHelper.Load().BackgroundColor);

                // 定位角标并异步检查更新
                PositionUpdateBadge();
                _ = CheckUpdateBadgeAsync();
            };
            windowBar.SizeChanged += (s, e) => PositionUpdateBadge();
        }

        /// <summary>
        /// 将角标定位到版本号文字右上角。
        /// AntdUI PageHeader 的布局：图标在左（约40px），标题在图标右侧垂直居中，
        /// SubText 在标题正下方（与标题左对齐），字体更小。
        /// 所以 SubText 的 X 起点 ≈ iconAndPad，与主标题同列。
        /// </summary>
        private void PositionUpdateBadge()
        {
            if (!IsHandleCreated) return;

            using var subFont = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            int iconAndPad = 134; // 图标区域宽度（含 padding）

            // SubText 左对齐于 iconAndPad，测量文字宽度确定右边界
            int subWidth = System.Windows.Forms.TextRenderer.MeasureText(
                windowBar.SubText ?? "", subFont).Width;

            // X：紧贴版本号文字右边缘，再退 4px 让角标压住末位数字
            int badgeX = iconAndPad + subWidth + 12;
            // Y：SubText 位于 windowBar 下半部分，约在 y=20 处，角标贴其顶部
            int badgeY = 8;

            updateBadge.Location = new System.Drawing.Point(badgeX, badgeY);
            updateBadge.BringToFront();
        }

        private async System.Threading.Tasks.Task CheckUpdateBadgeAsync()
        {
            try
            {
                var info = await UpdateHelper.CheckAsync();
                if (info == null) return;
                var currentVer = System.Windows.Forms.Application.ProductVersion;
                if (UpdateHelper.IsNewer(currentVer, info.LatestVersion))
                {
                    if (IsHandleCreated)
                        Invoke(() => updateBadge.Visible = true);
                }
            }
            catch { /* 静默失败 */ }
        }

        private void updateBadge_Click(object sender, EventArgs e)
        {
            OpenSettingOnUpdatePage();
        }

        private void OpenSettingOnUpdatePage()
        {
            var setting = new Setting(this);
            setting.NavigateToUpdate(); // 直接跳到 Update 页
            if (AntdUI.Modal.open(this, AntdUI.Localization.Get("Setting", "设置"), setting) == DialogResult.OK)
            {
                AntdUI.Config.Animation = setting.Animation;
                AntdUI.Config.ShadowEnabled = setting.ShadowEnabled;
                AntdUI.Config.ShowInWindow = setting.ShowInWindow;
                AntdUI.Config.ScrollBarHide = setting.ScrollBarHide;
                if (AntdUI.Config.TextRenderingHighQuality != setting.TextRenderingHighQuality)
                {
                    AntdUI.Config.TextRenderingHighQuality = setting.TextRenderingHighQuality;
                    Refresh();
                }
                var cfg = ConfigHelper.Load();
                cfg.MinimizeToTray = setting.MinimizeToTray;
                cfg.CloseAfterLaunch = setting.CloseAfterLaunch;
                cfg.HideToTrayOnLaunch = setting.HideToTrayOnLaunch;
                cfg.UseHardLink = setting.UseHardLink;
                cfg.UseExternalBrowser = setting.UseExternalBrowser;
                ConfigHelper.Save(cfg);
                Setting.ApplyStartWithWindows(setting.StartWithWindows);
            }
            RebuildGameButtons();
            RebuildSidebar();
        }


        public void RebuildFloatMenu()
        {
            var config = ConfigHelper.Load();
            if (_currentGame == null && config.Games.Count > 0)
                SelectGame(config.Games[0]);
        }

        public void RebuildGameButtons() { }

        public void RebuildSidebar()
        {
            panelSidebarItems.Controls.Clear();
            _sidebarBtns.Clear();

            var config = ConfigHelper.Load();
            foreach (var game in config.Games)
            {
                var g = game;
                var btn = new SidebarButton();
                btn.Width = 108;
                btn.Height = 72;
                btn.Margin = new Padding(2);
                btn.Cursor = Cursors.Hand;
                try
                {
                    var ico = GetSidebarIcon(g.IconName);
                    if (ico != null)
                    {
                        var src = ico.ToBitmap();
                        var dst = new System.Drawing.Bitmap(44, 44);
                        using var g2 = System.Drawing.Graphics.FromImage(dst);
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g2.DrawImage(src, 0, 0, 44, 44);
                        btn.GameIcon = g.IconName == "GlobalEndfield" ? ApplyRoundedCorners(dst, 10) : dst;
                    }
                }
                catch { }
                btn.Click += (s, e) => SelectGame(g);
                btn.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _dragBtn = btn;
                        _dragStartPos = e.Location;
                        _isDragging = false;
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                    AntdUI.ContextMenuStrip.open(btn, it =>
                    {
                        var cfg = ConfigHelper.Load();
                        cfg.Games.RemoveAll(x => x.RootPath == g.RootPath && x.Name == g.Name);
                        ConfigHelper.Save(cfg);
                        RebuildSidebar();
                        RebuildGameButtons();
                        RebuildFloatMenu();
                    }, new AntdUI.IContextMenuStripItem[]
                    {
                    new AntdUI.ContextMenuStripItem(AntdUI.Localization.Get("App.Sidebar.Delete", "删除")).SetIcon("DeleteOutlined"),
                    });
                    }
                };
                btn.MouseMove += SidebarBtn_MouseMove;
                btn.MouseUp += SidebarBtn_MouseUp;
                _sidebarBtns.Add(btn);
                panelSidebarItems.Controls.Add(btn);
            }
        }

        private void SidebarBtn_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragBtn == null || e.Button != MouseButtons.Left) return;
            if (!_isDragging)
            {
                if (Math.Abs(e.X - _dragStartPos.X) < 4 && Math.Abs(e.Y - _dragStartPos.Y) < 4) return;
                _isDragging = true;
            }
            // 将鼠标坐标转换到 panelSidebarItems 坐标系
            var posInPanel = panelSidebarItems.PointToClient(_dragBtn.PointToScreen(e.Location));
            int targetIndex = GetDropIndex(posInPanel.Y);
            int currentIndex = panelSidebarItems.Controls.IndexOf(_dragBtn);
            if (targetIndex != currentIndex && targetIndex >= 0 && targetIndex < panelSidebarItems.Controls.Count)
            {
                panelSidebarItems.Controls.SetChildIndex(_dragBtn, targetIndex);
            }
        }

        private void SidebarBtn_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.Button != MouseButtons.Left)
            {
                _dragBtn = null;
                _isDragging = false;
                return;
            }
            _isDragging = false;
            _dragBtn = null;
            // 同步新顺序到 config
            var cfg = ConfigHelper.Load();
            var newOrder = new System.Collections.Generic.List<GameEntry>();
            foreach (Control c in panelSidebarItems.Controls)
            {
                int idx = _sidebarBtns.IndexOf(c as SidebarButton);
                if (idx >= 0 && idx < cfg.Games.Count)
                    newOrder.Add(cfg.Games[idx]);
            }
            if (newOrder.Count == cfg.Games.Count)
            {
                cfg.Games = newOrder;
                ConfigHelper.Save(cfg);
            }
            // 重建侧边栏以同步 _sidebarBtns 顺序
            RebuildSidebar();
        }

        private int GetDropIndex(int y)
        {
            var controls = panelSidebarItems.Controls;
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                if (y < c.Top + c.Height / 2) return i;
            }
            return controls.Count - 1;
        }

        private static System.Drawing.Bitmap ApplyRoundedCorners(System.Drawing.Bitmap src, int radius)
        {
            var dst = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(dst);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            var rect = new System.Drawing.Rectangle(0, 0, src.Width, src.Height);
            int d = radius * 2;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.SetClip(path);
            g.DrawImage(src, rect);
            return dst;
        }

        private System.Drawing.Icon GetSidebarIcon(string iconName)
        {
            try
            {
                string basePath = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Resources");
                string file = iconName switch
                {
                    "Arknights"      => "Arknights.ico",
                    "BiliArknights"  => "BiliArknights.ico",
                    "Endfield"       => "Endfield.ico",
                    "BiliEndfield"   => "BiliEndfield.ico",
                    "GlobalEndfield" => "GlobalEndfield.ico",
                    "official"       => "official.ico",
                    _ => null
                };
                if (file == null) return null;
                string fullPath = System.IO.Path.Combine(basePath, file);
                if (!System.IO.File.Exists(fullPath)) return null;
                return new System.Drawing.Icon(fullPath, new System.Drawing.Size(256, 256));
            }
            catch { return null; }
        }

        private void SelectGame(GameEntry g)
        {
            _currentGame = g;
            var config = ConfigHelper.Load();
            for (int i = 0; i < _sidebarBtns.Count && i < config.Games.Count; i++)
            {
                _sidebarBtns[i].Selected = config.Games[i].IconName == g.IconName;
            }
            var oldPage = _currentGamePage;
            panelMain.Controls.Clear();
            oldPage?.Dispose();
            AntdUI.Spin.open(panelMain, async cfg =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                panelMain.Invoke(() =>
                {
                    _currentGamePage = new GamePage(g, this);
                    panelMain.Controls.Add(_currentGamePage);
                    _currentGamePage.UpdateLaunchPanelColor();
                });
            });
        }

        private void btnSidebarManage_Click(object sender, EventArgs e)
        {
            var picker = new GameIconPickerForm(this);
            AntdUI.Drawer.open(this, picker, AntdUI.TAlignMini.Bottom);
        }

        private void InitTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.icon,
                Text = "Xel Launcher",
                Visible = false,
            };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add(AntdUI.Localization.Get("App.Tray.Show", "显示主窗口"), null, (s, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(AntdUI.Localization.Get("App.Tray.Exit", "退出"), null, (s, e) => { _forceClose = true; _trayIcon.Visible = false; Application.Exit(); });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _trayIcon.Visible = false;
        }

        public void HideToTray()
        {
            Hide();
            _trayIcon.Visible = true;
        }

        public void ShowFromTray()
        {
            if (!IsHandleCreated) return;
            Invoke(new Action(() =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
                _trayIcon.Visible = false;
            }));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (WindowState == FormWindowState.Minimized && _minimizeToTray)
            {
                Hide();
                _trayIcon.Visible = true;
            }
        }

        private void btn_back_Click(object sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                if (windowBar.Tag is Control control)
                {
                    control.Dispose();
                    Controls.Remove(control);
                }
                windowBar.ShowBack = false;
                windowBar.SubText = "Overview";
            }));
        }

        private void btn_bgcolor_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            btn_bgcolor.SelectedValue = null;
            if (e.Value is not string hex) return;
            if (hex == "custom")
            {
                BeginInvoke(() =>
                {
                    var picker = new AntdUI.ColorPicker { Size = new System.Drawing.Size(40, 40) };
                    var cfg0 = ConfigHelper.Load();
                    picker.Value = System.Drawing.ColorTranslator.FromHtml(cfg0.BackgroundColor);
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(this, AntdUI.Localization.Get("App.BgColor.DialogTitle", "自定义背景色"), picker)
                    {
                        OkText = AntdUI.Localization.Get("App.BgColor.OK", "确定"), CancelText = AntdUI.Localization.Get("App.BgColor.Cancel", "取消"), MaskClosable = true,
                    });
                    if (result != System.Windows.Forms.DialogResult.OK) return;
                    hex = "#" + picker.Value.ToHex();
                    ApplyBackgroundColor(hex);
                    var cfg = ConfigHelper.Load();
                    cfg.BackgroundColor = hex;
                    ConfigHelper.Save(cfg);
                });
                return;
            }
            ApplyBackgroundColor(hex);
            var cfg2 = ConfigHelper.Load();
            cfg2.BackgroundColor = hex;
            ConfigHelper.Save(cfg2);
        }

        private void ApplyBackgroundColor(string hex)
        {
            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(hex);
                BackColor = color;
                _currentGamePage?.UpdateLaunchPanelColor();
            }
            catch { }
        }

        private void btn_mode_Click(object sender, EventArgs e)
        {
            btn_mode.Toggle = AntdUI.Config.IsDark = !AntdUI.Config.IsDark;
            // 持久化用户手动选择的主题模式
            var cfg = ConfigHelper.Load();
            cfg.ThemeMode = AntdUI.Config.IsDark ? "dark" : "light";
            ConfigHelper.Save(cfg);
            // AntdUI ThemeConfig 已经在 Config.IsDark 切换时自动设置了 BackColor（深色=#000，浅色=#fff）
            // 浅色模式下，如果用户有自定义背景色，再叠加覆盖一次
            if (!AntdUI.Config.IsDark)
                ApplyBackgroundColor(ConfigHelper.Load().BackgroundColor);
            _currentGamePage?.UpdateLaunchPanelColor();
            // 主题切换后让角标用新背景色重绘描边
            updateBadge.Invalidate();
        }

        private void colorTheme_ValueChanged(object sender, AntdUI.ColorEventArgs e)
        {
            AntdUI.Style.SetPrimary(e.Value);
            var cfg = ConfigHelper.Load();
            cfg.PrimaryColor = "#" + e.Value.ToHex();
            ConfigHelper.Save(cfg);
            Refresh();
        }

        private void btn_setting_Click(object sender, EventArgs e)
        {
            var setting = new Setting(this);
            if (AntdUI.Modal.open(this, AntdUI.Localization.Get("Setting", "设置"), setting) == DialogResult.OK)
            {
                AntdUI.Config.Animation = setting.Animation;
                AntdUI.Config.ShadowEnabled = setting.ShadowEnabled;
                AntdUI.Config.ShowInWindow = setting.ShowInWindow;
                AntdUI.Config.ScrollBarHide = setting.ScrollBarHide;
                if (AntdUI.Config.TextRenderingHighQuality != setting.TextRenderingHighQuality)
                {
                    AntdUI.Config.TextRenderingHighQuality = setting.TextRenderingHighQuality;
                    Refresh();
                }
                var cfg = ConfigHelper.Load();
                cfg.MinimizeToTray = setting.MinimizeToTray;
                cfg.CloseAfterLaunch = setting.CloseAfterLaunch;
                cfg.HideToTrayOnLaunch = setting.HideToTrayOnLaunch;
                cfg.UseHardLink = setting.UseHardLink;
                cfg.UseExternalBrowser = setting.UseExternalBrowser;
                ConfigHelper.Save(cfg);
                Setting.ApplyStartWithWindows(setting.StartWithWindows);
            }
            RebuildGameButtons();
            RebuildSidebar();
        }

        private void btn_global_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value is not string lang) return;

            // Apply language
            if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
            else AntdUI.Localization.Provider = null;
            AntdUI.Localization.SetLanguage(lang);

            // Persist language choice
            var cfg = ConfigHelper.Load();
            cfg.Language = lang;
            ConfigHelper.Save(cfg);

            // Rebuild localized runtime items
            btn_more.Items.Clear();
            btn_more.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.Help", "帮助"), "help").SetIcon("QuestionCircleOutlined"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.About", "关于"),"info").SetIcon("InfoCircleOutlined"),
                new AntdUI.SelectItem("Github","github").SetIcon("GithubOutlined"),
                new AntdUI.SelectItem("BiliBili","bilibili").SetIcon("BilibiliOutlined"),
            });
            btn_bgcolor.Items.Clear();
            btn_bgcolor.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.White", "纯白"),      "#FFFFFF"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Mint", "薄荷绿"),    "#F0F7F4"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Warm", "暖米色"),    "#FAF7F2"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Sky", "天空蓝"),    "#EFF6FB"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.BgColor.Custom", "自定义.."), "custom"),
            });

            // Rebuild sidebar (re-creates context menu items with updated language)
            RebuildSidebar();

            // 0.5s loading overlay on the whole window, then rebuild current page
            AntdUI.Spin.open(this, async spinCfg =>
            {
                await System.Threading.Tasks.Task.Delay(500);
                this.Invoke(new Action(() =>
                {
                    // Rebuild the GamePage so all in-constructor strings are re-created in new language
                    if (_currentGame != null) SelectGame(_currentGame);
                    Refresh();
                }));
            });
        }

        private void btn_more_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            btn_more.SelectedValue = null;
            if (e.Value is string code)
            {
                BeginInvoke(() =>
                {
                    switch (code)
                    {
                        case "help":
                            var browser = new TabHeaderForm("https://www.google.com/");
                            browser.Show(this);
                            break;
                        case "bilibili":
                            var bilibrowser = new TabHeaderForm("https://space.bilibili.com/244484919");
                            bilibrowser.Show(this);
                            break;
                        case "github":
                            var gitbrowser = new TabHeaderForm("https://github.com/lTinchl");
                            gitbrowser.Show(this);
                            break;
                        case "info":
                            AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", new About())
                            {
                                OkText = null,
                                CancelText = null,
                                BtnHeight = 0,
                                EnableSound = false,
                                MaskClosable = true
                            });
                            break;
                    }
                });
            }
        }
    }
}
