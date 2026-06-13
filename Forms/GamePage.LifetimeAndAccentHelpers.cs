using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;
namespace XelLauncher.Forms
{
    public partial class GamePage : UserControl
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _coverCts.Cancel(); } catch { }
                _coverCts.Dispose();
                _leftTooltip?.Dispose();
                _topTooltip?.Dispose();
                _bottomTooltip?.Dispose();
            }
            base.Dispose(disposing);
        }

        // Account management button handler.
        private void btnAccountManage_Click(object sender, EventArgs e)
        {
            var form = new AccountManagerForm(_overview, this, _game.IconName);
            AntdUI.Modal.open(new AntdUI.Modal.Config(_overview, AntdUI.Localization.Get("App.Game.AccountManage", "账号管理"), form)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
                CloseIcon = true,
                MaskClosable = true,
            });
        }

        private static Color PickCoverAccentColor(Image image)
        {
            if (image == null) return Color.FromArgb(190, 160, 126);

            try
            {
                using var bitmap = new Bitmap(image);
                int step = Math.Max(4, Math.Min(bitmap.Width, bitmap.Height) / 90);
                var buckets = new Dictionary<int, ColorBucket>();

                int startY = bitmap.Height / 4;
                for (int y = startY; y < bitmap.Height; y += step)
                {
                    for (int x = 0; x < bitmap.Width; x += step)
                    {
                        var color = bitmap.GetPixel(x, y);
                        if (color.A < 48) continue;

                        float saturation = color.GetSaturation();
                        float brightness = color.GetBrightness();
                        if (saturation < 0.12F || brightness < 0.22F || brightness > 0.86F) continue;

                        float hue = color.GetHue();
                        float warmDistance = Math.Min(Math.Abs(hue - 34F), 360F - Math.Abs(hue - 34F));
                        float warmBias = Math.Max(0F, 1F - warmDistance / 58F);
                        float mutedBias = 1F - Math.Min(1F, Math.Abs(saturation - 0.36F) / 0.42F);
                        float centerBias = 1F - Math.Min(1F, Math.Abs(brightness - 0.56F) / 0.44F);
                        float lowerBias = 0.75F + 0.25F * y / bitmap.Height;
                        float rightBias = x > bitmap.Width * 0.55F ? 1.12F : 1F;
                        float saturatedPenalty = saturation > 0.68F ? 0.52F : 1F;
                        float score = (0.35F + warmBias * 2.4F + mutedBias * 0.95F + centerBias * 0.65F)
                            * lowerBias * rightBias * saturatedPenalty;

                        int key = (color.R / 32 << 10) | (color.G / 32 << 5) | (color.B / 32);
                        buckets.TryGetValue(key, out var bucket);
                        bucket.Add(color, score);
                        buckets[key] = bucket;
                    }
                }

                if (buckets.Count == 0) return Color.FromArgb(190, 160, 126);

                var best = buckets.Values
                    .OrderByDescending(x => x.Score * Math.Sqrt(Math.Max(1, x.Count)))
                    .First();
                return best.ToColor();
            }
            catch
            {
                return Color.FromArgb(190, 160, 126);
            }
        }

        private static Color NormalizeAccent(Color color, float lightness, float minSaturation, float maxSaturation)
        {
            RgbToHsl(color, out var h, out var s, out _);
            s = Math.Max(minSaturation, Math.Min(maxSaturation, s));
            return HslToRgb(h, s, lightness);
        }

        private static Color ShiftHue(Color color, float degrees, float saturation, float lightness)
        {
            RgbToHsl(color, out var h, out var s, out _);
            h = (h + degrees) % 360F;
            if (h < 0) h += 360F;
            return HslToRgb(h, Math.Max(saturation, s), lightness);
        }

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

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private struct ColorBucket
        {
            private double _r;
            private double _g;
            private double _b;
            private double _weight;

            public int Count { get; private set; }
            public double Score { get; private set; }

            public void Add(Color color, double score)
            {
                Count++;
                Score += score;
                _weight += score;
                _r += color.R * score;
                _g += color.G * score;
                _b += color.B * score;
            }

            public Color ToColor()
            {
                if (_weight <= 0) return Color.FromArgb(190, 160, 126);
                return Color.FromArgb(
                    Math.Max(0, Math.Min(255, (int)Math.Round(_r / _weight))),
                    Math.Max(0, Math.Min(255, (int)Math.Round(_g / _weight))),
                    Math.Max(0, Math.Min(255, (int)Math.Round(_b / _weight))));
            }
        }
    }
}
