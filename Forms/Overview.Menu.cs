using System;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private void btn_setting_Click(object sender, EventArgs e)
        {
            var setting = new Setting(this);
            AntdUI.Modal.open(new AntdUI.Modal.Config(this, AntdUI.Localization.Get("Setting", "设置"), setting)
            {
                BtnHeight = 0,
                CloseIcon = true,
            });
            AntdUI.Config.Animation = setting.Animation;
            AntdUI.Config.ShadowEnabled = setting.ShadowEnabled;
            AntdUI.Config.ShowInWindow = setting.ShowInWindow;
            AntdUI.Config.ScrollBarHide = setting.ScrollBarHide;
            if (AntdUI.Config.TextRenderingHighQuality != setting.TextRenderingHighQuality)
            {
                AntdUI.Config.TextRenderingHighQuality = setting.TextRenderingHighQuality;
                Refresh();
            }
            var cfg = ConfigHelper.Load();
            cfg.MinimizeToTray = setting.MinimizeToTray;
            cfg.CloseAfterLaunch = setting.CloseAfterLaunch;
            cfg.HideToTrayOnLaunch = setting.HideToTrayOnLaunch;
            cfg.UseHardLink = setting.UseHardLink;
            cfg.UseExternalBrowser = setting.UseExternalBrowser;
            cfg.CheckGameUpdates = setting.CheckGameUpdates;
            cfg.UpdateDownloadSource = setting.UpdateDownloadSource;
            cfg.ArchiveLauncherImages = setting.ArchiveLauncherImages;
            ConfigHelper.Save(cfg);
            Setting.ApplyStartWithWindows(setting.StartWithWindows);
            LoadUpdateBadgeFromCache();
            RebuildGameButtons();
            RebuildSidebar();
        }
        private void btn_global_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value is not string lang) return;

            if (lang.StartsWith("en")) AntdUI.Localization.Provider = new Localizer();
            else AntdUI.Localization.Provider = null;
            AntdUI.Localization.SetLanguage(lang);

            var cfg = ConfigHelper.Load();
            cfg.Language = lang;
            ConfigHelper.Save(cfg);

            btn_more.Items.Clear();
            btn_more.Items.AddRange(new AntdUI.SelectItem[] {
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.Help", "帮助"), "help").SetIcon("QuestionCircleOutlined"),
                new AntdUI.SelectItem(AntdUI.Localization.Get("App.Menu.About", "关于"),"info").SetIcon("InfoCircleOutlined"),
                new AntdUI.SelectItem("Github","github").SetIcon("GithubOutlined"),
                new AntdUI.SelectItem("BiliBili","bilibili").SetIcon("BilibiliOutlined"),
            });

            RebuildSidebar();

            AntdUI.Spin.open(this, async spinCfg =>
            {
                await System.Threading.Tasks.Task.Delay(500);
                this.Invoke(new Action(() =>
                {
                    if (_currentGame != null) SelectGame(_currentGame, true);
                    Refresh();
                }));
            });
        }

        private void btn_more_Changed(object sender, AntdUI.ObjectNEventArgs e)
        {
            btn_more.SelectedValue = null;
            if (e.Value is string code)
            {
                BeginInvoke(() =>
                {
                switch (code)
                {
                        case "help":
                            TabHeaderForm.Open(
                                AntdUI.Localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                                    ? "https://github.com/lTinchl/Xel-Launcher/blob/master/docs/wiki.en-US.md"
                                    : "https://github.com/lTinchl/Xel-Launcher/blob/master/docs/wiki.zh-CN.md");
                            break;
                        case "info":
                            AntdUI.Modal.open(new AntdUI.Modal.Config(this, "", new About())
                            {
                                OkText = null,
                                CancelText = null,
                                BtnHeight = 0,
                                EnableSound = false,
                                MaskClosable = true
                            });
                            break;
                        case "github":
                            TabHeaderForm.Open("https://github.com/lTinchl/Xel-Launcher");
                            break;
                        case "bilibili":
                            TabHeaderForm.Open("https://www.bilibili.com/video/BV1FD9MBfED5");
                            break;
                    }
                });
            }
        }
    }
}
