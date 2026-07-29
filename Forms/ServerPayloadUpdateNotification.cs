using System;
using System.Drawing;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    internal enum ServerPayloadNotificationState
    {
        Running,
        Success,
        Warning,
        Error
    }

    internal sealed class ServerPayloadUpdateNotification : UserControl
    {
        private static readonly Color GlassSurface =
            Color.FromArgb(160, 34, 37, 43);
        private static readonly Color GlassBorder =
            Color.FromArgb(26, 255, 255, 255);
        private static readonly Color PrimaryText =
            Color.FromArgb(245, 255, 255, 255);
        private static readonly Color SecondaryText =
            Color.FromArgb(172, 207, 219, 230);

        private readonly AntdUI.Panel _card;
        private readonly AntdUI.Label _statusGlyph;
        private readonly AntdUI.Label _title;
        private readonly AntdUI.Label _detail;
        private readonly AntdUI.Progress _progress;
        private readonly AntdUI.Label _progressPercent;
        private readonly AntdUI.Button _closeButton;
        private readonly Timer _autoCloseTimer;

        public ServerPayloadUpdateNotification()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);

            BackColor = Color.Transparent;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(380, 126);
            MinimumSize = Size;
            MaximumSize = Size;

            _card = new AntdUI.Panel
            {
                Dock = DockStyle.Fill,
                Radius = 10,
                Shadow = 0,
                ShadowOpacity = 0F,
                BackColor = Color.Transparent,
                Back = GlassSurface,
                BorderWidth = 1F,
                BorderColor = GlassBorder,
            };

            _statusGlyph = new AntdUI.Label
            {
                Text = "●",
                Location = new Point(22, 17),
                Size = new Size(16, 24),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            };

            _title = new AntdUI.Label
            {
                Location = new Point(44, 14),
                Size = new Size(286, 28),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            };

            _detail = new AntdUI.Label
            {
                Location = new Point(44, 45),
                Size = new Size(306, 36),
                Font = new Font("Microsoft YaHei UI", 8.5F),
            };

            _progress = new AntdUI.Progress
            {
                Location = new Point(44, 91),
                Size = new Size(278, 8),
                Value = 0F,
                Loading = true,
                BackColor = Color.Transparent,
                Back = Color.FromArgb(48, 255, 255, 255),
                Fill = Color.FromArgb(218, 123, 207, 211),
                ForeColor = PrimaryText,
                UseSystemText = true,
                // AntdUI suppresses the entire track when system text is empty.
                Text = " ",
            };

            _progressPercent = new AntdUI.Label
            {
                Text = "0%",
                Location = new Point(328, 82),
                Size = new Size(36, 26),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = PrimaryText,
                TextAlign = ContentAlignment.MiddleRight,
            };

            _closeButton = new AntdUI.Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(335, 14),
                Size = new Size(28, 28),
                IconSvg = "CloseOutlined",
                Ghost = true,
                BorderWidth = 0,
                Radius = 6,
                WaveSize = 0,
            };
            _closeButton.Click += (_, _) =>
                DismissRequested?.Invoke(this, EventArgs.Empty);

            _card.Controls.Add(_statusGlyph);
            _card.Controls.Add(_title);
            _card.Controls.Add(_detail);
            _card.Controls.Add(_progress);
            _card.Controls.Add(_progressPercent);
            _card.Controls.Add(_closeButton);
            Controls.Add(_card);

            _autoCloseTimer = new Timer();
            _autoCloseTimer.Tick += (_, _) =>
            {
                _autoCloseTimer.Stop();
                AutoCloseElapsed?.Invoke(this, EventArgs.Empty);
            };

            ApplyState(ServerPayloadNotificationState.Running);
        }

        public event EventHandler DismissRequested;
        public event EventHandler AutoCloseElapsed;

        public void ShowProgress(string title, string detail, float value, bool loading)
        {
            StopAutoClose();
            ApplyTheme();
            ApplyState(ServerPayloadNotificationState.Running);
            _title.Text = title;
            _detail.Text = detail;
            _progress.Loading = loading;
            var progress = Math.Clamp(value, 0F, 1F);
            _progress.Value = progress;
            _progressPercent.Text =
                $"{(int)Math.Round(progress * 100F, MidpointRounding.AwayFromZero)}%";
        }

        public void ShowResult(
            string title,
            string detail,
            ServerPayloadNotificationState state,
            int autoCloseSeconds)
        {
            ApplyTheme();
            ApplyState(state);
            _title.Text = title;
            _detail.Text = detail;
            _progress.Loading = false;
            _progress.Value = 1F;
            _progressPercent.Text = "100%";

            _autoCloseTimer.Stop();
            _autoCloseTimer.Interval = Math.Max(1, autoCloseSeconds) * 1000;
            _autoCloseTimer.Start();
        }

        private void ApplyTheme()
        {
            _card.Back = GlassSurface;
            _card.BorderColor = GlassBorder;
            _title.ForeColor = PrimaryText;
            _detail.ForeColor = SecondaryText;
            _progressPercent.ForeColor = PrimaryText;
            _closeButton.ForeColor = SecondaryText;
        }

        private void ApplyState(ServerPayloadNotificationState state)
        {
            _statusGlyph.ForeColor = state switch
            {
                ServerPayloadNotificationState.Success => Color.FromArgb(82, 196, 26),
                ServerPayloadNotificationState.Warning => Color.FromArgb(250, 173, 20),
                ServerPayloadNotificationState.Error => Color.FromArgb(255, 77, 79),
                _ => AntdUI.Style.Db.Primary,
            };
        }

        private void StopAutoClose()
        {
            if (_autoCloseTimer.Enabled)
                _autoCloseTimer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoCloseTimer.Stop();
                _autoCloseTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
