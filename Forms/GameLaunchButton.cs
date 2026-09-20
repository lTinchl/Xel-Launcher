using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    internal sealed class GameLaunchButton : AntdUI.Button
    {
        private bool _runningAppearance;

        public void SetRunningAppearance(bool running)
        {
            if (_runningAppearance == running) return;
            _runningAppearance = running;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_runningAppearance)
            {
                base.OnPaint(e);
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ReadRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            int diameter = Math.Min(rect.Height, rect.Width);

            using var outline = new GraphicsPath();
            outline.AddArc(
                rect.Left,
                rect.Top,
                diameter,
                diameter,
                90,
                180);

            outline.AddArc(
                rect.Right - diameter,
                rect.Top,
                diameter,
                diameter,
                270,
                180);

            outline.CloseFigure();

            using var fill = new SolidBrush(Color.FromArgb(145, 145, 145));
            g.FillPath(fill, outline);

            var foreground = Color.FromArgb(40, 40, 40);

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                rect,
                foreground,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }
    }
}
