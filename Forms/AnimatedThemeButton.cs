using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    internal sealed class AnimatedThemeButton : AntdUI.Button
    {
        private const float DesignWidth = 274F;
        private const float DesignHeight = 110F;
        private const int DurationMs = 260;
        private const int IdleFrameCycle = 2400;
        private readonly Timer _timer;
        private readonly Stopwatch _watch = new();
        private bool _iconIsDark;
        private bool _fromDark;
        private bool _toDark;
        private float _progress = 1F;
        private int _idleFrame;

        public AnimatedThemeButton()
        {
            Text = string.Empty;
            Padding = Padding.Empty;
            BorderWidth = 0;
            WaveSize = 0;
            Ghost = true;
            _timer = new Timer { Interval = AnimationFrameHelper.GetFrameInterval(this) };
            _timer.Tick += Timer_Tick;
        }

        public void SetDarkIcon(bool isDark)
        {
            _timer.Stop();
            _watch.Reset();
            _iconIsDark = isDark;
            _fromDark = isDark;
            _toDark = isDark;
            _progress = 1F;
            Invalidate();
        }

        public void AnimateToDarkIcon(bool isDark)
        {
            _fromDark = _iconIsDark;
            _toDark = isDark;
            _progress = 0F;
            _watch.Restart();
            UpdateTimer();
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateTimer();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            UpdateTimer();
        }

        protected override void OnDraw(AntdUI.DrawEventArgs e)
        {
            if (e.Graphics == null) return;

            Color backColor = Parent != null ? Parent.BackColor : BackColor ?? Color.Transparent;
            using (var back = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(back, ClientRectangle);
            }
            DrawThemeIcon(e.Graphics, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer.Dispose();
                _watch.Stop();
            }
            base.Dispose(disposing);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _idleFrame = (_idleFrame + 1) % IdleFrameCycle;

            if (_watch.IsRunning)
            {
                _progress = Math.Min(1F, _watch.ElapsedMilliseconds / (float)DurationMs);
                if (_progress >= 1F)
                {
                    _watch.Reset();
                    _iconIsDark = _toDark;
                }
            }

            UpdateTimer();
            Invalidate();
        }

        private void UpdateTimer()
        {
            bool shouldRun = Visible && Parent != null && !IsDisposed;
            if (shouldRun)
            {
                AnimationFrameHelper.ApplyFrameInterval(_timer, this);
                if (!_timer.Enabled) _timer.Start();
            }
            else if (_timer.Enabled)
            {
                _timer.Stop();
            }
        }

        private void DrawThemeIcon(Graphics g, Rectangle bounds)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float eased = LinearEase(_progress);
            float darkProgress = _iconIsDark ? 1F : 0F;
            if (_progress < 1F && _fromDark != _toDark)
                darkProgress = _toDark ? eased : 1F - eased;

            if (!Enabled) darkProgress *= 0.65F;

            float scale = Math.Min(bounds.Width / DesignWidth, bounds.Height / DesignHeight);
            float dx = bounds.Left + (bounds.Width - DesignWidth * scale) / 2F;
            float dy = bounds.Top + (bounds.Height - DesignHeight * scale) / 2F;
            g.TranslateTransform(dx, dy);
            g.ScaleTransform(scale, scale);

            DrawReplicaSwitch(g, darkProgress, _idleFrame);

            g.Restore(state);
        }

        private static void DrawReplicaSwitch(Graphics g, float darkProgress, int frame)
        {
            var track = new RectangleF(6F, 7F, 262F, 96F);
            using var path = RoundPath(track, 48F);

            DrawReplicaShadow(g, track);
            DrawTrackBackground(g, track, path, darkProgress);

            var clip = g.Save();
            g.SetClip(path);
            DrawReplicaDayBands(g, track, 1F - darkProgress);
            DrawReplicaStars(g, track, darkProgress, frame);
            DrawReplicaClouds(g, track, 1F - darkProgress, frame);
            DrawReplicaNightMist(g, track, darkProgress);
            g.Restore(clip);

            DrawReplicaChrome(g, track, path);

            float knob = 74F;
            float knobPadding = 14F;
            float knobX = track.Left + knobPadding + (track.Width - knob - knobPadding * 2F) * darkProgress;
            float knobY = track.Top + (track.Height - knob) / 2F;
            var knobRect = new RectangleF(knobX, knobY, knob, knob);
            DrawReplicaKnobShadow(g, knobRect, darkProgress);
            DrawReplicaSun(g, knobRect, 1F - darkProgress);
            DrawReplicaMoon(g, knobRect, darkProgress);
        }

        private static void DrawReplicaShadow(Graphics g, RectangleF track)
        {
            using var dropPath = RoundPath(new RectangleF(track.X + 2F, track.Y + 7F, track.Width - 1F, track.Height), 44F);
            using var drop = new PathGradientBrush(dropPath)
            {
                CenterColor = Color.FromArgb(38, 70, 78, 92),
                SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) }
            };
            g.FillPath(drop, dropPath);
        }

        private static void DrawTrackBackground(Graphics g, RectangleF track, GraphicsPath path, float darkProgress)
        {
            using var bg = new LinearGradientBrush(
                track,
                Blend(Color.FromArgb(141, 181, 222), Color.FromArgb(26, 30, 54), darkProgress),
                Blend(Color.FromArgb(68, 132, 191), Color.FromArgb(7, 10, 25), darkProgress),
                LinearGradientMode.Vertical);
            g.FillPath(bg, path);

            using var leftShade = new LinearGradientBrush(
                track,
                Color.FromArgb(88, 42, 68, 103),
                Color.FromArgb(0, 42, 68, 103),
                LinearGradientMode.Horizontal);
            g.FillPath(leftShade, path);
        }

        private static void DrawReplicaDayBands(Graphics g, RectangleF track, float alpha)
        {
            if (alpha <= 0F) return;

            using var lightBand = new SolidBrush(Color.FromArgb(ClampAlpha(36 * alpha), 197, 219, 244));
            using var midBand = new SolidBrush(Color.FromArgb(ClampAlpha(28 * alpha), 56, 125, 188));

            g.FillEllipse(lightBand, 78F, -32F, 116F, 170F);
            g.FillEllipse(midBand, 126F, -36F, 118F, 176F);
            g.FillEllipse(midBand, 172F, -38F, 116F, 180F);

            using var gloss = new LinearGradientBrush(
                new RectangleF(track.Left, track.Top + 2F, track.Width, 34F),
                Color.FromArgb(ClampAlpha(64 * alpha), Color.White),
                Color.FromArgb(0, Color.White),
                LinearGradientMode.Vertical);
            g.FillRectangle(gloss, track.Left + 10F, track.Top + 1F, track.Width - 18F, 34F);
        }

        private static void DrawReplicaClouds(Graphics g, RectangleF track, float alpha, int frame)
        {
            if (alpha <= 0F) return;

            float drift = (float)Math.Sin(frame * Math.PI / 120D) * 2F;
            using var rearShadow = new SolidBrush(Color.FromArgb(ClampAlpha(58 * alpha), 120, 132, 157));
            using var rear = new SolidBrush(Color.FromArgb(ClampAlpha(224 * alpha), 222, 235, 251));
            using var frontShadow = new SolidBrush(Color.FromArgb(ClampAlpha(58 * alpha), 164, 170, 184));
            using var front = new SolidBrush(Color.FromArgb(ClampAlpha(252 * alpha), Color.White));

            DrawReplicaRearCloudBank(g, rearShadow, 112F + drift, 74F + 3F);
            DrawReplicaRearCloudBank(g, rear, 110F + drift, 74F);
            DrawReplicaRightCloud(g, rearShadow, 210F - drift * 0.3F, 45F + 2F);
            DrawReplicaRightCloud(g, rear, 208F - drift * 0.3F, 45F);
            DrawReplicaFrontCloudBank(g, frontShadow, 92F - drift * 0.35F, 91F + 2F);
            DrawReplicaFrontCloudBank(g, front, 90F - drift * 0.35F, 89F);
        }

        private static void DrawReplicaRearCloudBank(Graphics g, Brush brush, float x, float y)
        {
            g.FillEllipse(brush, x, y - 19F, 52F, 42F);
            g.FillEllipse(brush, x + 43F, y - 12F, 44F, 36F);
            g.FillEllipse(brush, x + 78F, y - 24F, 56F, 50F);
            g.FillRectangle(brush, x - 6F, y + 7F, 150F, 25F);
        }

        private static void DrawReplicaRightCloud(Graphics g, Brush brush, float x, float y)
        {
            g.FillEllipse(brush, x, y - 10F, 58F, 52F);
            g.FillEllipse(brush, x + 31F, y - 31F, 70F, 68F);
            g.FillEllipse(brush, x + 78F, y - 18F, 58F, 56F);
            g.FillRectangle(brush, x + 4F, y + 18F, 125F, 38F);
        }

        private static void DrawReplicaFrontCloudBank(Graphics g, Brush brush, float x, float y)
        {
            g.FillEllipse(brush, x, y - 12F, 34F, 26F);
            g.FillEllipse(brush, x + 25F, y - 22F, 48F, 40F);
            g.FillEllipse(brush, x + 66F, y - 12F, 36F, 28F);
            g.FillRectangle(brush, x + 10F, y + 4F, 112F, 19F);
        }

        private static void DrawReplicaStars(Graphics g, RectangleF track, float alpha, int frame)
        {
            if (alpha <= 0F) return;

            foreach (var star in _stars)
            {
                float pulse = Twinkle(frame, star.Phase, star.Period, star.FlashDuration);
                int a = ClampAlpha(alpha * (star.BaseAlpha + star.BlinkAlpha * pulse));
                if (a <= 0) continue;

                using var brush = new SolidBrush(Color.FromArgb(a, Color.White));
                if (star.Cross)
                {
                    DrawStar(g, brush, star.X, star.Y, star.Size);
                }
                else
                {
                    float size = star.Size + pulse * 0.8F;
                    g.FillEllipse(brush, star.X - size / 2F, star.Y - size / 2F, size, size);
                }
            }

            DrawShootingStar(g, alpha, frame, 420);
            DrawShootingStar(g, alpha, frame, 1620);
        }

        private static readonly TwinkleStar[] _stars =
        {
            new(36F, 31F, 4.2F, 106F, 132F, 17, 620, 34, false),
            new(57F, 49F, 5.4F, 84F, 155F, 211, 760, 38, true),
            new(78F, 24F, 3.8F, 92F, 125F, 457, 680, 30, false),
            new(96F, 72F, 4.1F, 76F, 144F, 97, 920, 36, false),
            new(115F, 45F, 5.8F, 118F, 138F, 349, 840, 40, true),
            new(134F, 28F, 3.7F, 80F, 118F, 541, 980, 32, false),
            new(153F, 61F, 4.4F, 96F, 128F, 689, 720, 34, false),
            new(174F, 38F, 5.1F, 104F, 142F, 803, 1040, 42, true),
            new(196F, 74F, 4.1F, 76F, 116F, 127, 880, 30, false),
            new(213F, 30F, 3.9F, 88F, 124F, 611, 800, 34, false),
            new(235F, 56F, 4.5F, 72F, 134F, 293, 1120, 38, false),
        };

        private readonly struct TwinkleStar
        {
            public TwinkleStar(float x, float y, float size, float baseAlpha, float blinkAlpha, int phase, int period, int flashDuration, bool cross)
            {
                X = x;
                Y = y;
                Size = size;
                BaseAlpha = baseAlpha;
                BlinkAlpha = blinkAlpha;
                Phase = phase;
                Period = period;
                FlashDuration = flashDuration;
                Cross = cross;
            }

            public float X { get; }
            public float Y { get; }
            public float Size { get; }
            public float BaseAlpha { get; }
            public float BlinkAlpha { get; }
            public int Phase { get; }
            public int Period { get; }
            public int FlashDuration { get; }
            public bool Cross { get; }
        }

        private static float Twinkle(int frame, int phase, int period, int flashDuration)
        {
            int value = (frame + phase) % period;
            if (value > flashDuration) return 0F;

            float center = flashDuration * 0.5F;
            float distance = Math.Abs(value - center) / center;
            float pulse = Math.Max(0F, 1F - distance);
            return EaseInOut(pulse);
        }

        private static void DrawShootingStar(Graphics g, float alpha, int frame, int startFrame)
        {
            const int duration = 58;
            int value = (frame - startFrame + IdleFrameCycle) % IdleFrameCycle;
            if (value < 0 || value > duration) return;

            float progress = value / (float)duration;
            float fade = (float)Math.Sin(progress * Math.PI);
            int lineAlpha = ClampAlpha(alpha * 210F * fade);
            if (lineAlpha <= 0) return;

            float headX = 46F + progress * 126F;
            float headY = 22F + progress * 38F;
            float tailX = headX - 42F;
            float tailY = headY - 15F;

            using var tail = new LinearGradientBrush(
                new PointF(tailX, tailY),
                new PointF(headX, headY),
                Color.FromArgb(0, Color.White),
                Color.FromArgb(lineAlpha, Color.White));
            using var pen = new Pen(tail, 3F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, tailX, tailY, headX, headY);

            using var head = new SolidBrush(Color.FromArgb(ClampAlpha(alpha * 235F * fade), Color.White));
            g.FillEllipse(head, headX - 2.2F, headY - 2.2F, 4.4F, 4.4F);
        }

        private static void DrawStar(Graphics g, Brush brush, float cx, float cy, float size)
        {
            using var path = new GraphicsPath();
            path.AddLine(cx, cy - size, cx + size * 0.28F, cy - size * 0.28F);
            path.AddLine(cx + size, cy, cx + size * 0.28F, cy + size * 0.28F);
            path.AddLine(cx, cy + size, cx - size * 0.28F, cy + size * 0.28F);
            path.AddLine(cx - size, cy, cx - size * 0.28F, cy - size * 0.28F);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        private static void DrawReplicaNightMist(Graphics g, RectangleF track, float alpha)
        {
            if (alpha <= 0F) return;
            using var mist = new SolidBrush(Color.FromArgb(ClampAlpha(36 * alpha), 82, 94, 136));
            g.FillEllipse(mist, 80F, 68F, 70F, 35F);
            g.FillEllipse(mist, 142F, 72F, 88F, 40F);
            g.FillRectangle(mist, 70F, 88F, 160F, 30F);
        }

        private static void DrawReplicaKnobShadow(Graphics g, RectangleF knob, float darkProgress)
        {
            float xOffset = 7F - 2F * darkProgress;
            using var path = EllipsePath(new RectangleF(knob.X + xOffset, knob.Y + 7F, knob.Width + 7F, knob.Height + 7F));
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(100, 0, 0, 0),
                SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) }
            };
            g.FillPath(brush, path);
        }

        private static void DrawReplicaSun(Graphics g, RectangleF knob, float alpha)
        {
            if (alpha <= 0F) return;
            int a = ClampAlpha(255 * alpha);
            using var brush = new LinearGradientBrush(
                knob,
                Color.FromArgb(a, 255, 227, 56),
                Color.FromArgb(a, 241, 193, 41),
                LinearGradientMode.Vertical);
            g.FillEllipse(brush, knob);

            using var inner = new Pen(Color.FromArgb(ClampAlpha(52 * alpha), 255, 246, 150), 3.5F);
            g.DrawEllipse(inner, knob.X + 4F, knob.Y + 4F, knob.Width - 8F, knob.Height - 8F);
            using var rim = new Pen(Color.FromArgb(ClampAlpha(58 * alpha), 124, 95, 26), 2F);
            g.DrawEllipse(rim, knob);
        }

        private static void DrawReplicaMoon(Graphics g, RectangleF knob, float alpha)
        {
            if (alpha <= 0F) return;
            int a = ClampAlpha(255 * alpha);
            using var brush = new LinearGradientBrush(
                knob,
                Color.FromArgb(a, 245, 247, 255),
                Color.FromArgb(a, 176, 190, 220),
                LinearGradientMode.ForwardDiagonal);
            g.FillEllipse(brush, knob);

            DrawMoonCrater(g, knob, alpha, 0.34F, 0.27F, 8.8F);
            DrawMoonCrater(g, knob, alpha, 0.58F, 0.54F, 7.2F);
            DrawMoonCrater(g, knob, alpha, 0.30F, 0.64F, 5.8F);
            DrawMoonCrater(g, knob, alpha, 0.62F, 0.25F, 4.6F);

            using var rim = new Pen(Color.FromArgb(ClampAlpha(92 * alpha), Color.White), 1F);
            g.DrawEllipse(rim, knob);
        }

        private static void DrawMoonCrater(Graphics g, RectangleF knob, float alpha, float xRatio, float yRatio, float size)
        {
            float x = knob.Left + knob.Width * xRatio - size / 2F;
            float y = knob.Top + knob.Height * yRatio - size / 2F;
            var craterRect = new RectangleF(x, y, size, size);

            using var shade = new SolidBrush(Color.FromArgb(ClampAlpha(120 * alpha), 134, 145, 178));
            g.FillEllipse(shade, craterRect);

            using var highlight = new Pen(Color.FromArgb(ClampAlpha(76 * alpha), 252, 253, 255), 1.2F);
            g.DrawArc(highlight, x + 1.1F, y + 1.1F, size - 2.2F, size - 2.2F, 205F, 118F);

            using var inner = new Pen(Color.FromArgb(ClampAlpha(44 * alpha), 92, 105, 142), 1F);
            g.DrawArc(inner, x + 0.8F, y + 0.8F, size - 1.6F, size - 1.6F, 25F, 130F);
        }

        private static GraphicsPath RoundPath(RectangleF rect, float radius)
        {
            float d = radius * 2F;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath EllipsePath(RectangleF rect)
        {
            var path = new GraphicsPath();
            path.AddEllipse(rect);
            return path;
        }

        private static void DrawReplicaChrome(Graphics g, RectangleF track, GraphicsPath path)
        {
            using var outer = new Pen(Color.FromArgb(118, 78, 103, 132), 3.4F);
            g.DrawPath(outer, path);

            using var top = new Pen(Color.FromArgb(130, Color.White), 2F);
            var inset = new RectangleF(track.X + 4F, track.Y + 4F, track.Width - 8F, track.Height - 8F);
            using var insetPath = RoundPath(inset, inset.Height / 2F);
            g.DrawPath(top, insetPath);
        }

        private static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)Math.Round(from.A + (to.A - from.A) * amount),
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        private static int ClampAlpha(float value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private static float LinearEase(float value)
        {
            value = Math.Max(0F, Math.Min(1F, value));
            const float softness = 0.12F;
            return value * (1F - softness) + EaseInOut(value) * softness;
        }

        private static float EaseOutCubic(float value)
        {
            float t = 1F - value;
            return 1F - t * t * t;
        }

        private static float EaseInOut(float value)
        {
            value = Math.Max(0F, Math.Min(1F, value));
            return value * value * (3F - 2F * value);
        }
    }
}
