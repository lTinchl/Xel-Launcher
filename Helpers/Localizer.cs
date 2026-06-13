using System.Globalization;
using System.Resources;

namespace XelLauncher.Helpers
{
    public class Localizer : AntdUI.ILocalization
    {
        private static readonly ResourceManager Strings = new(
            "XelLauncher.Resources.i18n.Strings",
            typeof(Localizer).Assembly);

        public string GetLocalizedString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var language = AntdUI.Localization.CurrentLanguage;
            CultureInfo culture;
            try
            {
                culture = string.IsNullOrWhiteSpace(language)
                    ? CultureInfo.CurrentUICulture
                    : CultureInfo.GetCultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentUICulture;
            }

            return Strings.GetString(key, culture);
        }
    }
}
