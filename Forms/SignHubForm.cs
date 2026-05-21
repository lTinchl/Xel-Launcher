using System;
using System.Drawing;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public class SignHubForm : UserControl
    {
        private const int FormWidth = 1040;
        private const int HeaderHeight = 58;
        private const int ContentHeight = 662;
        private const int MarginSize = 28;

        private readonly Panel _contentHost;
        private readonly AntdUI.Button _btnSkyland;
        private readonly AntdUI.Button _btnSkport;
        private readonly Panel _tabUnderline;
        private readonly SkylandSignForm _skylandSignForm;
        private readonly SkportSignForm _skportSignForm;
        private int _activeTab;

        public SignHubForm(Overview overview, int initialTab = 0)
        {
            var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.FromArgb(250, 252, 255);
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);

            Font = new Font("Microsoft YaHei UI", 10F);
            Size = new Size(FormWidth, HeaderHeight + ContentHeight);
            MinimumSize = Size;
            BackColor = surface;

            var header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(FormWidth, HeaderHeight),
                BackColor = surface,
            };

            var title = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.SignHub.Title", "签到"),
                Location = new Point(MarginSize, 8),
                Size = new Size(160, 38),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = normalText,
            };

            _btnSkyland = CreateTabButton(AntdUI.Localization.Get("App.Skyland.Title", "Skyland Sign"));
            _btnSkport = CreateTabButton(AntdUI.Localization.Get("App.Skport.Title", "SKPORT Sign"));
            _btnSkyland.Location = new Point(190, 13);
            _btnSkyland.Size = new Size(118, 32);
            _btnSkport.Location = new Point(330, 13);
            _btnSkport.Size = new Size(112, 32);
            _btnSkyland.Click += (s, e) => ShowTab(0);
            _btnSkport.Click += (s, e) => ShowTab(1);

            _tabUnderline = new Panel
            {
                BackColor = Color.FromArgb(22, 119, 255),
                Size = new Size(42, 3),
            };

            var btnClose = new AntdUI.Button
            {
                IconSvg = "CloseOutlined",
                Location = new Point(FormWidth - MarginSize - 36, 10),
                Size = new Size(36, 36),
                Ghost = true,
                Radius = 6,
                BorderWidth = 0,
                WaveSize = 0,
            };
            btnClose.Click += (s, e) => FindForm()?.Close();

            header.Controls.Add(title);
            header.Controls.Add(_btnSkyland);
            header.Controls.Add(_btnSkport);
            header.Controls.Add(_tabUnderline);
            header.Controls.Add(btnClose);
            Controls.Add(header);

            _contentHost = new Panel
            {
                Location = new Point(0, HeaderHeight),
                Size = new Size(FormWidth, ContentHeight),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = surface,
            };
            Controls.Add(_contentHost);

            _skylandSignForm = new SkylandSignForm(overview, embedded: true)
            {
                Dock = DockStyle.Fill,
            };
            _skportSignForm = new SkportSignForm(overview, embedded: true)
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };

            _contentHost.Controls.Add(_skylandSignForm);
            _contentHost.Controls.Add(_skportSignForm);
            ShowTab(initialTab == 1 ? 1 : 0);
        }

        private static AntdUI.Button CreateTabButton(string text)
        {
            return new AntdUI.Button
            {
                Text = text,
                Ghost = true,
                BorderWidth = 0,
                Radius = 6,
                BackHover = Color.Transparent,
                BackActive = Color.Transparent,
                WaveSize = 0,
                TabStop = false,
                Font = new Font("Microsoft YaHei UI", 10F),
            };
        }

        private void ShowTab(int tab)
        {
            _activeTab = tab;
            _skylandSignForm.Visible = tab == 0;
            _skportSignForm.Visible = tab == 1;

            if (tab == 0)
                _skylandSignForm.BringToFront();
            else
                _skportSignForm.BringToFront();

            UpdateTabState();
        }

        private void UpdateTabState()
        {
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
            var activeButton = _activeTab == 0 ? _btnSkyland : _btnSkport;

            _btnSkyland.ForeColor = _activeTab == 0 ? normalText : subtleText;
            _btnSkport.ForeColor = _activeTab == 1 ? normalText : subtleText;
            _btnSkyland.Font = new Font("Microsoft YaHei UI", 10F, _activeTab == 0 ? FontStyle.Bold : FontStyle.Regular);
            _btnSkport.Font = new Font("Microsoft YaHei UI", 10F, _activeTab == 1 ? FontStyle.Bold : FontStyle.Regular);

            var textWidth = TextRenderer.MeasureText(activeButton.Text ?? string.Empty, activeButton.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            var underlineWidth = Math.Max(28, Math.Min(activeButton.Width - 20, (int)(textWidth * 0.72F)));
            _tabUnderline.Bounds = new Rectangle(
                activeButton.Left + (activeButton.Width - underlineWidth) / 2,
                activeButton.Bottom + 6,
                underlineWidth,
                3);
            _tabUnderline.BringToFront();
        }
    }
}
