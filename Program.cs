using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Forms;
using XelLauncher.Helpers;

namespace XelLauncher
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.ThreadException += (s, e) =>
                LogHelper.LogError(e.Exception, "UI ThreadException");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    LogHelper.LogError(ex, "UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogHelper.LogError(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };

            if (args.Any(arg => string.Equals(arg, "--skyland-auto-sign", StringComparison.OrdinalIgnoreCase)))
            {
                SkylandAutoSignCommandRunner.Run();
                return;
            }

            try
            {
                _ = Microsoft.Web.WebView2.Core.CoreWebView2Environment
                    .GetAvailableBrowserVersionString();
            }
            catch
            {
                MessageBox.Show(
                    "未检测到 Microsoft Edge WebView2 Runtime。\r\n\r\n请安装 WebView2 Runtime 后再运行 XelLauncher。",
                    "缺少 WebView2 Runtime",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#if !NET10_0
            //ComWrappers.RegisterForMarshalling(WinFormsComInterop.WinFormsComWrappers.Instance);
#endif
            var command = string.Join(" ", args);
            AntdUI.Localization.DefaultLanguage = "zh-CN";
            var cfg = ConfigHelper.Load();
            var lang = !string.IsNullOrEmpty(cfg.Language)
                ? cfg.Language
                : AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
            AntdUI.Localization.SetLanguage(lang);
            AntdUI.Config.Theme()
                .Dark(AppTheme.DarkBackground, AppTheme.DarkForeground)
                .Light(AppTheme.LightBackground, AppTheme.LightForeground)
                .FormBorderColor(AppTheme.LightBorder, AppTheme.DarkBorder);
            AntdUI.Config.IsDark = cfg.ThemeMode switch
            {
                "dark" => true,
                "light" => false,
                _ => IsSystemDarkMode()
            };
            AntdUI.Config.TextRenderingHighQuality = true;
            AntdUI.Config.ShowInWindow = true;
            AntdUI.Config.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            AntdUI.Config.SetEmptyImageSvg(Properties.Resources.icon_empty, Properties.Resources.icon_empty_dark);
            AntdUI.SvgDb.Emoji = AntdUI.FluentFlat.Emoji;
            if (!string.IsNullOrEmpty(cfg.PrimaryColor))
                AntdUI.Style.SetPrimary(System.Drawing.ColorTranslator.FromHtml(cfg.PrimaryColor));

            if (command == "m") Application.Run(new Main());
            else if (command == "tab") Application.Run(new TabHeaderForm());
            else Application.Run(new Overview(command == "t"));
        }

        static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                var val = key?.GetValue("AppsUseLightTheme");
                return val is int i && i == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
