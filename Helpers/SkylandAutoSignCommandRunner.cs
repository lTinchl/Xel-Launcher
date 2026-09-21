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
                            Title = Localizer.GetRequiredString("App.Skyland.Auto.Title"),
                            Message = Localizer.GetRequiredString("App.Skyland.Auto.AlreadyDone")
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
                    notifyIcon.ShowBalloonTip(8000, Localizer.GetRequiredString("App.Skyland.Auto.FailedTitle"), ex.Message, ToolTipIcon.Error);
                    await Task.Delay(8500);
                }
                finally
                {
                    notifyIcon.Visible = false;
                    Application.Exit();
                }
            });

            Application.Run(context);
        }
    }
}
