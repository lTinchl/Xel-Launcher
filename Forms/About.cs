using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher
{
    public partial class About : UserControl
    {
        public About()
        {
            InitializeComponent();
            _ = LoadGitHubDataAsync();
        }

        private async Task LoadGitHubDataAsync(int retry = 3)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Xel Launcher/" + Application.ProductVersion);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            var starsTask = LoadStarsAsync(client, retry);
            var downloadsTask = LoadDownloadsAsync(client, retry);
            var stars = await starsTask;
            var downloads = await downloadsTask;

            if (IsHandleCreated)
                Invoke(() =>
                {
                    shieldStars.Text = stars ?? "N/A";
                    shieldDownloads.Text = downloads ?? "N/A";
                });
        }

        private static async Task<string> LoadStarsAsync(HttpClient client, int retry)
        {
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    var repoJson = await client.GetStringAsync("https://api.github.com/repos/lTinchl/Xel-Launcher");
                    using var repoDoc = JsonDocument.Parse(repoJson);
                    return repoDoc.RootElement.GetProperty("stargazers_count").GetInt32().ToString();
                }
                catch
                {
                    if (i < retry - 1) await Task.Delay(2000);
                }
            }

            return await LoadShieldMessageAsync(client, "https://img.shields.io/github/stars/lTinchl/Xel-Launcher.json");
        }

        private static async Task<string> LoadDownloadsAsync(HttpClient client, int retry)
        {
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    var releasesJson = await client.GetStringAsync("https://api.github.com/repos/lTinchl/Xel-Launcher/releases");
                    using var releasesDoc = JsonDocument.Parse(releasesJson);
                    return releasesDoc.RootElement.EnumerateArray()
                        .SelectMany(r => r.GetProperty("assets").EnumerateArray())
                        .Sum(a => a.GetProperty("download_count").GetInt32())
                        .ToString();
                }
                catch
                {
                    if (i < retry - 1) await Task.Delay(2000);
                }
            }

            return await LoadShieldMessageAsync(client, "https://img.shields.io/github/downloads/lTinchl/Xel-Launcher/total.json");
        }

        private static async Task<string> LoadShieldMessageAsync(HttpClient client, string url)
        {
            try
            {
                var json = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
