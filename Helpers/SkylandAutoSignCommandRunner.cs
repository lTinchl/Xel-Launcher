using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class SkylandAutoSignCommandRunner
    {
        public static void Run()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();

            using var notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.icon,
                Text = "Xel Launcher",
                Visible = true,
            };
            var context = new ApplicationContext();

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await SkylandAutoSignHelper.RunIfDueAsync(cfg => cfg.SkylandStartupSignEnabled);
                    if (!result.Notify)
                    {
                        result = new SkylandAutoSignResult
                        {
                            Notify = true,
                            Success = true,
                            Title = AntdUI.Localization.Get("App.Skyland.Auto.Title", "森空岛自动签到"),
                            Message = AntdUI.Localization.Get("App.Skyland.Auto.AlreadyDone", "今日已执行过自动签到，已跳过。")
                        };
                    }

                    notifyIcon.ShowBalloonTip(
                        8000,
                        result.Title,
                        result.Message,
                        result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
                    await Task.Delay(8500);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex, "SkylandAutoSignCommand");
                    notifyIcon.ShowBalloonTip(8000, AntdUI.Localization.Get("App.Skyland.Auto.FailedTitle", "森空岛自动签到失败"), ex.Message, ToolTipIcon.Error);
                    await Task.Delay(8500);
                }
                finally
                {
                    notifyIcon.Visible = false;
                    context.ExitThread();
                }
            });

            Application.Run(context);
        }
    }
}
