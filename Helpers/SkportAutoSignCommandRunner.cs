using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class SkportAutoSignCommandRunner
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
                    var result = await SkportAutoSignHelper.RunIfDueAsync(cfg => cfg.SkportStartupSignEnabled);
                    if (!result.Notify)
                    {
                        result = new SkportAutoSignResult
                        {
                            Notify = true,
                            Success = true,
                            Title = AntdUI.Localization.Get("App.Skport.Auto.Title", "SKPORT 自动签到"),
                            Message = AntdUI.Localization.Get("App.Skport.Auto.AlreadyDone", "今日已执行过自动签到，已跳过。")
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
                    LogHelper.LogError(ex, "SkportAutoSignCommand");
                    notifyIcon.ShowBalloonTip(8000, AntdUI.Localization.Get("App.Skport.Auto.FailedTitle", "SKPORT 自动签到失败"), ex.Message, ToolTipIcon.Error);
                    await Task.Delay(8500);
                }
                finally
                {
                    Application.Exit();
                }
            });

            Application.Run(context);
        }
    }
}
