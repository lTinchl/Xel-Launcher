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
            shieldPanel.SizeChanged += (s, e) =>
            {
                int left = (Width - shieldPanel.Width) / 2;
                shieldPanel.Margin = new System.Windows.Forms.Padding(left > 0 ? left : 0, 6, 0, 0);
            };
            _ = LoadGitHubDataAsync();
        }

        private async Task LoadGitHubDataAsync(int retry = 3)
        {
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.Add("User-Agent", "Xel Launcher/" + Application.ProductVersion);
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                    var repoJson = await client.GetStringAsync("https://api.github.com/repos/lTinchl/Xel-Launcher");
                    using var repoDoc = JsonDocument.Parse(repoJson);
                    var stars = repoDoc.RootElement.GetProperty("stargazers_count").GetInt32();

                    var releasesJson = await client.GetStringAsync("https://api.github.com/repos/lTinchl/Xel-Launcher/releases");
                    using var releasesDoc = JsonDocument.Parse(releasesJson);
                    var downloads = releasesDoc.RootElement.EnumerateArray()
                        .SelectMany(r => r.GetProperty("assets").EnumerateArray())
                        .Sum(a => a.GetProperty("download_count").GetInt32());

                    if (IsHandleCreated)
                        Invoke(() =>
                        {
                            shieldStars.Text = stars.ToString();
                            shieldDownloads.Text = downloads.ToString();
                        });
                    return;
                }
                catch
                {
                    if (i < retry - 1)
                        await Task.Delay(2000);
                }
            }
            if (IsHandleCreated)
                Invoke(() =>
                {
                    shieldStars.Text = "N/A";
                    shieldDownloads.Text = "N/A";
                });
        }
    }
}
