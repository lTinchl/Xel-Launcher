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
    public sealed class SkportAutoSignResult
    {
        public bool Notify { get; init; }
        public bool Success { get; init; }
        public string Title { get; init; } = "";
        public string Message { get; init; } = "";
    }

    public static class SkportAutoSignHelper
    {
        private const string StartupValueName = "XelLauncherSkportAutoSign";
        private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static async Task<SkportAutoSignResult> RunIfDueAsync(
            Func<AppConfig, bool> enabledSelector,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            var cfg = ConfigHelper.Load();
            if (!enabledSelector(cfg))
                return new SkportAutoSignResult();

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (cfg.SkportLastAutoSignDate == today)
                return new SkportAutoSignResult();

            var tokens = SkportTokenStorage.GetTokens(cfg);
            if (tokens.Count == 0)
            {
                var skipped = Localizer.GetRequiredString("App.Skport.Auto.NoToken");
                SkportLogStore.Append(skipped);
                return new SkportAutoSignResult
                {
                    Notify = true,
                    Success = false,
                    Title = Localizer.GetRequiredString("App.Skport.Auto.Title"),
                    Message = skipped
                };
            }

            ReportProgress(progress, Localizer.GetRequiredString("App.Skport.Auto.Start"));
            var logProgress = new Progress<string>(message => ReportProgress(progress, message));
            var results = await new SkportService().SignAllAsync(tokens, logProgress, cancellationToken);

            cfg = ConfigHelper.Load();
            cfg.SkportLastAutoSignDate = today;
            ConfigHelper.Save(cfg);

            var message = results.Count == 0
                ? Localizer.GetRequiredString("App.Skport.Auto.Done")
                : string.Join(Environment.NewLine, results.Take(3));
            if (results.Count > 3)
                message += Environment.NewLine + string.Format(Localizer.GetRequiredString("App.Skport.Auto.MoreResults"), results.Count - 3);
            ReportProgress(progress, Localizer.GetRequiredString("App.Skport.Auto.Complete"));

            return new SkportAutoSignResult
            {
                Notify = true,
                Success = true,
                Title = Localizer.GetRequiredString("App.Skport.Auto.CompleteTitle"),
                Message = message
            };
        }

        private static void ReportProgress(IProgress<string> progress, string message)
        {
            SkportLogStore.Append(message);
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

            key.SetValue(StartupValueName, $"\"{exe}\" --skport-auto-sign");
        }

        private static string ApplicationExecutablePath()
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path)) return path;
            return Path.ChangeExtension(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), ".exe");
        }
    }
}
