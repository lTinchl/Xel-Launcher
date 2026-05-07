using System;
using System.Drawing;

namespace XelLauncher.Helpers
{
    internal static class GameTheme
    {
        public static Color GetAccent(string iconName)
        {
            return iconName switch
            {
                "Arknights" or "BiliArknights" => Color.FromArgb(190, 160, 126),
                "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield" => Color.FromArgb(176, 164, 114),
                _ => Color.FromArgb(190, 160, 126),
            };
        }

        public static Color GetAccentHover(string iconName) => ShiftLightness(GetAccent(iconName), 0.08F);

        public static Color GetAccentActive(string iconName) => ShiftLightness(GetAccent(iconName), -0.14F);

        private static Color ShiftLightness(Color color, float delta)
        {
            RgbToHsl(color, out var h, out var s, out var l);
            return HslToRgb(h, s, Math.Max(0.18F, Math.Min(0.72F, l + delta)));
        }

        private static void RgbToHsl(Color color, out float h, out float s, out float l)
        {
            float r = color.R / 255F;
            float g = color.G / 255F;
            float b = color.B / 255F;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2F;

            if (Math.Abs(max - min) < 0.0001F)
            {
                h = 0F;
                s = 0F;
                return;
            }

            float d = max - min;
            s = l > 0.5F ? d / (2F - max - min) : d / (max + min);
            if (Math.Abs(max - r) < 0.0001F)
                h = (g - b) / d + (g < b ? 6F : 0F);
            else if (Math.Abs(max - g) < 0.0001F)
                h = (b - r) / d + 2F;
            else
                h = (r - g) / d + 4F;
            h *= 60F;
        }

        private static Color HslToRgb(float h, float s, float l)
        {
            h = ((h % 360F) + 360F) % 360F / 360F;
            float r, g, b;

            if (s <= 0.0001F)
            {
                r = g = b = l;
            }
            else
            {
                float q = l < 0.5F ? l * (1F + s) : l + s - l * s;
                float p = 2F * l - q;
                r = HueToRgb(p, q, h + 1F / 3F);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1F / 3F);
            }

            return Color.FromArgb(
                Math.Max(0, Math.Min(255, (int)Math.Round(r * 255F))),
                Math.Max(0, Math.Min(255, (int)Math.Round(g * 255F))),
                Math.Max(0, Math.Min(255, (int)Math.Round(b * 255F))));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0F) t += 1F;
            if (t > 1F) t -= 1F;
            if (t < 1F / 6F) return p + (q - p) * 6F * t;
            if (t < 1F / 2F) return q;
            if (t < 2F / 3F) return p + (q - p) * (2F / 3F - t) * 6F;
            return p;
        }
    }
}
