using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    internal sealed class ThemeSurfacePanel : Panel
    {
        private Color _surfaceColor = Color.Transparent;

        public ThemeSurfacePanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SurfaceColor
        {
            get => _surfaceColor;
            set
            {
                if (_surfaceColor.ToArgb() == value.ToArgb()) return;
                _surfaceColor = value;
                base.BackColor = value;
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(_surfaceColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }
}
