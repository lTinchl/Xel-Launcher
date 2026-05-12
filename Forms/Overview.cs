using AntdUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public partial class Overview : AntdUI.Window
    {
        GameEntry _currentGame = null;
        readonly List<SidebarButton> _sidebarBtns = new();
        private NotifyIcon _trayIcon;
        private bool _minimizeToTray => ConfigHelper.Load().MinimizeToTray;
        private bool _forceClose = false;
        private GamePage _currentGamePage = null;
        private bool _isSwitchingGame = false;
        private const int SidebarButtonWidth = 108;
        private const int SidebarButtonHeight = 72;
        private const int SidebarButtonGap = 4;
        private const int SidebarButtonTop = 4;

        private SidebarButton _dragBtn = null;
        private System.Drawing.Point _dragStartPos;
        private bool _isDragging = false;
        private bool _suppressSidebarClick = false;
        private Timer _sidebarReorderTimer;
        private Stopwatch _sidebarReorderWatch;
        private Dictionary<Control, System.Drawing.Rectangle> _sidebarAnimFrom = new();
        private Dictionary<Control, System.Drawing.Rectangle> _sidebarAnimTo = new();
        private Timer _sidebarSelectionTimer;
        private SidebarSelectionIndicator _sidebarSelectionIndicator;
        private System.Drawing.RectangleF _sidebarSelectionBounds;
        private System.Drawing.RectangleF _sidebarSelectionTarget;
        private System.Drawing.Color _sidebarSelectionColor = AntdUI.Style.Db.Primary;
        private bool _sidebarSelectionInitialized;

        public Overview(bool top)
        {
            InitializeComponent();
            this.Icon = Properties.Resources.icon;
            windowBar.Text = "Xel Launcher ";
            AntdUI.Config.DropDownMarginFurther = true;

            TopMost = top;
            EnableDoubleBuffer(panelSidebar);
            EnableDoubleBuffer(panelSidebarItems);
            EnableDoubleBuffer(sidebarBottomPad);
            EnableDoubleBuffer(panelMain);
            panelSidebarItems.SizeChanged += (s, e) => LayoutSidebarButtons(false);
            panelSidebarItems.Scroll += (s, e) => LayoutSidebarButtons(false);
            _sidebarSelectionIndicator = new SidebarSelectionIndicator
            {
                Visible = false,
                AccentColor = _sidebarSelectionColor
            };
            panelSidebar.Controls.Add(_sidebarSelectionIndicator);
            _sidebarSelectionIndicator.BringToFront();
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
            var lang = AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) btn_global.SelectedValue = globals[1].Tag;
            else btn_global.SelectedValue = globals[0].Tag;

            RebuildSidebar();
            InitTrayIcon();
            RebuildFloatMenu();
            btn_mode.Toggle = AntdUI.Config.IsDark;
            btn_mode.SetDarkIcon(AntdUI.Config.IsDark);
            Load += (s, e) =>
            {
                if (!AntdUI.Config.IsDark)
                    ApplyBackgroundColor(ConfigHelper.Load().BackgroundColor);
                else
                    ApplyThemeSurfaces();

                PositionUpdateBadge();
                LoadUpdateBadgeFromCache();
                _ = RefreshUpdateStateOnStartupAsync();
                _ = RunSkylandAutoSignOnLaunchAsync();
                _ = RunSkportAutoSignOnLaunchAsync();
            };
            windowBar.SizeChanged += (s, e) => PositionUpdateBadge();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sidebarSelectionTimer?.Stop();
                _sidebarSelectionTimer?.Dispose();
                _sidebarSelectionTimer = null;
            }

            base.Dispose(disposing);
        }
    }
}
