using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    internal static class AnimationFrameHelper
    {
        private const int Vrefresh = 116;
        private const int DefaultRefreshRate = 60;
        private const int MinRefreshRate = 30;
        private const int MaxRefreshRate = 240;
        private const int MinFrameIntervalMs = 4;
        private const int MaxFrameIntervalMs = 33;

        public static int GetFrameInterval(Control control = null)
        {
            int refreshRate = GetRefreshRate(control);
            refreshRate = Math.Max(MinRefreshRate, Math.Min(MaxRefreshRate, refreshRate));
            int interval = (int)Math.Round(1000D / refreshRate);
            return Math.Max(MinFrameIntervalMs, Math.Min(MaxFrameIntervalMs, interval));
        }

        public static void ApplyFrameInterval(Timer timer, Control control = null)
        {
            if (timer == null) return;
            timer.Interval = GetFrameInterval(control);
        }

        public static float ScaleEase(float baseEase, Timer timer, int baselineIntervalMs = 15)
        {
            baseEase = Math.Max(0F, Math.Min(1F, baseEase));
            int interval = Math.Max(1, timer?.Interval ?? baselineIntervalMs);
            double scale = interval / (double)Math.Max(1, baselineIntervalMs);
            return (float)(1D - Math.Pow(1D - baseEase, scale));
        }

        private static int GetRefreshRate(Control control)
        {
            try
            {
                using var graphics = control != null && control.IsHandleCreated && !control.IsDisposed
                    ? control.CreateGraphics()
                    : Graphics.FromHwnd(IntPtr.Zero);
                IntPtr hdc = graphics.GetHdc();
                try
                {
                    int refreshRate = GetDeviceCaps(hdc, Vrefresh);
                    return refreshRate > 1 ? refreshRate : DefaultRefreshRate;
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }
            catch
            {
                return DefaultRefreshRate;
            }
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    }
}
