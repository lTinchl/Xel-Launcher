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
                            Title = Localizer.GetRequiredString("App.Skport.Auto.Title"),
                            Message = Localizer.GetRequiredString("App.Skport.Auto.AlreadyDone")
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
                    notifyIcon.ShowBalloonTip(8000, Localizer.GetRequiredString("App.Skport.Auto.FailedTitle"), ex.Message, ToolTipIcon.Error);
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
