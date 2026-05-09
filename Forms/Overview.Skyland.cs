using AntdUI;
using System;
using System.Threading.Tasks;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private async Task RunSkylandAutoSignOnLaunchAsync()
        {
            try
            {
                await Task.Delay(2000);
                var result = await SkylandAutoSignHelper.RunIfDueAsync(cfg => cfg.SkylandSignEnabled);
                if (!result.Notify || IsDisposed) return;

                BeginInvoke(new Action(() =>
                {
                    Notification.open(new Notification.Config(new Target(this),
                        result.Title,
                        result.Message,
                        result.Success ? TType.Success : TType.Warn,
                        TAlignFrom.BR)
                    {
                        AutoClose = 8
                    });
                }));
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "SkylandAutoSignOnLaunch");
            }
        }
    }
}
