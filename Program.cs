using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

            if (args.Any(arg => string.Equals(arg, "--skport-auto-sign", StringComparison.OrdinalIgnoreCase)))
            {
                SkportAutoSignCommandRunner.Run();
                return;
            }

            if (!IsWindows10Version22H2OrLater())
            {
                MessageBox.Show(
                    "XelLauncher 需要 Windows 10 22H2（内部版本 19045）或更高版本。\r\n\r\n请升级系统后再运行。",
                    "系统版本不受支持",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
            if (cfg.RunAsAdministrator && !IsRunningAsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath!,
                        Arguments = string.Join(" ", args.Select(QuoteArgument)),
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    return;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Continue normally if the user declines the UAC prompt.
                }
            }
            var lang = !string.IsNullOrEmpty(cfg.Language)
                ? cfg.Language
                : AntdUI.Localization.CurrentLanguage;
            AntdUI.Localization.Provider = new Localizer();
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

        static bool IsWindows10Version22H2OrLater()
        {
            return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045);
        }

        private static bool IsRunningAsAdministrator()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private static string QuoteArgument(string argument) =>
            string.IsNullOrEmpty(argument) || argument.Any(char.IsWhiteSpace) || argument.Contains('"')
                ? '"' + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + '"'
                : argument;

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

        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public static void TrimMemory()
        {
            try
            {
                SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }
    }
}
