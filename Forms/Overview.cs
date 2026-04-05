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
    public class BrowserForm : AntdUI.Window
    {
        private TabHeader tabHeader;
        private System.Windows.Forms.Panel panelContent;
        private readonly Dictionary<string, WebView2> _browsers = new();
        private readonly Dictionary<string, TagTabItem> _tabs = new();
        private int _tabCounter = 0;

        public BrowserForm(string startUrl = "https://www.google.com")
        {
            this.Text = "浏览器";
            this.Size = new System.Drawing.Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterParent;

            // 内容区域先加（在下层）
            panelContent = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(panelContent);

            // TabHeader 后加（在上层）
            tabHeader = new TabHeader
            {
                Dock = DockStyle.Top,
                ShowAdd = true,
                DragSort = true,
                Height = 44
            };
            this.Controls.Add(tabHeader);

            // 事件绑定——用标准 EventHandler 签名，让 VS 推断
            tabHeader.TabChanged += TabHeader_TabChanged;
            tabHeader.TabClosing += TabHeader_TabClosing;
            tabHeader.AddClick += (s, e) => AddTab("https://www.google.com");

            AddTab(startUrl);
        }

        private void TabHeader_TabChanged(object sender, TabChangedEventArgs e)
        {
            OnTabChanged(e.Value);
        }

        private void TabHeader_TabClosing(object sender, TabCloseEventArgs e)
        {
            OnTabClosing(e.Value);
        }

        private async void AddTab(string url)
        {
            _tabCounter++;
            string tabId = "tab_" + _tabCounter;

            var tab = new TagTabItem(url) { ShowClose = true, Tag = tabId };
            _tabs[tabId] = tab;
            tabHeader.AddTab(tab, true);

            foreach (var kv in _browsers)
                kv.Value.Visible = false;

            var browser = new WebView2 { Dock = DockStyle.Fill, Visible = true };
            _browsers[tabId] = browser;
            panelContent.Controls.Add(browser);

            try
            {
                await browser.EnsureCoreWebView2Async();
                browser.CoreWebView2.Navigate(url);

                browser.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    if (this.IsHandleCreated)
                        this.Invoke(() => tab.Text = browser.CoreWebView2.DocumentTitle);
                };
            }
            catch
            {
                AntdUI.Message.error(this, "未找到 WebView2 Runtime，请先安装！");
            }
        }

        private void OnTabChanged(TagTabItem tab)
        {
            if (tab?.Tag is not string tabId) return;
            foreach (var kv in _browsers)
                kv.Value.Visible = kv.Key == tabId;
        }

        private void OnTabClosing(TagTabItem tab)
        {
            if (tab?.Tag is not string tabId) return;

            if (_browsers.TryGetValue(tabId, out var browser))
            {
                browser.Dispose();
                _browsers.Remove(tabId);
            }
            _tabs.Remove(tabId);

            if (tabHeader.Items.Count == 0)
                this.Close();
        }
    }

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
                new AntdUI.SelectItem("中文","zh-CN"),
                new AntdUI.SelectItem("English(待施工)","en-US")
            };
            btn_global.Items.AddRange(globals);
            btn_more.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem("帮助", "help").SetIcon("QuestionCircleOutlined"),
                new AntdUI.SelectItem("关于","info").SetIcon("InfoCircleOutlined"),
                new AntdUI.SelectItem("Github","github").SetIcon("GithubOutlined"),
                new AntdUI.SelectItem("BiliBili","bilibili").SetIcon("BilibiliOutlined"),

            });
            btn_bgcolor.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem("纯白",      "#FFFFFF"),
                new AntdUI.SelectItem("薄荷绿",    "#F0F7F4"),
                new AntdUI.SelectItem("暖米色",    "#FAF7F2"),
                new AntdUI.SelectItem("天空蓝",    "#EFF6FB"),
                new AntdUI.SelectItem("自定义..", "custom"),
            });
            var lang = AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) btn_global.SelectedValue = globals[1].Tag;
            else btn_global.SelectedValue = globals[0].Tag;

            RebuildSidebar();
            InitTrayIcon();
            RebuildFloatMenu();
            Load += (s, e) =>
            {
                ApplyBackgroundColor(ConfigHelper.Load().BackgroundColor);
            };
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
                        new AntdUI.ContextMenuStripItem("删除").SetIcon("DeleteOutlined"),
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
            panelMain.Controls.Clear();
            AntdUI.Spin.open(panelMain, async cfg =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                panelMain.Invoke(() =>
                {
                    _currentGamePage = new GamePage(g, this);
                    panelMain.Controls.Add(_currentGamePage);
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
            menu.Items.Add("显示主窗口", null, (s, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { _forceClose = true; _trayIcon.Visible = false; Application.Exit(); });
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
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(this, "自定义背景色", picker)
                    {
                        OkText = "确定", CancelText = "取消", MaskClosable = true,
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
            _currentGamePage?.UpdateLaunchPanelColor();
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
                cfg.MAA_Official = setting.MaaOfficial;
                cfg.MAA_Bilibili = setting.MaaBilibili;
                ConfigHelper.Save(cfg);
                Setting.ApplyStartWithWindows(setting.StartWithWindows);
            }
            RebuildGameButtons();
            RebuildSidebar();
        }

        private void btn_global_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value is string lang)
            {
                if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
                else AntdUI.Localization.Provider = null;
                AntdUI.Localization.SetLanguage(lang);
                Refresh();
            }
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
                            var browser = new TabHeaderForm("https://www.bing.com/");
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
