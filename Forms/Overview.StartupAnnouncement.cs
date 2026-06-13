using System;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private void ShowStartupAnnouncementIfNeeded()
        {
            try
            {
                var version = Application.ProductVersion;
                if (string.IsNullOrWhiteSpace(version)) return;

                var cfg = ConfigHelper.Load();
                if (string.Equals(cfg.LastReadStartupAnnouncementVersion, version, StringComparison.OrdinalIgnoreCase))
                    return;

                var dialog = new StartupAnnouncementDialog(version);
                dialog.ReadCompleted += (s, e) =>
                {
                    var latest = ConfigHelper.Load();
                    latest.LastReadStartupAnnouncementVersion = version;
                    ConfigHelper.Save(latest);
                };

                AntdUI.Modal.open(new AntdUI.Modal.Config(this,
                    AntdUI.Localization.Get("App.StartupAnnouncement.ModalTitle", "版本信息"),
                    dialog)
                {
                    BtnHeight = 0,
                    CloseIcon = false,
                    MaskClosable = false,
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "StartupAnnouncement");
            }
        }
    }
}
