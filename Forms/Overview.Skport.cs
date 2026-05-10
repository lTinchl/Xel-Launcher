using AntdUI;
using System;
using System.Threading.Tasks;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private async Task RunSkportAutoSignOnLaunchAsync()
        {
            try
            {
                await Task.Delay(3000);
                var result = await SkportAutoSignHelper.RunIfDueAsync(cfg => cfg.SkportSignEnabled);
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
                LogHelper.LogError(ex, "SkportAutoSignOnLaunch");
            }
        }
    }
}
