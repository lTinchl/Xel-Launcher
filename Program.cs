using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Forms;
using XelLauncher.Helpers;

namespace XelLauncher
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arge)
        {
            Application.ThreadException += (s, e) =>
            LogHelper.LogError(e.Exception, "UI ThreadException");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    LogHelper.LogError(ex, "UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                LogHelper.LogError(ex.Exception, "UnobservedTaskException");
                ex.SetObserved();
            };
            try
            {
                var version = Microsoft.Web.WebView2.Core.CoreWebView2Environment
                    .GetAvailableBrowserVersionString();
            }
            catch
            {
                MessageBox.Show("检测到系统未安装 WebView2 Runtime，请安装后再运行！");
                return;
            }
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#if !NET10_0
            //ComWrappers.RegisterForMarshalling(WinFormsComInterop.WinFormsComWrappers.Instance);
#endif
            var command = string.Join(" ", arge);
            AntdUI.Localization.DefaultLanguage = "zh-CN";
            var cfg = ConfigHelper.Load();
            // 优先使用用户保存的语言，否则跟随系统语言
            string lang = !string.IsNullOrEmpty(cfg.Language)
                ? cfg.Language
                : AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
            AntdUI.Localization.SetLanguage(lang);
            AntdUI.Config.Theme().Dark("#000", "#fff").Light("#fff", "#000").FormBorderColor();
            // 根据用户保存的主题模式决定深色/浅色，"system" 则跟随系统注册表
            AntdUI.Config.IsDark = cfg.ThemeMode switch
            {
                "dark"  => true,
                "light" => false,
                _       => IsSystemDarkMode()   // "system" 或其他旧值
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
            else
            {
                var overview = new Overview(command == "t");
                overview.Load += async (s, e) =>
                {
                    try
                    {
                        await Task.Delay(3000); // 等待主界面完全渲染
                        await CheckUpdateSilentAsync(overview);
                    }
                    catch (Exception ex)
                    {
                        Helpers.LogHelper.LogError(ex, "Overview.Load silent update");
                    }
                };
                Application.Run(overview);
            }
        }

        /// <summary>
        /// 读取注册表判断系统是否处于深色模式（AppsUseLightTheme=0 即深色）
        /// </summary>
        static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                var val = key?.GetValue("AppsUseLightTheme");
                return val is int i && i == 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// 静默检查更新：有新版本且未通知过同一版本时弹出通知。
        /// </summary>
        static async Task CheckUpdateSilentAsync(AntdUI.Window owner)
        {
            try
            {
                var info = await Helpers.UpdateHelper.CheckAsync();
                if (info == null) return;

                var cfg = Helpers.ConfigHelper.Load();
                var currentVer = System.Windows.Forms.Application.ProductVersion;
                if (!Helpers.UpdateHelper.IsNewer(currentVer, info.LatestVersion)) return;
                if (cfg.LastNotifiedVersion == info.LatestVersion) return;

                // 更新已通知版本，避免重复弹出
                cfg.LastNotifiedVersion = info.LatestVersion;
                Helpers.ConfigHelper.Save(cfg);

                owner.Invoke(() =>
                {
                    AntdUI.Notification.open(new AntdUI.Notification.Config(new AntdUI.Target(owner),
                        AntdUI.Localization.Get("App.Update.NewVersionTitle", "发现新版本"),
                        "v" + info.LatestVersion,
                        AntdUI.TType.Info,
                        AntdUI.TAlignFrom.TL)
                    {
                        AutoClose = 6
                    });
                });
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.LogError(ex, "CheckUpdateSilentAsync");
            }
        }
    }
}