using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public sealed class SkylandAutoSignResult
    {
        public bool Notify { get; init; }
        public bool Success { get; init; }
        public string Title { get; init; } = "";
        public string Message { get; init; } = "";
    }

    public static class SkylandAutoSignHelper
    {
        private const string StartupValueName = "XelLauncherSkylandAutoSign";
        private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static async Task<SkylandAutoSignResult> RunIfDueAsync(
            Func<AppConfig, bool> enabledSelector,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            var cfg = ConfigHelper.Load();
            if (!enabledSelector(cfg))
                return new SkylandAutoSignResult();

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (cfg.SkylandLastAutoSignDate == today)
                return new SkylandAutoSignResult();

            var tokens = SkylandTokenStorage.GetTokens(cfg);
            if (tokens.Count == 0)
            {
                var skipped = Localizer.GetRequiredString("App.Skyland.Auto.NoToken");
                SkylandLogStore.Append(skipped);
                return new SkylandAutoSignResult
                {
                    Notify = true,
                    Success = false,
                    Title = Localizer.GetRequiredString("App.Skyland.Auto.Title"),
                    Message = skipped
                };
            }

            ReportProgress(progress, Localizer.GetRequiredString("App.Skyland.Auto.Start"));
            var logProgress = new Progress<string>(message => ReportProgress(progress, message));
            var results = await new SkylandService().SignAllAsync(tokens, logProgress, cancellationToken);

            cfg = ConfigHelper.Load();
            cfg.SkylandLastAutoSignDate = today;
            ConfigHelper.Save(cfg);

            var message = results.Count == 0
                ? Localizer.GetRequiredString("App.Skyland.Auto.Done")
                : string.Join(Environment.NewLine, results.Take(3));
            if (results.Count > 3)
                message += Environment.NewLine + string.Format(Localizer.GetRequiredString("App.Skyland.Auto.MoreResults"), results.Count - 3);
            ReportProgress(progress, Localizer.GetRequiredString("App.Skyland.Auto.Complete"));

            return new SkylandAutoSignResult
            {
                Notify = true,
                Success = true,
                Title = Localizer.GetRequiredString("App.Skyland.Auto.CompleteTitle"),
                Message = message
            };
        }

        private static void ReportProgress(IProgress<string> progress, string message)
        {
            SkylandLogStore.Append(message);
            progress?.Report(message);
        }

        public static void ApplyStartupRegistration(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(StartupRunKey, writable: true);

            if (!enabled)
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
                exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exe))
                exe = ApplicationExecutablePath();

            key.SetValue(StartupValueName, $"\"{exe}\" --skyland-auto-sign");
        }

        private static string ApplicationExecutablePath()
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path)) return path;
            return Path.ChangeExtension(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), ".exe");
        }
    }
}
