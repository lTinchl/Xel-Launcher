using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher
{
    public partial class Setting : UserControl
    {
        AntdUI.BaseForm form;
        private const int EmLineScroll = 0x00B6;
        private const int EmGetLineCount = 0x00BA;
        private const int EmGetFirstVisibleLine = 0x00CE;
        private const int WmVScroll = 0x0115;
        private const int SbBottom = 7;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private UpdateInfo _updateInfo;
        private CancellationTokenSource _downloadCts;
        private RoundedPanel _logCard;
        private RoundedPanel _updateHeaderCard;
        private RoundedPanel _softwareCard;
        private AntdUI.Label _updateSectionTitle;
        private ThinScrollBar _updateChangelogScrollBar;
        private ThinScrollBar _logScrollBar;
        private System.Windows.Forms.Timer _logScrollHideTimer;
        private System.Windows.Forms.Timer _updateScrollHideTimer;
        private Panel _tabBar;
        private Panel _tabUnderline;
        private AntdUI.Label _settingTitle;
        private Panel _logHeader;
        private AntdUI.Label _logTitle;
        private AntdUI.Label _updateHeaderTitle;
        private AntdUI.Label _updateHeaderSubtitle;
        private AntdUI.Label _updateHeaderArrow;
        private AntdUI.Label _updateAutoOption;
        private AntdUI.Label _updateNotifyOption;
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "Xel Launcher";

        public bool Animation, ShadowEnabled, ShowInWindow, ScrollBarHide, TextRenderingHighQuality, MinimizeToTray, StartWithWindows, CloseAfterLaunch, HideToTrayOnLaunch, UseExternalBrowser, UseHardLink;

        public Setting(AntdUI.BaseForm _form)
        {
            form = _form;
            InitializeComponent();
            Size = new Size(600, 500);
            MinimumSize = Size;
            HideInternalUiOptions();
            ApplyModernLayout();
            ApplyThemeColors();
            btnSoftware.Click += (s, e) => ShowPanel(0);
            btnLog.Click += (s, e) => ShowPanel(1);
            btnUpdate.Click += (s, e) => ShowPanel(2);
            LogHelper.OnLog += RefreshLogFromLogger;
            Disposed += (s, e) =>
            {
                LogHelper.OnLog -= RefreshLogFromLogger;
                _logScrollHideTimer?.Dispose();
                _updateScrollHideTimer?.Dispose();
            };

            switch1.Checked = Animation = AntdUI.Config.Animation;
            switch2.Checked = ShadowEnabled = AntdUI.Config.ShadowEnabled;
            switch3.Checked = ShowInWindow = AntdUI.Config.ShowInWindow;
            switch4.Checked = ScrollBarHide = AntdUI.Config.ScrollBarHide;
            switch5.Checked = TextRenderingHighQuality = AntdUI.Config.TextRenderingHighQuality;
            switch6.Checked = MinimizeToTray = ConfigHelper.Load().MinimizeToTray;
            switch7.Checked = StartWithWindows = GetStartWithWindows();
            switch8.Checked = CloseAfterLaunch = ConfigHelper.Load().CloseAfterLaunch;
            switch9.Checked = HideToTrayOnLaunch = ConfigHelper.Load().HideToTrayOnLaunch;
            switch10.Checked = UseExternalBrowser = ConfigHelper.Load().UseExternalBrowser;
            switch11.Checked = UseHardLink = ConfigHelper.Load().UseHardLink;

            switch1.CheckedChanged += (s, e) => { Animation = e.Value; };
            switch2.CheckedChanged += (s, e) => { ShadowEnabled = e.Value; };
            switch3.CheckedChanged += (s, e) => { ShowInWindow = e.Value; };
            switch4.CheckedChanged += (s, e) => { ScrollBarHide = e.Value; };
            switch5.CheckedChanged += (s, e) => { TextRenderingHighQuality = e.Value; };
            switch6.CheckedChanged += (s, e) => { MinimizeToTray = e.Value; };
            switch7.CheckedChanged += (s, e) => { StartWithWindows = e.Value; };
            switch8.CheckedChanged += (s, e) => { CloseAfterLaunch = e.Value; };
            switch9.CheckedChanged += (s, e) => { HideToTrayOnLaunch = e.Value; };
            switch10.CheckedChanged += (s, e) => { UseExternalBrowser = e.Value; };
            switch11.CheckedChanged += (s, e) => { UseHardLink = e.Value; };

            BindUpdatePanel();
            Load += async (s, e) =>
            {
                try { await CheckUpdateAsync(); }
                catch { /* 静默失败，不打扰用户 */ }
            };
        }

        private void HideInternalUiOptions()
        {
            var controls = new Control[]
            {
                label1, switch1,
                label2, switch2,
                label3, switch3,
                label4, switch4,
                label5, switch5,
            };

            foreach (var control in controls)
                control.Visible = false;

            for (int i = 0; i < 5 && i < tableSoftware.RowStyles.Count; i++)
            {
                tableSoftware.RowStyles[i].SizeType = SizeType.Absolute;
                tableSoftware.RowStyles[i].Height = 0F;
            }
        }

        private void ApplyThemeColors()
        {
            if (!AntdUI.Config.IsDark) return;

            BackColor = AppTheme.DarkBackground;
            ForeColor = AppTheme.DarkForeground;
            panelLeft.BackColor = AppTheme.DarkBackground;
            panelRight.BackColor = AppTheme.DarkBackground;
            scrollSoftware.BackColor = AppTheme.DarkBackground;
            panelLog.BackColor = AppTheme.DarkBackground;
            panelUpdate.BackColor = AppTheme.DarkBackground;
            tableSoftware.BackColor = _softwareCard != null ? AppTheme.DarkSurface : AppTheme.DarkBackground;
            if (_tabBar != null) _tabBar.BackColor = AppTheme.DarkBackground;
            if (_settingTitle != null) _settingTitle.ForeColor = AppTheme.DarkForeground;
            if (_tabUnderline != null) _tabUnderline.BackColor = Color.FromArgb(255, 76, 84);
            if (_logHeader != null) _logHeader.BackColor = AppTheme.DarkBackground;
            if (_logTitle != null) _logTitle.ForeColor = AppTheme.DarkForeground;

            if (_logCard != null)
            {
                _logCard.FillColor = AppTheme.DarkSurface;
                _logCard.BorderColor = Color.FromArgb(66, 72, 82);
            }
            if (_logScrollBar != null)
                _logScrollBar.BackColor = AppTheme.DarkSurface;
            if (_softwareCard != null)
            {
                _softwareCard.FillColor = AppTheme.DarkSurface;
                _softwareCard.BorderColor = Color.FromArgb(66, 72, 82);
            }
            txtLog.BackColor = AppTheme.DarkSurface;
            txtLog.ForeColor = AppTheme.DarkForegroundSecondary;
            txtChangelog.BackColor = Color.FromArgb(24, 29, 36);
            txtChangelog.ForeColor = Color.FromArgb(218, 232, 255);
        }

        private void ApplyModernLayout()
        {
            var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.FromArgb(250, 252, 255);
            var cardBack = AntdUI.Config.IsDark ? AppTheme.DarkSurface : Color.White;
            var border = AntdUI.Config.IsDark ? Color.FromArgb(66, 72, 82) : Color.FromArgb(214, 221, 232);
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);

            BackColor = surface;
            panelLeft.Visible = false;
            dividerV.Visible = false;
            panelLeft.BackColor = surface;
            panelRight.BackColor = surface;
            panelRight.Dock = DockStyle.Fill;
            panelRight.Padding = new Padding(14, 66, 14, 12);
            panelLog.BackColor = surface;
            panelUpdate.BackColor = surface;
            scrollSoftware.BackColor = surface;
            scrollSoftware.Padding = new Padding(0);

            dividerV.Width = 1;

            BuildTopTabs(surface, normalText, subtleText);
            BuildSoftwareCard(cardBack, border, normalText, subtleText);

            tableSoftware.BackColor = cardBack;
            tableSoftware.Padding = new Padding(0);
            foreach (Control control in tableSoftware.Controls)
            {
                if (control is AntdUI.Label label)
                {
                    label.Font = new Font("Microsoft YaHei UI", 9.5F);
                    label.ForeColor = normalText;
                    label.TextAlign = ContentAlignment.MiddleLeft;
                }
            }

            panelLog.Padding = Padding.Empty;
            _logCard = WrapTextBox(panelLog, txtLog, cardBack, border);
            _logCard.Dock = DockStyle.None;
            _logCard.Padding = new Padding(12, 14, 12, 10);
            BuildLogScrollBar(cardBack);
            BuildLogHeader(surface, normalText, subtleText);
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.BackColor = cardBack;
            txtLog.ForeColor = normalText;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.ScrollBars = RichTextBoxScrollBars.None;
            txtLog.MouseWheel += HandleLogMouseWheel;
            txtLog.VScroll += (s, e) => UpdateLogScrollBar();
            txtLog.TextChanged += (s, e) => UpdateLogScrollBar();

            panelUpdate.Padding = Padding.Empty;
            BuildUpdateHeader(cardBack, border, normalText, subtleText);
            lblCurrentVersionTitle.ForeColor = subtleText;
            lblLatestVersionTitle.ForeColor = subtleText;
            lblCurrentVersion.ForeColor = normalText;
            lblLatestVersion.ForeColor = normalText;
            lblCurrentVersion.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            lblLatestVersion.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            btnCheckUpdate.Radius = 6;
            btnCheckUpdate.Type = AntdUI.TTypeMini.Primary;

            if (txtChangelog.Parent != _updateHeaderCard)
            {
                txtChangelog.Parent?.Controls.Remove(txtChangelog);
                _updateHeaderCard.Controls.Add(txtChangelog);
            }
            txtChangelog.Visible = true;
            txtChangelog.ReadOnly = true;
            txtChangelog.TabStop = false;
            txtChangelog.ShortcutsEnabled = false;
            txtChangelog.DetectUrls = false;
            txtChangelog.BorderStyle = BorderStyle.None;
            txtChangelog.ScrollBars = RichTextBoxScrollBars.None;
            txtChangelog.Dock = DockStyle.None;
            txtChangelog.Font = new Font("Microsoft YaHei UI", 9F);
            txtChangelog.BackColor = _updateHeaderCard.FillColor;
            txtChangelog.ForeColor = normalText;
            txtChangelog.MouseWheel += HandleUpdateChangelogMouseWheel;
            txtChangelog.VScroll += (s, e) => UpdateChangelogScrollBar();
            txtChangelog.TextChanged += (s, e) => UpdateChangelogScrollBar();
            if (_updateChangelogScrollBar != null)
            {
                _updateChangelogScrollBar.Visible = false;
                _updateChangelogScrollBar.BringToFront();
            }

            if (panelUpdateButtons.Parent != _updateHeaderCard)
            {
                panelUpdateButtons.Parent?.Controls.Remove(panelUpdateButtons);
                _updateHeaderCard.Controls.Add(panelUpdateButtons);
            }
            panelUpdateButtons.BackColor = _updateHeaderCard.FillColor;
            panelUpdateButtons.Padding = new Padding(0);
            panelUpdateButtons.Visible = false;
            ConfigureUpdateButtons(surface, border, normalText);
            lblDownloadStatus.ForeColor = subtleText;
            LayoutUpdateHeader();
        }

        private void BuildUpdateHeader(Color cardBack, Color border, Color normalText, Color subtleText)
        {
            if (tableUpdate.Parent != panelUpdate)
                return;

            panelUpdate.Controls.Remove(tableUpdate);
            panelUpdate.Controls.Remove(txtChangelog);
            panelUpdate.Controls.Remove(panelUpdateButtons);
            tableUpdate.Controls.Remove(lblCurrentVersionTitle);
            tableUpdate.Controls.Remove(lblLatestVersionTitle);
            tableUpdate.Controls.Remove(lblCurrentVersion);
            tableUpdate.Controls.Remove(lblLatestVersion);
            tableUpdate.Controls.Remove(btnCheckUpdate);

            _updateHeaderCard = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 420,
                Padding = new Padding(18),
                FillColor = cardBack,
                BorderColor = border,
                AccentColor = Color.FromArgb(22, 119, 255),
                ShowAccent = false,
                Radius = 10,
            };

            _updateHeaderTitle = new AntdUI.Label
            {
                Text = "\u53d1\u73b0\u65b0\u7248\u672c",
                Size = new Size(180, 30),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = normalText,
            };
            _updateHeaderSubtitle = new AntdUI.Label
            {
                Text = "\u5f53\u524d\u7248\u672c\uff1a",
                Size = new Size(76, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            lblCurrentVersionTitle.Text = "";
            lblCurrentVersionTitle.Size = new Size(1, 1);
            lblCurrentVersionTitle.Dock = DockStyle.None;
            lblCurrentVersionTitle.Font = new Font("Microsoft YaHei UI", 9F);
            lblCurrentVersion.Size = new Size(96, 24);
            lblCurrentVersion.Dock = DockStyle.None;

            _updateHeaderArrow = new AntdUI.Label
            {
                Text = "",
                Size = new Size(1, 1),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 119, 255),
            };

            lblLatestVersionTitle.Text = "\u6700\u65b0\u7248\u672c\uff1a";
            lblLatestVersionTitle.Size = new Size(76, 24);
            lblLatestVersionTitle.Dock = DockStyle.None;
            lblLatestVersionTitle.Font = new Font("Microsoft YaHei UI", 9F);
            lblLatestVersionTitle.ForeColor = subtleText;
            lblLatestVersion.Size = new Size(96, 24);
            lblLatestVersion.Dock = DockStyle.None;
            lblLatestVersion.ForeColor = Color.FromArgb(22, 119, 255);

            btnCheckUpdate.Text = "\u68c0\u67e5\u66f4\u65b0";
            btnCheckUpdate.Size = new Size(92, 30);
            btnCheckUpdate.Dock = DockStyle.None;
            btnCheckUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCheckUpdate.Ghost = true;
            btnCheckUpdate.BorderWidth = 1F;

            _updateSectionTitle = new AntdUI.Label
            {
                Text = "\u66f4\u65b0\u5185\u5bb9",
                Size = new Size(120, 24),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = normalText,
            };
            txtChangelog.Text = "\u70b9\u51fb\u68c0\u67e5\u66f4\u65b0\u540e\u663e\u793a\u5185\u5bb9\u3002";
            _updateChangelogScrollBar = new ThinScrollBar(
                AntdUI.Config.IsDark ? Color.FromArgb(48, 52, 60) : Color.FromArgb(236, 240, 246),
                AntdUI.Config.IsDark ? Color.FromArgb(118, 128, 146) : Color.FromArgb(156, 166, 182));
            _updateChangelogScrollBar.Visible = false;
            _updateChangelogScrollBar.ScrollRequested += ScrollUpdateChangelogToTrackTop;
            _updateChangelogScrollBar.MouseWheel += HandleUpdateChangelogMouseWheel;
            _updateChangelogScrollBar.MouseEnter += (s, e) => RevealUpdateScrollBar(autoHide: false);
            _updateChangelogScrollBar.MouseLeave += (s, e) => ScheduleUpdateScrollBarHide();
            _updateChangelogScrollBar.DragEnded += () =>
            {
                UpdateChangelogScrollBar();
                ScheduleUpdateScrollBarHide();
            };
            _updateHeaderCard.MouseEnter += (s, e) => RevealUpdateScrollBar(autoHide: false);
            _updateHeaderCard.MouseLeave += (s, e) => ScheduleUpdateScrollBarHide();
            txtChangelog.MouseEnter += (s, e) => RevealUpdateScrollBar(autoHide: false);
            txtChangelog.MouseLeave += (s, e) => ScheduleUpdateScrollBarHide();
            _updateScrollHideTimer = new System.Windows.Forms.Timer { Interval = 900 };
            _updateScrollHideTimer.Tick += (s, e) =>
            {
                if (_updateChangelogScrollBar == null || _updateChangelogScrollBar.IsDisposed) return;
                if (_updateChangelogScrollBar.IsDragging || IsMouseInside(txtChangelog) || IsMouseInside(_updateChangelogScrollBar)) return;
                _updateScrollHideTimer.Stop();
                _updateChangelogScrollBar.Visible = false;
            };
            _updateAutoOption = new AntdUI.Label
            {
                Text = "\u53d1\u5e03\u65e5\u671f\uff1a\u68c0\u67e5\u540e\u663e\u793a",
                Size = new Size(220, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };
            _updateNotifyOption = new AntdUI.Label
            {
                Text = "\u66f4\u65b0\u5927\u5c0f\uff1a\u68c0\u67e5\u540e\u663e\u793a",
                Size = new Size(220, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            _updateHeaderCard.Resize += (s, e) => LayoutUpdateHeader();
            _updateHeaderCard.Controls.Add(_updateHeaderTitle);
            _updateHeaderCard.Controls.Add(_updateHeaderSubtitle);
            _updateHeaderCard.Controls.Add(lblCurrentVersionTitle);
            _updateHeaderCard.Controls.Add(lblCurrentVersion);
            _updateHeaderCard.Controls.Add(_updateHeaderArrow);
            _updateHeaderCard.Controls.Add(lblLatestVersionTitle);
            _updateHeaderCard.Controls.Add(lblLatestVersion);
            _updateHeaderCard.Controls.Add(btnCheckUpdate);
            _updateHeaderCard.Controls.Add(_updateSectionTitle);
            _updateHeaderCard.Controls.Add(txtChangelog);
            _updateHeaderCard.Controls.Add(_updateChangelogScrollBar);
            _updateHeaderCard.Controls.Add(_updateAutoOption);
            _updateHeaderCard.Controls.Add(_updateNotifyOption);
            panelUpdate.Controls.Add(_updateHeaderCard);
            _updateHeaderCard.BringToFront();
            LayoutUpdateHeader();
        }
        private void LayoutUpdateHeader()
        {
            if (_updateHeaderCard == null) return;

            var width = _updateHeaderCard.ClientSize.Width;
            if (width <= 0) return;

            var contentWidth = Math.Max(300, width - 36);
            _updateHeaderTitle.Location = new Point(18, 18);
            _updateHeaderSubtitle.Location = new Point(18, 56);
            lblCurrentVersion.Location = new Point(96, 56);
            lblCurrentVersion.Size = new Size(96, 24);
            lblLatestVersionTitle.Location = new Point(18, 82);
            lblLatestVersion.Location = new Point(96, 82);
            lblLatestVersion.Size = new Size(110, 24);
            lblCurrentVersionTitle.Visible = false;
            _updateHeaderArrow.Visible = false;
            btnCheckUpdate.Location = new Point(width - btnCheckUpdate.Width - 18, 22);

            _updateSectionTitle.Location = new Point(18, 120);
            txtChangelog.Location = new Point(18, 150);
            txtChangelog.Size = new Size(contentWidth - 18, 150);
            if (_updateChangelogScrollBar != null)
            {
                _updateChangelogScrollBar.Location = new Point(18 + contentWidth - 10, 154);
                _updateChangelogScrollBar.Size = new Size(8, 142);
                _updateChangelogScrollBar.BringToFront();
                UpdateChangelogScrollBar();
            }
            _updateAutoOption.Location = new Point(18, 316);
            _updateNotifyOption.Location = new Point(18, 342);

            panelUpdateButtons.Location = new Point(18, 382);
            panelUpdateButtons.Size = new Size(contentWidth, 34);
            if (btnDownloadSetup.Parent is Panel buttonPanel)
            {
                buttonPanel.Dock = DockStyle.None;
                buttonPanel.Location = new Point(0, 0);
                buttonPanel.Size = new Size(contentWidth, 34);
                btnDownloadSetup.Size = new Size(108, 32);
                btnDownloadSetup.Location = new Point(0, 1);
                btnDownloadPortable.Size = new Size(112, 32);
                btnDownloadPortable.Location = new Point(124, 1);
                btnFallback.Size = new Size(100, 32);
                btnFallback.Location = new Point(252, 1);
            }
            progressDownload.Width = contentWidth;
        }

        private void ConfigureUpdateButtons(Color surface, Color border, Color normalText)
        {
            btnDownloadSetup.Text = "\u7acb\u5373\u66f4\u65b0";
            btnDownloadSetup.LocalizationText = "";
            btnDownloadSetup.Type = AntdUI.TTypeMini.Primary;
            btnDownloadSetup.Radius = 6;

            btnDownloadPortable.Text = "\u4fbf\u643a\u7248";
            btnDownloadPortable.LocalizationText = "";
            btnDownloadPortable.Type = AntdUI.TTypeMini.Default;
            btnDownloadPortable.Ghost = true;
            btnDownloadPortable.BorderWidth = 1F;
            btnDownloadPortable.Radius = 6;

            btnFallback.Text = "\u7f51\u76d8\u4e0b\u8f7d";
            btnFallback.LocalizationText = "";
            btnFallback.Type = AntdUI.TTypeMini.Default;
            btnFallback.Ghost = true;
            btnFallback.BorderWidth = 1F;
            btnFallback.Radius = 6;
        }
        private void BuildTopTabs(Color surface, Color normalText, Color subtleText)
        {
            if (_tabBar == null)
            {
                _tabBar = new Panel
                {
                    Dock = DockStyle.None,
                    Height = 44,
                    BackColor = surface,
                    Padding = new Padding(0),
                };
                _settingTitle = new AntdUI.Label
                {
                    Text = "设置",
                    Size = new Size(120, 30),
                    Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                    ForeColor = normalText,
                    Visible = false,
                };
                _tabUnderline = new Panel
                {
                    Size = new Size(34, 2),
                    BackColor = Color.FromArgb(255, 76, 84),
                };
                panelRight.Controls.Add(_tabBar);
                _tabBar.Controls.Add(_settingTitle);
                _tabBar.Controls.Add(btnSoftware);
                _tabBar.Controls.Add(btnUpdate);
                _tabBar.Controls.Add(btnLog);
                _tabBar.Controls.Add(_tabUnderline);
                panelRight.Resize += (s, e) => LayoutTopTabs();
                _tabBar.Resize += (s, e) => LayoutTopTabs();
            }
            else
            {
                if (btnSoftware.Parent != _tabBar)
                {
                    btnSoftware.Parent?.Controls.Remove(btnSoftware);
                    _tabBar.Controls.Add(btnSoftware);
                }
                if (btnUpdate.Parent != _tabBar)
                {
                    btnUpdate.Parent?.Controls.Remove(btnUpdate);
                    _tabBar.Controls.Add(btnUpdate);
                }
                if (btnLog.Parent != _tabBar)
                {
                    btnLog.Parent?.Controls.Remove(btnLog);
                    _tabBar.Controls.Add(btnLog);
                }
            }

            _tabBar.BackColor = surface;
            StyleTabButton(btnSoftware, "常规", normalText);
            StyleTabButton(btnUpdate, "软件更新", normalText);
            StyleTabButton(btnLog, "软件日志", normalText);
            _tabBar.BringToFront();
            LayoutTopTabs();
            UpdateTopTabState(0);
        }

        private void LayoutTopTabs()
        {
            if (_tabBar == null) return;

            _tabBar.Location = new Point(panelRight.Padding.Left, 14);
            _tabBar.Width = Math.Max(180, panelRight.ClientSize.Width - panelRight.Padding.Left - panelRight.Padding.Right);

            var y = 8;
            var x = 0;
            LayoutTabButton(btnSoftware, x, y, 64);
            x += btnSoftware.Width + 22;
            LayoutTabButton(btnUpdate, x, y, 86);
            x += btnUpdate.Width + 22;
            LayoutTabButton(btnLog, x, y, 86);
            PositionTabUnderline();
        }

        private void StyleTabButton(AntdUI.Button button, string text, Color foreColor)
        {
            button.Text = text;
            button.LocalizationText = "";
            button.Dock = DockStyle.None;
            button.Ghost = true;
            button.BorderWidth = 0;
            button.Radius = 0;
            button.Type = AntdUI.TTypeMini.Default;
            button.TabStop = false;
            button.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            button.ForeColor = foreColor;
            button.GotFocus += (s, e) => panelRight.Focus();
            button.MouseUp += (s, e) => panelRight.Focus();
        }

        private static void LayoutTabButton(AntdUI.Button button, int x, int y, int width)
        {
            button.Location = new Point(x, y);
            button.Size = new Size(width, 28);
        }

        private void UpdateTopTabState(int tab)
        {
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
            btnSoftware.ForeColor = tab == 0 ? normalText : subtleText;
            btnUpdate.ForeColor = tab == 2 ? normalText : subtleText;
            btnLog.ForeColor = tab == 1 ? normalText : subtleText;
            btnSoftware.Font = new Font("Microsoft YaHei UI", 10F, tab == 0 ? FontStyle.Bold : FontStyle.Regular);
            btnUpdate.Font = new Font("Microsoft YaHei UI", 10F, tab == 2 ? FontStyle.Bold : FontStyle.Regular);
            btnLog.Font = new Font("Microsoft YaHei UI", 10F, tab == 1 ? FontStyle.Bold : FontStyle.Regular);
            PositionTabUnderline();
        }

        private void PositionTabUnderline()
        {
            if (_tabUnderline == null) return;

            var active = scrollSoftware.Visible ? btnSoftware : panelUpdate.Visible ? btnUpdate : btnLog;
            _tabUnderline.Width = Math.Min(34, active.Width - 12);
            _tabUnderline.Location = new Point(active.Left + 6, active.Bottom + 1);
            _tabUnderline.BringToFront();
        }

        private void BuildSoftwareCard(Color cardBack, Color border, Color normalText, Color subtleText)
        {
            if (_softwareCard == null)
            {
                scrollSoftware.Controls.Remove(tableSoftware);
                _softwareCard = new RoundedPanel
                {
                    Dock = DockStyle.Top,
                    Height = 286,
                    Padding = new Padding(16, 14, 16, 12),
                    FillColor = cardBack,
                    BorderColor = border,
                    Radius = 8,
                };
                tableSoftware.Dock = DockStyle.Top;
                tableSoftware.AutoSize = false;
                tableSoftware.Height = 260;
                _softwareCard.Controls.Add(tableSoftware);
                scrollSoftware.Controls.Add(_softwareCard);
            }

            _softwareCard.FillColor = cardBack;
            _softwareCard.BorderColor = border;
            tableSoftware.BackColor = cardBack;
            tableSoftware.ColumnStyles[1].Width = 72F;
            label6.Text = "最小化到托盘";
            label7.Text = "开机自动运行";
            label8.Text = "启动游戏后关闭软件";
            label9.Text = "启动游戏后隐藏至托盘";
            label10.Text = "使用外部浏览器";
            label11.Text = "使用硬链接切服";

            for (int i = 5; i <= 10 && i < tableSoftware.RowStyles.Count; i++)
            {
                tableSoftware.RowStyles[i].SizeType = SizeType.Absolute;
                tableSoftware.RowStyles[i].Height = 43F;
            }
        }

        private void BuildLogHeader(Color surface, Color normalText, Color subtleText)
        {
            if (_logHeader != null) return;

            _logHeader = new Panel
            {
                Dock = DockStyle.None,
                Height = 48,
                BackColor = surface,
            };
            _logTitle = new AntdUI.Label
            {
                Text = "软件日志",
                Size = new Size(120, 24),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = normalText,
            };

            _logHeader.Controls.Add(_logTitle);
            _logHeader.Resize += (s, e) => LayoutLogHeader();
            panelLog.Resize += (s, e) => LayoutLogHeader();
            panelLog.Controls.Add(_logHeader);
            _logHeader.BringToFront();
            LayoutLogHeader();
        }

        private void BuildLogScrollBar(Color cardBack)
        {
            if (_logCard == null || _logScrollBar != null) return;

            _logScrollBar = new ThinScrollBar(
                AntdUI.Config.IsDark ? Color.FromArgb(48, 52, 60) : Color.FromArgb(236, 240, 246),
                AntdUI.Config.IsDark ? Color.FromArgb(118, 128, 146) : Color.FromArgb(156, 166, 182))
            {
                Visible = false,
                BackColor = cardBack,
            };
            _logScrollBar.ScrollRequested += ScrollLogToTrackTop;
            _logScrollBar.MouseWheel += HandleLogMouseWheel;
            _logScrollBar.MouseEnter += (s, e) => RevealLogScrollBar(autoHide: false);
            _logScrollBar.MouseLeave += (s, e) => ScheduleLogScrollBarHide();
            _logScrollBar.DragEnded += () =>
            {
                UpdateLogScrollBar();
                ScheduleLogScrollBarHide();
            };

            _logCard.MouseEnter += (s, e) => RevealLogScrollBar(autoHide: false);
            _logCard.MouseLeave += (s, e) => ScheduleLogScrollBarHide();
            txtLog.MouseEnter += (s, e) => RevealLogScrollBar(autoHide: false);
            txtLog.MouseLeave += (s, e) => ScheduleLogScrollBarHide();

            _logScrollHideTimer = new System.Windows.Forms.Timer { Interval = 900 };
            _logScrollHideTimer.Tick += (s, e) =>
            {
                if (_logScrollBar == null || _logScrollBar.IsDisposed) return;
                if (_logScrollBar.IsDragging || IsMouseInside(_logCard)) return;
                _logScrollHideTimer.Stop();
                _logScrollBar.Visible = false;
            };

            _logCard.Controls.Add(_logScrollBar);
            _logScrollBar.BringToFront();
        }

        private void LayoutLogHeader()
        {
            if (_logHeader == null) return;

            var width = Math.Max(0, panelLog.ClientSize.Width);
            _logHeader.Location = new Point(0, 0);
            _logHeader.Size = new Size(width, 48);
            _logTitle.Location = new Point(0, 10);

            if (_logCard != null)
            {
                const int top = 54;
                _logCard.Location = new Point(0, top);
                _logCard.Size = new Size(width, Math.Max(80, panelLog.ClientSize.Height - top));
            }

            LayoutLogScrollBar();
        }

        private void LayoutLogScrollBar()
        {
            if (_logCard == null || _logScrollBar == null) return;

            _logScrollBar.Location = new Point(Math.Max(0, _logCard.ClientSize.Width - 18), 14);
            _logScrollBar.Size = new Size(8, Math.Max(24, _logCard.ClientSize.Height - 28));
            _logScrollBar.BringToFront();
            UpdateLogScrollBar();
        }

        private static void LayoutNavButton(AntdUI.Button button, int x, int y)
        {
            button.Dock = DockStyle.None;
            button.Location = new Point(x, y);
            button.Size = new Size(108, 38);
            button.Radius = 6;
            button.BorderWidth = 0;
        }

        private static RoundedPanel WrapTextBox(Panel host, RichTextBox textBox, Color fill, Color border)
        {
            host.Controls.Remove(textBox);

            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 10),
                FillColor = fill,
                BorderColor = border,
                Radius = 8,
            };
            textBox.Dock = DockStyle.Fill;
            card.Controls.Add(textBox);
            host.Controls.Add(card);
            return card;
        }

        private static bool GetStartWithWindows()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }

        public static void ApplyStartWithWindows(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (enable)
                key.SetValue(AppName, System.Windows.Forms.Application.ExecutablePath);
            else
                key.DeleteValue(AppName, false);
        }
        private void ShowPanel(int tab)
        {
            scrollSoftware.Visible = tab == 0;
            panelLog.Visible = tab == 1;
            panelUpdate.Visible = tab == 2;
            UpdateTopTabState(tab);
            if (tab == 1) RefreshLog();
        }

        /// <summary>
        /// 从外部调用，直接切换到「软件更新」页。
        /// </summary>
        public void NavigateToUpdate() => ShowPanel(2);

        private void RefreshLog()
        {
            txtLog.Text = LogHelper.GetAll();
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
            UpdateLogScrollBar();
        }

        private void RefreshLogFromLogger()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (!panelLog.Visible) return;

            try
            {
                if (InvokeRequired)
                    BeginInvoke(new Action(RefreshLog));
                else
                    RefreshLog();
            }
            catch (InvalidOperationException) { }
        }

        private void UpdateLogScrollBar()
        {
            if (txtLog.IsDisposed || _logScrollBar == null ||
                _logScrollBar.IsDisposed || _logScrollBar.IsDragging) return;

            if (!TryGetScrollMetrics(txtLog, _logScrollBar.Height, out var totalLines,
                    out var visibleLines, out var firstVisible))
            {
                _logScrollBar.Visible = false;
                return;
            }

            var trackHeight = _logScrollBar.Height;
            var thumbHeight = Math.Max(28, trackHeight * visibleLines / totalLines);
            var maxThumbTop = Math.Max(1, trackHeight - thumbHeight);
            var maxFirstVisible = Math.Max(1, totalLines - visibleLines);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, firstVisible * maxThumbTop / maxFirstVisible));
            _logScrollBar.SetThumb(thumbTop, thumbHeight);
        }

        private void RevealLogScrollBar(bool autoHide)
        {
            if (_logScrollBar == null || _logScrollBar.IsDisposed) return;

            UpdateLogScrollBar();
            if (!LogNeedsScroll()) return;

            _logScrollBar.Visible = true;
            _logScrollBar.BringToFront();
            _logScrollHideTimer?.Stop();
            if (autoHide) _logScrollHideTimer?.Start();
        }

        private void ScheduleLogScrollBarHide()
        {
            if (_logScrollBar == null || _logScrollBar.IsDisposed) return;
            if (_logScrollBar.IsDragging) return;

            _logScrollHideTimer?.Stop();
            _logScrollHideTimer?.Start();
        }

        private bool LogNeedsScroll()
        {
            return _logScrollBar != null &&
                   TryGetScrollMetrics(txtLog, _logScrollBar.Height, out _, out _, out _);
        }

        private void ScrollLogToTrackTop(int requestedTop)
        {
            if (txtLog.IsDisposed || _logScrollBar == null || _logScrollBar.IsDisposed) return;
            if (!TryGetScrollMetrics(txtLog, _logScrollBar.Height, out var totalLines,
                    out var visibleLines, out _))
            {
                UpdateLogScrollBar();
                return;
            }

            var maxThumbTop = Math.Max(1, _logScrollBar.Height - _logScrollBar.ThumbHeight);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, requestedTop));
            if (thumbTop >= maxThumbTop - 1)
            {
                ScrollRichTextBoxToEnd(txtLog);
                _logScrollBar.SetThumb(maxThumbTop, _logScrollBar.ThumbHeight);
                return;
            }

            var targetFirstLine = thumbTop * Math.Max(1, totalLines - visibleLines) / maxThumbTop;
            var currentFirstLine = txtLog.GetLineFromCharIndex(txtLog.GetCharIndexFromPosition(new Point(1, 1)));
            ScrollLogLines(targetFirstLine - currentFirstLine);
            _logScrollBar.SetThumb(thumbTop, _logScrollBar.ThumbHeight);
            RevealLogScrollBar(autoHide: false);
        }

        private void HandleLogMouseWheel(object sender, MouseEventArgs e)
        {
            var notches = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;
            var lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            ScrollLogLines(-notches * lines);
            UpdateLogScrollBar();
            RevealLogScrollBar(autoHide: true);
        }

        private void ScrollLogLines(int lineDelta)
        {
            if (lineDelta == 0 || txtLog.IsDisposed) return;
            SendMessage(txtLog.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(lineDelta));
        }

        private static bool TryGetScrollMetrics(RichTextBox textBox, int trackHeight,
            out int totalLines, out int visibleLines, out int firstVisible)
        {
            if (trackHeight <= 0)
            {
                totalLines = 1;
                visibleLines = 1;
                firstVisible = 0;
                return false;
            }

            totalLines = textBox.IsHandleCreated
                ? Math.Max(1, SendMessage(textBox.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero).ToInt32())
                : Math.Max(1, textBox.GetLineFromCharIndex(textBox.TextLength) + 1);
            visibleLines = Math.Max(1, (textBox.ClientSize.Height - 4) / Math.Max(1, textBox.Font.Height));
            firstVisible = textBox.IsHandleCreated
                ? Math.Max(0, SendMessage(textBox.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero).ToInt32())
                : textBox.GetLineFromCharIndex(textBox.GetCharIndexFromPosition(new Point(1, 1)));
            firstVisible = Math.Min(Math.Max(0, totalLines - visibleLines), firstVisible);
            return totalLines > visibleLines;
        }

        private static bool IsMouseInside(Control control)
        {
            return control != null &&
                   !control.IsDisposed &&
                   control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position);
        }

        private void UpdateChangelogScrollBar()
        {
            if (txtChangelog.IsDisposed || _updateChangelogScrollBar == null ||
                _updateChangelogScrollBar.IsDisposed || _updateChangelogScrollBar.IsDragging) return;

            if (!TryGetScrollMetrics(txtChangelog, _updateChangelogScrollBar.Height, out var totalLines,
                    out var visibleLines, out var firstVisible))
            {
                _updateChangelogScrollBar.Visible = false;
                return;
            }

            var trackHeight = _updateChangelogScrollBar.Height;
            var thumbHeight = Math.Max(28, trackHeight * visibleLines / totalLines);
            var maxThumbTop = Math.Max(1, trackHeight - thumbHeight);
            var maxFirstVisible = Math.Max(1, totalLines - visibleLines);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, firstVisible * maxThumbTop / maxFirstVisible));
            _updateChangelogScrollBar.SetThumb(thumbTop, thumbHeight);
        }

        private void RevealUpdateScrollBar(bool autoHide)
        {
            if (_updateChangelogScrollBar == null || _updateChangelogScrollBar.IsDisposed) return;

            UpdateChangelogScrollBar();
            if (!UpdateChangelogNeedsScroll()) return;

            _updateChangelogScrollBar.Visible = true;
            _updateChangelogScrollBar.BringToFront();
            _updateScrollHideTimer?.Stop();
            if (autoHide) _updateScrollHideTimer?.Start();
        }

        private void ScheduleUpdateScrollBarHide()
        {
            if (_updateChangelogScrollBar == null || _updateChangelogScrollBar.IsDisposed) return;
            if (_updateChangelogScrollBar.IsDragging) return;

            _updateScrollHideTimer?.Stop();
            _updateScrollHideTimer?.Start();
        }

        private bool UpdateChangelogNeedsScroll()
        {
            return _updateChangelogScrollBar != null &&
                   TryGetScrollMetrics(txtChangelog, _updateChangelogScrollBar.Height, out _, out _, out _);
        }

        private void ScrollUpdateChangelogToTrackTop(int requestedTop)
        {
            if (txtChangelog.IsDisposed || _updateChangelogScrollBar == null || _updateChangelogScrollBar.IsDisposed) return;

            if (!TryGetScrollMetrics(txtChangelog, _updateChangelogScrollBar.Height, out var totalLines,
                    out var visibleLines, out _))
            {
                UpdateChangelogScrollBar();
                return;
            }

            var maxThumbTop = Math.Max(1, _updateChangelogScrollBar.Height - _updateChangelogScrollBar.ThumbHeight);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, requestedTop));
            if (thumbTop >= maxThumbTop - 1)
            {
                ScrollRichTextBoxToEnd(txtChangelog);
                _updateChangelogScrollBar.SetThumb(maxThumbTop, _updateChangelogScrollBar.ThumbHeight);
                return;
            }

            var targetFirstLine = thumbTop * Math.Max(1, totalLines - visibleLines) / maxThumbTop;
            var currentFirstLine = txtChangelog.GetLineFromCharIndex(txtChangelog.GetCharIndexFromPosition(new Point(1, 1)));
            ScrollUpdateChangelogLines(targetFirstLine - currentFirstLine);
            _updateChangelogScrollBar.SetThumb(thumbTop, _updateChangelogScrollBar.ThumbHeight);
        }

        private void HandleUpdateChangelogMouseWheel(object sender, MouseEventArgs e)
        {
            var notches = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;
            var lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            ScrollUpdateChangelogLines(-notches * lines);
            UpdateChangelogScrollBar();
            RevealUpdateScrollBar(autoHide: true);
        }

        private void ScrollUpdateChangelogLines(int lineDelta)
        {
            if (lineDelta == 0 || txtChangelog.IsDisposed) return;
            SendMessage(txtChangelog.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(lineDelta));
        }

        private static void ScrollRichTextBoxToEnd(RichTextBox textBox)
        {
            if (textBox.IsDisposed) return;

            SendMessage(textBox.Handle, WmVScroll, new IntPtr(SbBottom), IntPtr.Zero);
        }

        private void BindUpdatePanel()
        {
            btnCheckUpdate.Click += async (s, e) =>
            {
                try { await CheckUpdateAsync(); }
                catch (Exception ex) { ShowChangelog(AntdUI.Localization.Get("App.Update.ErrorPrefix", "发生意外错误：") + ex.Message); }
            };
            btnDownloadSetup.Click += async (s, e) =>
            {
                try { await DownloadAsync(isSetup: true); }
                catch (Exception ex) { lblDownloadStatus.Text = AntdUI.Localization.Get("App.Update.DownloadErrorPrefix", "错误：") + ex.Message; }
            };
            btnDownloadPortable.Click += async (s, e) =>
            {
                try { await DownloadAsync(isSetup: false); }
                catch (Exception ex) { lblDownloadStatus.Text = AntdUI.Localization.Get("App.Update.DownloadErrorPrefix", "错误：") + ex.Message; }
            };
            btnFallback.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(UpdateHelper.FallbackUrl))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = UpdateHelper.FallbackUrl,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        System.Diagnostics.Process.Start("explorer.exe", UpdateHelper.FallbackUrl);
                    }
                }
            };
        }

        private async Task CheckUpdateAsync()
        {
            btnCheckUpdate.Text = AntdUI.Localization.Get("App.Update.Checking", "检查中...");

            btnCheckUpdate.Enabled = false;
            try
            {
                var state = await UpdateHelper.CheckAndPersistAsync(System.Windows.Forms.Application.ProductVersion);
                var info = UpdateHelper.ToUpdateInfo(state);
                if (info == null)
                {
                    ShowChangelog(AntdUI.Localization.Get("App.Update.CheckFailed", "检查失败，请检查网络连接。"));
                    lblLatestVersion.Text = "—";
                    _updateAutoOption.Text = "发布日期：检查失败";
                    _updateNotifyOption.Text = "更新大小：检查失败";
                    panelUpdateButtons.Visible = false;
                    return;
                }

                _updateInfo = info;
                lblLatestVersion.Text = "v" + info.LatestVersion;
                _updateAutoOption.Text = "发布日期：" + FormatReleaseDate(info.PublishedAt);
                _updateNotifyOption.Text = "更新大小：" + FormatReleaseSize(info);

                var currentVer = System.Windows.Forms.Application.ProductVersion;
                if (state.HasUpdate && UpdateHelper.IsNewer(currentVer, info.LatestVersion))
                {
                    ShowChangelog(info.Changelog);
                    panelUpdateButtons.Visible = true;
                    btnDownloadSetup.Visible    = true;
                    btnDownloadPortable.Visible = true;
                    btnFallback.Visible         = false;
                    progressDownload.Visible    = false;
                    progressDownload.Value      = 0F;
                    lblDownloadStatus.Text      = "";
                }
                else
                {
                    ShowChangelog("当前已是最新版本。");
                    panelUpdateButtons.Visible = false;
                }
            }
            finally
            {
                btnCheckUpdate.Text    = AntdUI.Localization.Get("App.Update.CheckUpdate", "检查更新");
                btnCheckUpdate.Enabled = true;
            }
        }

        private async Task DownloadAsync(bool isSetup)
        {
            if (_updateInfo == null) return;

            string url = isSetup ? _updateInfo.SetupDownloadUrl : _updateInfo.PortableDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                ShowFallback();
                return;
            }

            string destPath;
            if (!isSetup)
            {
                var sfd = new System.Windows.Forms.SaveFileDialog
                {
                    Title            = AntdUI.Localization.Get("App.Update.SavePortableTitle", "保存便携版"),
                    FileName         = $"XelLauncher.v{_updateInfo.LatestVersion}-Portable.zip",
                    Filter           = AntdUI.Localization.Get("App.Update.SavePortableFilter", "ZIP 压缩包|*.zip"),
                    DefaultExt       = "zip",
                    RestoreDirectory = true
                };
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                destPath = sfd.FileName;
            }
            else
            {
                var tmpDir = Path.Combine(Path.GetTempPath(), "XelLauncher_Update");
                destPath = Path.Combine(tmpDir,
                    $"XelLauncher-{_updateInfo.LatestVersion}-Setup.exe");
                Directory.CreateDirectory(tmpDir);
            }

            btnDownloadSetup.Enabled    = false;
            btnDownloadPortable.Enabled = false;
            progressDownload.Visible    = true;
            progressDownload.Value      = 0F;
            lblDownloadStatus.Text      = AntdUI.Localization.Get("App.Update.Preparing", "准备下载...");

            _downloadCts = new CancellationTokenSource();

            try
            {
                await UpdateHelper.DownloadAsync(url, destPath,
                    (pct, downloaded, total) =>
                    {
                        if (!IsHandleCreated) return;
                        Invoke(() =>
                        {
                            if (pct >= 0)
                            {
                                progressDownload.Value = pct / 100F;
                                var dlMB    = downloaded / 1048576.0;
                                var totalMB = total / 1048576.0;
                                lblDownloadStatus.Text = $"{dlMB:F1} MB / {totalMB:F1} MB  {pct}%";
                            }
                            else
                            {
                                var dlMB = downloaded / 1048576.0;
                                lblDownloadStatus.Text = string.Format(AntdUI.Localization.Get("App.Update.DownloadedMB", "{0:F1} MB 已下载"), dlMB);
                            }
                        });
                    },
                    _downloadCts.Token);

                if (isSetup)
                {
                    var batDir  = Path.Combine(Path.GetTempPath(), "XelLauncher_Update");
                    var batPath = Path.Combine(batDir, "update.bat");
                    Directory.CreateDirectory(batDir);
                    File.WriteAllText(batPath,
                        "@echo off\r\n" +
                        "TIMEOUT /T 2 /NOBREAK >nul\r\n" +
                        $"start \"\" \"{destPath}\"\r\n" +
                        "exit\r\n");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        Arguments       = $"/c \"{batPath}\"",
                        WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                        UseShellExecute = true
                    });
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    lblDownloadStatus.Text = AntdUI.Localization.Get("App.Update.DownloadDone", "下载完成！");
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"/select,\"{destPath}\"");
                }
            }
            catch (OperationCanceledException)
            {
                lblDownloadStatus.Text = AntdUI.Localization.Get("App.Update.DownloadCanceled", "已取消");
            }
            catch (Exception)
            {
                lblDownloadStatus.Text = AntdUI.Localization.Get("App.Update.DownloadFailed", "下载失败");
                ShowFallback();
            }
            finally
            {
                btnDownloadSetup.Enabled    = true;
                btnDownloadPortable.Enabled = true;
            }
        }

        private void ShowFallback()
        {
            btnDownloadSetup.Visible    = false;
            btnDownloadPortable.Visible = false;
            btnFallback.Visible         = true;
        }

        private void ShowChangelog(string text)
        {
            txtChangelog.Text = FormatUpdateChangelog(text);
            txtChangelog.SelectionStart = 0;
            txtChangelog.ScrollToCaret();
            UpdateChangelogScrollBar();
            _updateHeaderCard?.BringToFront();
            panelUpdateButtons?.BringToFront();
        }

        private void HideChangelog()
        {
            txtChangelog.Text = "\u5f53\u524d\u5df2\u662f\u6700\u65b0\u7248\u672c\u3002";
            UpdateChangelogScrollBar();
        }
        private static string FormatReleaseDate(DateTimeOffset? publishedAt)
        {
            return publishedAt.HasValue
                ? publishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "未知";
        }

        private static string FormatReleaseSize(UpdateInfo info)
        {
            var setup = FormatBytes(info.SetupSizeBytes);
            var portable = FormatBytes(info.PortableSizeBytes);

            if (setup != "未知" && portable != "未知")
                return $"安装包 {setup} / 便携包 {portable}";
            if (setup != "未知")
                return setup;
            if (portable != "未知")
                return portable;
            return "未知";
        }

        private static string FormatBytes(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value <= 0) return "未知";

            var value = bytes.Value;
            if (value >= 1024L * 1024L * 1024L)
                return $"{value / 1073741824.0:F2} GB";
            if (value >= 1024L * 1024L)
                return $"{value / 1048576.0:F1} MB";
            if (value >= 1024L)
                return $"{value / 1024.0:F1} KB";
            return $"{value} B";
        }

        private static string FormatUpdateChangelog(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "暂无更新内容。";

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                var item = line.Trim().TrimStart('-', '*', '•').Trim();
                if (item.Length == 0) continue;
                if (result.Length > 0) result.AppendLine();
                result.Append("• ").Append(item);
            }
            return result.Length > 0 ? result.ToString() : "暂无更新内容。";
        }

        private sealed class ThinScrollBar : Control
        {
            private readonly Color _trackColor;
            private readonly Color _thumbColor;
            private int _thumbTop;
            private int _thumbHeight;
            private int _dragStartY;
            private int _dragStartTop;

            public event Action<int> ScrollRequested;
            public event Action DragEnded;
            public bool IsDragging { get; private set; }
            public int ThumbHeight => _thumbHeight;

            public ThinScrollBar(Color trackColor, Color thumbColor)
            {
                _trackColor = trackColor;
                _thumbColor = thumbColor;
                _thumbHeight = 42;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            public void SetThumb(int top, int height)
            {
                var clampedHeight = Math.Max(12, Math.Min(Height, height));
                var clampedTop = Math.Max(0, Math.Min(Math.Max(0, Height - clampedHeight), top));
                if (_thumbTop == clampedTop && _thumbHeight == clampedHeight) return;

                _thumbTop = clampedTop;
                _thumbHeight = clampedHeight;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var trackPath = RoundedPanel.CreateRoundRectPath(new Rectangle(2, 0, 4, Height), 2))
                using (var trackFill = new SolidBrush(_trackColor))
                    e.Graphics.FillPath(trackFill, trackPath);

                using var thumbPath = RoundedPanel.CreateRoundRectPath(new Rectangle(2, _thumbTop, 4, _thumbHeight), 2);
                using var thumbFill = new SolidBrush(_thumbColor);
                e.Graphics.FillPath(thumbFill, thumbPath);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                var thumbRect = new Rectangle(0, _thumbTop, Width, _thumbHeight);
                if (!thumbRect.Contains(e.Location))
                    ScrollRequested?.Invoke(e.Y - _thumbHeight / 2);

                IsDragging = true;
                _dragStartY = Cursor.Position.Y;
                _dragStartTop = _thumbTop;
                Capture = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!IsDragging) return;
                ScrollRequested?.Invoke(_dragStartTop + Cursor.Position.Y - _dragStartY);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                IsDragging = false;
                Capture = false;
                DragEnded?.Invoke();
            }
        }
        private sealed class RoundedPanel : Panel
        {
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public int Radius { get; set; } = 8;
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color FillColor { get; set; } = Color.White;
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color BorderColor { get; set; } = Color.FromArgb(214, 221, 232);
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color AccentColor { get; set; } = Color.FromArgb(22, 119, 255);
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public bool ShowAccent { get; set; }

            public RoundedPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = CreateRoundRectPath(new Rectangle(1, 1, Width - 3, Height - 3), Radius);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(BorderColor, 1F);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);

                if (!ShowAccent) return;

                using var accent = new Pen(Color.FromArgb(190, AccentColor), 1.5F);
                e.Graphics.DrawLine(accent, 14, 1, Math.Min(Width - 15, 140), 1);
                using var glow = new SolidBrush(Color.FromArgb(28, AccentColor));
                e.Graphics.FillEllipse(glow, Width - 42, 16, 18, 18);
            }

            public static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                var diameter = radius * 2;
                var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

                path.AddArc(arc, 180, 90);
                arc.X = rect.Right - diameter;
                path.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - diameter;
                path.AddArc(arc, 0, 90);
                arc.X = rect.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
