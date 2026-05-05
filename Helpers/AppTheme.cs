using System.Drawing;

namespace XelLauncher.Helpers
{
    internal static class AppTheme
    {
        public static readonly Color LightBackground = Color.White;
        public static readonly Color LightForeground = Color.FromArgb(24, 28, 34);
        public static readonly Color LightBorder = Color.FromArgb(218, 224, 231);

        public static readonly Color DarkBackground = Color.FromArgb(23, 26, 31);
        public static readonly Color DarkHeader = Color.FromArgb(27, 31, 37);
        public static readonly Color DarkSurface = Color.FromArgb(31, 36, 43);
        public static readonly Color DarkSurfaceHover = Color.FromArgb(42, 48, 57);
        public static readonly Color DarkSurfaceActive = Color.FromArgb(48, 55, 65);
        public static readonly Color DarkBorder = Color.FromArgb(62, 70, 82);
        public static readonly Color DarkForeground = Color.FromArgb(232, 237, 243);
        public static readonly Color DarkForegroundSecondary = Color.FromArgb(176, 184, 196);

        public const string DarkLaunchPanel = "#252B34";
    }
}
