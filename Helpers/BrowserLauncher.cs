using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class BrowserLauncher
    {
        public static void Open(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not ("http" or "https"))
                    throw new ArgumentException(Localizer.GetRequiredString("App.Browser.InvalidUrl"));

                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "OpenSystemBrowser");
                MessageBox.Show(Localizer.GetRequiredString("App.Browser.OpenFailed") +
                    Environment.NewLine + ex.Message, "Xel Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
