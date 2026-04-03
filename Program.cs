using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Forms;

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
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#if !NET10_0
            ComWrappers.RegisterForMarshalling(WinFormsComInterop.WinFormsComWrappers.Instance);
#endif
            var command = string.Join(" ", arge);
            AntdUI.Localization.DefaultLanguage = "zh-CN";
            var lang = AntdUI.Localization.CurrentLanguage;
            if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
            AntdUI.Config.Theme().Dark("#000", "#fff").Light("#fff", "#000").FormBorderColor();
            AntdUI.Config.TextRenderingHighQuality = true;
            AntdUI.Config.ShowInWindow = true;
            AntdUI.Config.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            AntdUI.Config.SetEmptyImageSvg(Properties.Resources.icon_empty, Properties.Resources.icon_empty_dark);
            AntdUI.SvgDb.Emoji = AntdUI.FluentFlat.Emoji;
            var cfg = ConfigHelper.Load();
            if (!string.IsNullOrEmpty(cfg.PrimaryColor))
                AntdUI.Style.SetPrimary(System.Drawing.ColorTranslator.FromHtml(cfg.PrimaryColor));
            if (command == "m") Application.Run(new Main());
            else if (command == "tab") Application.Run(new TabHeaderForm());
            else Application.Run(new Overview(command == "t"));
        }
    }
}