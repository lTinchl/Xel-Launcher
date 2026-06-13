using System;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

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

        private void LoadUpdateBadgeFromCache()
        {
            try
            {
                var cfg = ConfigHelper.Load();
                updateBadge.Visible = UpdateHelper.ShouldShowCachedUpdate(cfg, Application.ProductVersion);
            }
            catch { }
        }

        private void ShowStartupUpdateReminderFromCache()
        {
            try
            {
                var cfg = ConfigHelper.Load();
                updateBadge.Visible = UpdateHelper.ShouldShowCachedUpdate(cfg, Application.ProductVersion);

                if (cfg.UpdateState == null ||
                    !UpdateHelper.ShouldShowUpdateReminder(cfg, Application.ProductVersion))
                    return;

                ShowUpdateReminder(cfg.UpdateState);
            }
            catch { }
        }

        private async System.Threading.Tasks.Task RefreshUpdateStateOnStartupAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(1500);
                await UpdateHelper.CheckAndPersistAsync(Application.ProductVersion);
                if (IsDisposed || !IsHandleCreated) return;

                BeginInvoke(new Action(() =>
                {
                    LoadUpdateBadgeFromCache();
                    ShowStartupUpdateReminderFromCache();
                }));
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "RefreshUpdateStateOnStartup");
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(LoadUpdateBadgeFromCache));
            }
        }

        private void updateBadge_Click(object sender, EventArgs e)
        {
            OpenSettingOnUpdatePage();
        }

        private void ShowUpdateReminder(AppUpdateState state)
        {
            var info = UpdateHelper.ToUpdateInfo(state);
            if (info == null) return;

            var dialog = new UpdateReminderDialog(info, Application.ProductVersion);
            AntdUI.Modal.open(new AntdUI.Modal.Config(this, AntdUI.Localization.Get("App.Update.ModalTitle", "软件更新"), dialog)
            {
                BtnHeight = 0,
                CloseIcon = true,
                MaskClosable = true,
            });

            ApplyUpdateReminderAction(state.LatestVersion, dialog.SelectedAction);
        }

        private void ApplyUpdateReminderAction(string latestVersion, UpdateReminderAction action)
        {
            if (string.IsNullOrWhiteSpace(latestVersion)) return;

            var cfg = ConfigHelper.Load();
            cfg.UpdateState ??= new AppUpdateState();
            cfg.LastNotifiedVersion = latestVersion;

            switch (action)
            {
                case UpdateReminderAction.SkipVersion:
                    cfg.UpdateState.SkippedVersion = latestVersion;
                    ConfigHelper.Save(cfg);
                    updateBadge.Visible = UpdateHelper.ShouldShowCachedUpdate(cfg, Application.ProductVersion);
                    break;
                case UpdateReminderAction.DisableReminder:
                    cfg.UpdateState.DisableReminder = true;
                    ConfigHelper.Save(cfg);
                    updateBadge.Visible = UpdateHelper.ShouldShowCachedUpdate(cfg, Application.ProductVersion);
                    break;
                case UpdateReminderAction.UpdateNow:
                    ConfigHelper.Save(cfg);
                    OpenSettingOnUpdatePage();
                    break;
                default:
                    ConfigHelper.Save(cfg);
                    updateBadge.Visible = UpdateHelper.ShouldShowCachedUpdate(cfg, Application.ProductVersion);
                    break;
            }
        }

        private void OpenSettingOnUpdatePage()
        {
            var setting = new Setting(this);
            setting.NavigateToUpdate();
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
            ConfigHelper.Save(cfg);
            Setting.ApplyStartWithWindows(setting.StartWithWindows);
            LoadUpdateBadgeFromCache();
            RebuildGameButtons();
            RebuildSidebar();
        }
    }
}
