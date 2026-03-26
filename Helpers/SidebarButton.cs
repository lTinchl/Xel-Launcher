using System;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    class SidebarButton : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public System.Drawing.Bitmap GameIcon { get; set; }
        private bool _selected;
        private bool _hovered;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        public SidebarButton()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = System.Drawing.Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int pad = 10;
            var rect = new System.Drawing.Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
            int radius = 10;

            if (_selected || _hovered)
            {
                var color = _selected
                    ? AntdUI.Style.Db.Primary
                    : System.Drawing.Color.FromArgb(40, 128, 128, 128);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(_selected ? 40 : 20, color));
                using var path = RoundRect(rect, radius);
                g.FillPath(brush, path);
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(_selected ? 180 : 60, AntdUI.Style.Db.Primary), _selected ? 2f : 1f);
                g.DrawPath(pen, path);
            }

            if (GameIcon != null)
            {
                int iconSize = 44;
                int ix = (Width - iconSize) / 2;
                int iy = (Height - iconSize) / 2;
                g.DrawImage(GameIcon, new System.Drawing.Rectangle(ix, iy, iconSize, iconSize));
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(System.Drawing.Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
