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
        private const int ControlHeight = 25;
        private const int HorizontalPadding = 9;
        private const int SectionGap = 7;
        private string _channelText = "";
        private string _versionText = "";
        private Color _accentColor = Color.FromArgb(48, 214, 238);
        private Font _channelFont;

        public GameInfoBadgeControl()
        {
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);
            _channelFont ??= new Font(Font, FontStyle.Bold);
            Height = ControlHeight;
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

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
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

            var panelBounds = new RectangleF(0.5F, 0.5F, Width - 1F, PanelHeight - 1F);
            DrawAnnouncementSurface(g, panelBounds);

            SplitVersionText(_versionText, out string versionLabel, out string versionValue);
            float channelWidth = MeasureTextWidth(g, _channelText, _channelFont);
            float labelWidth = MeasureTextWidth(g, versionLabel, Font);
            float valueWidth = MeasureTextWidth(g, versionValue, Font);
            float textTop = panelBounds.Top;
            float textHeight = panelBounds.Height;
            float channelLeft = panelBounds.Left + HorizontalPadding;
            float separatorX = channelLeft + channelWidth + SectionGap;
            float versionLeft = separatorX + SectionGap + 1F;

            DrawLeftText(
                g,
                new RectangleF(channelLeft, textTop, channelWidth + 1F, textHeight),
                _channelText,
                _channelFont,
                Color.FromArgb(245, _accentColor));

            using (var separatorPen = new Pen(Color.FromArgb(42, 255, 255, 255), 1F))
                g.DrawLine(separatorPen, separatorX, panelBounds.Top + 6F, separatorX, panelBounds.Bottom - 6F);

            DrawLeftText(
                g,
                new RectangleF(versionLeft, textTop, labelWidth + 1F, textHeight),
                versionLabel,
                Font,
                Color.FromArgb(172, 207, 219, 230));
            DrawLeftText(
                g,
                // Latin digits have a shorter visual cap height than the CJK label.
                // Lift them by one pixel so both text runs share the same optical center.
                new RectangleF(versionLeft + labelWidth, textTop - 1F, valueWidth + 1F, textHeight),
                versionValue,
                Font,
                Color.FromArgb(224, 222, 235, 244));
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
            int channelWidth = MeasureTextWidth(_channelText, _channelFont);
            int versionWidth = MeasureTextWidth(versionLabel, Font) + MeasureTextWidth(versionValue, Font);
            int width = HorizontalPadding * 2 + channelWidth + SectionGap * 2 + 1 + versionWidth;
            Size = new Size(Math.Max(142, width), ControlHeight);
        }

        private static void DrawAnnouncementSurface(Graphics g, RectangleF bounds)
        {
            using var path = CreateRoundedRectangle(bounds, 8F);
            using var background = new SolidBrush(Color.FromArgb(188, 34, 37, 43));
            using var border = new Pen(Color.FromArgb(34, 255, 255, 255), 1F);
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
            value = text.Substring(separatorIndex + 1).TrimStart();
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

        private static int MeasureTextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(
                text ?? "",
                font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        private static float MeasureTextWidth(Graphics g, string text, Font font)
        {
            using var format = StringFormat.GenericTypographic;
            return g.MeasureString(text ?? "", font, int.MaxValue, format).Width;
        }

    }
}
