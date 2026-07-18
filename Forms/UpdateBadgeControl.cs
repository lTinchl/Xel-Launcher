using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    /// <summary>
    /// 显示在版本号右上角的软件更新动态徽标。
    /// </summary>
    public class UpdateBadgeControl : Control
    {
        private const int PulseDurationMs = 1200;
        private static readonly Color BadgeColor = Color.FromArgb(255, 77, 79);
        private System.Windows.Forms.Timer _pulseTimer;
        private Stopwatch _pulseWatch;

        public UpdateBadgeControl()
        {
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Size = new Size(18, 18);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float centerX = Width / 2F;
            float centerY = Height / 2F;
            float coreDiameter = Math.Min(8F, Math.Min(Width, Height) * 0.46F);
            float maxHaloDiameter = Math.Max(coreDiameter, Math.Min(Width, Height) - 1F);

            double cycleProgress = AntdUI.Config.Animation && _pulseWatch?.IsRunning == true
                ? (_pulseWatch.Elapsed.TotalMilliseconds % PulseDurationMs) / PulseDurationMs
                : 0D;
            double easedProgress = cycleProgress * cycleProgress * (3D - 2D * cycleProgress);
            float haloDiameter = coreDiameter +
                (maxHaloDiameter - coreDiameter) * (float)easedProgress;
            int haloAlpha = (int)Math.Round(78D * (1D - easedProgress));

            if (haloAlpha > 0)
            {
                using var haloBrush = new SolidBrush(Color.FromArgb(haloAlpha, BadgeColor));
                g.FillEllipse(
                    haloBrush,
                    centerX - haloDiameter / 2F,
                    centerY - haloDiameter / 2F,
                    haloDiameter,
                    haloDiameter);
            }

            using var coreBrush = new SolidBrush(BadgeColor);
            g.FillEllipse(
                coreBrush,
                centerX - coreDiameter / 2F,
                centerY - coreDiameter / 2F,
                coreDiameter,
                coreDiameter);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdatePulseAnimationState();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdatePulseAnimationState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopPulseAnimation();
                _pulseTimer?.Dispose();
                _pulseTimer = null;
                _pulseWatch = null;
            }
            base.Dispose(disposing);
        }

        public void RefreshAnimationState()
        {
            UpdatePulseAnimationState();
        }

        private void UpdatePulseAnimationState()
        {
            if (!Visible || !IsHandleCreated || IsDisposed || !AntdUI.Config.Animation)
            {
                StopPulseAnimation();
                Invalidate();
                return;
            }

            _pulseWatch ??= new Stopwatch();
            _pulseWatch.Restart();
            if (_pulseTimer == null)
            {
                _pulseTimer = new System.Windows.Forms.Timer();
                _pulseTimer.Tick += PulseTimer_Tick;
            }
            AnimationFrameHelper.ApplyFrameInterval(_pulseTimer, this);
            _pulseTimer.Start();
        }

        private void PulseTimer_Tick(object sender, EventArgs e)
        {
            if (!Visible || IsDisposed || !AntdUI.Config.Animation)
            {
                StopPulseAnimation();
                return;
            }

            Invalidate();
        }

        private void StopPulseAnimation()
        {
            _pulseTimer?.Stop();
            _pulseWatch?.Stop();
        }
    }
}
