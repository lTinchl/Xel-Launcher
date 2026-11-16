using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    internal sealed class GameInfoBadgeControl : Control
    {
        private const int PanelHeight = 24;
        private int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96F));
        private int ScaledPanelHeight => Math.Max(S(PanelHeight), Font.Height + S(10));
        private const int HorizontalPadding = 9;
        private const int SectionGap = 7;
        private string _channelText = "";
        private string _versionText = "";
        private Color _accentColor = Color.FromArgb(48, 214, 238);
        private Font _channelFont;
        private float _transitionProgress;

        public GameInfoBadgeControl()
        {
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);
            _channelFont ??= new Font(Font, FontStyle.Bold);
            ResizeToContent();
            TabStop = false;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor
        {
            get => _accentColor;
            set
            {
                if (_accentColor == value) return;
                _accentColor = value;
                Invalidate();
            }
        }

        public void SetContent(string channelText, string versionText)
        {
            channelText ??= "";
            versionText ??= "";
            if (_channelText == channelText && _versionText == versionText) return;

            _channelText = channelText;
            _versionText = versionText;
            ResizeToContent();
            Invalidate();
        }

        public void SetTransitionProgress(float progress)
        {
            progress = Math.Max(0F, Math.Min(1F, progress));
            if (Math.Abs(_transitionProgress - progress) < 0.001F) return;

            _transitionProgress = progress;
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _channelFont?.Dispose();
            _channelFont = Font == null ? null : new Font(Font, FontStyle.Bold);
            ResizeToContent();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 0 || _channelFont == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var transitionState = g.Save();
            g.SetClip(ClientRectangle, CombineMode.Intersect);
            float transitionOffset = (float)Math.Round((ScaledPanelHeight + S(2)) * _transitionProgress);
            g.TranslateTransform(0F, -transitionOffset);

            try
            {
                var panelBounds = new RectangleF(0.5F, 0.5F, Width - 1F, ScaledPanelHeight - 1F);
                DrawAnnouncementSurface(g, panelBounds);

                SplitVersionText(_versionText, out string versionLabel, out string versionValue);
                float channelWidth = MeasureTextWidth(g, _channelText, _channelFont);
                float labelWidth = MeasureTextWidth(g, versionLabel, Font);
                float valueWidth = MeasureTextWidth(g, versionValue, Font);
                float textTop = panelBounds.Top;
                float textHeight = panelBounds.Height;
                float channelLeft = panelBounds.Left + S(HorizontalPadding);
                float separatorX = channelLeft + channelWidth + S(SectionGap);
                float versionLeft = separatorX + S(SectionGap) + S(1);

                using (var separatorPen = new Pen(Color.FromArgb(42, 255, 255, 255), 1F))
                    g.DrawLine(separatorPen, separatorX, panelBounds.Top + S(6), separatorX, panelBounds.Bottom - S(6));

                DrawLeftText(
                    g,
                    new RectangleF(channelLeft, textTop, channelWidth + 1F, textHeight),
                    _channelText,
                    _channelFont,
                    Color.FromArgb(245, _accentColor));

                DrawLeftText(
                    g,
                    new RectangleF(versionLeft, textTop, labelWidth + 1F, textHeight),
                    versionLabel,
                    Font,
                    Color.FromArgb(172, 207, 219, 230));
                DrawLeftText(
                    g,
                    new RectangleF(versionLeft + labelWidth, textTop - S(1), valueWidth + 1F, textHeight),
                    versionValue,
                    Font,
                    Color.FromArgb(224, 222, 235, 244));
            }
            finally
            {
                g.Restore(transitionState);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channelFont?.Dispose();
                Font?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ResizeToContent()
        {
            if (Font == null || _channelFont == null) return;

            SplitVersionText(_versionText, out string versionLabel, out string versionValue);
            using var g = CreateGraphics();
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float channelWidth = MeasureTextWidth(g, _channelText, _channelFont);
            float versionWidth = MeasureTextWidth(g, versionLabel, Font) + MeasureTextWidth(g, versionValue, Font);
            int width = (int)Math.Ceiling(S(HorizontalPadding) * 2 + channelWidth + S(SectionGap) * 2 + S(1) + versionWidth + 1);
            Size = new Size(width, ScaledPanelHeight + 1);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ResizeToContent();
            Invalidate();
        }
        private void DrawAnnouncementSurface(Graphics g, RectangleF bounds)
        {
            using var path = CreateRoundedRectangle(bounds, S(8));
            using var background = new SolidBrush(Color.FromArgb(100, 34, 37, 43));
            using var border = new Pen(Color.FromArgb(24, 255, 255, 255), 1F);
            g.FillPath(background, path);
            g.DrawPath(border, path);
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2F);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        private static void SplitVersionText(string text, out string label, out string value)
        {
            text ??= "";
            int separatorIndex = text.LastIndexOfAny(new[] { ':', '：' });
            if (separatorIndex < 0 || separatorIndex >= text.Length - 1)
            {
                label = "";
                value = text;
                return;
            }

            label = text.Substring(0, separatorIndex + 1);
            value = text.Substring(separatorIndex + 1);
        }

        private static void DrawLeftText(
            Graphics g,
            RectangleF bounds,
            string text,
            Font font,
            Color color)
        {
            using var brush = new SolidBrush(color);
            // Keep drawing consistent with MeasureTextWidth(Graphics, ...). The default
            // StringFormat has wider glyph padding than GenericTypographic, which made
            // exact-fit strings such as "Official" get ellipsized even when the badge
            // itself had enough room.
            using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming = StringTrimming.None;
            format.FormatFlags |= StringFormatFlags.NoWrap;
            g.DrawString(text, font, brush, bounds, format);
        }

        private static float MeasureTextWidth(Graphics g, string text, Font font)
        {
            using var format = StringFormat.GenericTypographic;
            return g.MeasureString(text ?? "", font, int.MaxValue, format).Width;
        }

    }
}
