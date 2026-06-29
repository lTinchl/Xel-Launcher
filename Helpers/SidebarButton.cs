using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    class SidebarButton : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public System.Drawing.Bitmap GameIcon { get; set; }
        private bool _selected;
        private bool _hovered;
        private readonly Timer _animationTimer;
        private readonly Stopwatch _animationWatch = new();
        private float _selectedProgress;
        private float _hoverProgress;
        private float _selectedFrom;
        private float _hoverFrom;
        private Color _accentColor = Color.FromArgb(64, 128, 255);

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool ShowSelectionBar { get; set; } = true;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color AccentColor
        {
            get => _accentColor;
            set
            {
                if (_accentColor.ToArgb() == value.ToArgb()) return;
                _accentColor = value;
                Invalidate();
            }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                StartVisualAnimation();
            }
        }

        public SidebarButton()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = System.Drawing.Color.Transparent;
            _animationTimer = new Timer { Interval = AnimationFrameHelper.GetFrameInterval(this) };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            StartVisualAnimation();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            StartVisualAnimation();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = DeviceDpi / 96F;

            float active = Math.Max(_selectedProgress, _hoverProgress * 0.72F);

            if (active > 0.001F)
            {
                float inset = ScaleDpi(2F, scale) * (1F - active);
                float frameWidth = ScaleDpi(60F, scale);
                float frameHeight = ScaleDpi(54F, scale);
                var rect = new RectangleF(
                    (Width - frameWidth) / 2F + inset,
                    (Height - frameHeight) / 2F + inset,
                    frameWidth - inset * 2F,
                    frameHeight - inset * 2F);
                int radius = ScaleDpi(9, scale);
                var color = _selectedProgress >= _hoverProgress
                    ? _accentColor
                    : Color.FromArgb(128, 128, 128);
                int fillAlpha = (int)Math.Round(8 + 34 * _selectedProgress + 14 * _hoverProgress);
                int borderAlpha = (int)Math.Round(36 + 132 * _selectedProgress + 42 * _hoverProgress);
                float borderWidth = 1F + _selectedProgress * 0.8F;

                using var brush = new SolidBrush(Color.FromArgb(Clamp(fillAlpha, 0, 76), color));
                using var path = RoundRect(rect, radius);
                g.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(Clamp(borderAlpha, 0, 168), _accentColor), borderWidth);
                g.DrawPath(pen, path);
            }

            if (ShowSelectionBar && _selectedProgress > 0.001F)
            {
                float barHeight = ScaleDpi(40F + 8F * _selectedProgress, scale);
                float barWidth = ScaleDpi(2F + 1.5F * _selectedProgress, scale);
                var bar = new RectangleF(ScaleDpi(6F, scale), (Height - barHeight) / 2F, barWidth, barHeight);
                using var brush = new SolidBrush(Color.FromArgb((int)Math.Round(190 * _selectedProgress), _accentColor));
                using var path = RoundRect(bar, ScaleDpi(2, scale));
                g.FillPath(brush, path);
            }

            if (GameIcon != null)
            {
                int iconSize = Math.Min(ScaleDpi(44, scale), Math.Min(Width, Height) - ScaleDpi(12, scale));
                int ix = (Width - iconSize) / 2;
                int iy = (Height - iconSize) / 2;
                g.DrawImage(GameIcon, new Rectangle(ix, iy, iconSize, iconSize));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Tick -= AnimationTimer_Tick;
                _animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void StartVisualAnimation()
        {
            _selectedFrom = _selectedProgress;
            _hoverFrom = _hoverProgress;
            _animationWatch.Restart();
            AnimationFrameHelper.ApplyFrameInterval(_animationTimer, this);
            if (!_animationTimer.Enabled)
                _animationTimer.Start();
            Invalidate();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            const double duration = 150D;
            double progress = Math.Min(1D, _animationWatch.Elapsed.TotalMilliseconds / duration);
            float eased = (float)(1D - Math.Pow(1D - progress, 3D));

            _selectedProgress = Lerp(_selectedFrom, _selected ? 1F : 0F, eased);
            _hoverProgress = Lerp(_hoverFrom, _hovered ? 1F : 0F, eased);
            Invalidate();

            if (progress < 1D) return;

            _selectedProgress = _selected ? 1F : 0F;
            _hoverProgress = _hovered ? 1F : 0F;
            _animationTimer.Stop();
            _animationWatch.Stop();
        }

        private static float Lerp(float from, float to, float progress)
        {
            return from + (to - from) * progress;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static int ScaleDpi(int value, float scale) =>
            Math.Max(1, (int)Math.Round(value * scale));

        private static float ScaleDpi(float value, float scale) =>
            value * scale;

        private static GraphicsPath RoundRect(RectangleF r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class SidebarSelectionIndicator : Control
    {
        private Color _accentColor = Color.FromArgb(64, 128, 255);

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color AccentColor
        {
            get => _accentColor;
            set
            {
                if (_accentColor.ToArgb() == value.ToArgb()) return;
                _accentColor = value;
                Invalidate();
            }
        }

        public SidebarSelectionIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(210, _accentColor));
            using var path = RoundRect(new RectangleF(0, 0, Width, Height), Math.Max(2, (int)Math.Round(DeviceDpi / 96F * 2F)));
            e.Graphics.FillPath(brush, path);
        }

        protected override void WndProc(ref Message m)
        {
            const int wmNcHitTest = 0x84;
            const int htTransparent = -1;

            base.WndProc(ref m);
            if (m.Msg == wmNcHitTest)
                m.Result = new IntPtr(htTransparent);
        }

        private static GraphicsPath RoundRect(RectangleF r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
