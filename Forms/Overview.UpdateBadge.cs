using System;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private void PositionUpdateBadge()
        {
            if (!IsHandleCreated) return;

            using var subFont = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            int iconAndPad = 134;
            int subWidth = TextRenderer.MeasureText(windowBar.SubText ?? "", subFont).Width;
            int badgeX = iconAndPad + subWidth + 12;
            int badgeY = 8;

            updateBadge.Location = new System.Drawing.Point(badgeX, badgeY);
            updateBadge.BringToFront();
        }

        private async System.Threading.Tasks.Task CheckUpdateBadgeAsync()
        {
            try
            {
                var info = await UpdateHelper.CheckAsync();
                if (info == null) return;
                var currentVer = Application.ProductVersion;
                if (UpdateHelper.IsNewer(currentVer, info.LatestVersion))
                {
                    if (IsHandleCreated)
                        Invoke(() => updateBadge.Visible = true);
                }
            }
            catch { }
        }

        private void updateBadge_Click(object sender, EventArgs e)
        {
            OpenSettingOnUpdatePage();
        }

        private void OpenSettingOnUpdatePage()
        {
            var setting = new Setting(this);
            setting.NavigateToUpdate();
            if (AntdUI.Modal.open(this, AntdUI.Localization.Get("Setting", "设置"), setting) == DialogResult.OK)
            {
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
                ConfigHelper.Save(cfg);
                Setting.ApplyStartWithWindows(setting.StartWithWindows);
            }
            RebuildGameButtons();
            RebuildSidebar();
        }
    }
}
