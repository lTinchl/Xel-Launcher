// Helpers/UpdateHelper.cs
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class UpdateHelper
    {
        private const string ApiUrl =
            "https://api.github.com/repos/lTinchl/Xel-Launcher/releases/latest";

        // 备用网盘链接，GitHub 下载失败时跳转
        public const string FallbackUrl = "TODO";

        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static UpdateHelper()
        {
            var version = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetName().Version?.ToString() ?? "unknown";
            _client.DefaultRequestHeaders.Add("User-Agent", $"XelLauncher/{version}");
            _client.DefaultRequestHeaders.Add(
                "Accept",
                "application/vnd.github+json");
        }

        /// <summary>
        /// 从 GitHub 查询最新 Release，返回 UpdateInfo；
        /// 网络错误或解析失败时返回 null。
        /// </summary>
        public static async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                var json = await _client.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // tag_name 可能是 "v0.1.6" 或 "0.1.6"
                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var version = tagName.TrimStart('v', 'V');
                var changelog = root.GetProperty("body").GetString() ?? "";
                var releaseUrl = root.GetProperty("html_url").GetString() ?? "";

                string setupUrl = null;
                string portableUrl = null;

                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        var url  = asset.GetProperty("browser_download_url").GetString() ?? "";
                        if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                            setupUrl = url;
                        else if (name.EndsWith("-Portable.zip", StringComparison.OrdinalIgnoreCase))
                            portableUrl = url;
                    }
                }

                return new UpdateInfo
                {
                    LatestVersion       = version,
                    Changelog           = changelog,
                    SetupDownloadUrl    = setupUrl,
                    PortableDownloadUrl = portableUrl,
                    ReleasePageUrl      = releaseUrl
                };
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.LogError(ex, "UpdateHelper.CheckAsync - Timeout");
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "UpdateHelper.CheckAsync");
                return null;
            }
        }

        /// <summary>
        /// 比较当前版本与最新版本，返回 true 表示有新版本可用。
        /// </summary>
        public static bool IsNewer(string currentVersion, string latestVersion)
        {
            try
            {
                var current = new Version(currentVersion.TrimStart('v', 'V'));
                var latest  = new Version(latestVersion.TrimStart('v', 'V'));
                return latest > current;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"UpdateHelper.IsNewer({currentVersion}, {latestVersion})");
                return false;
            }
        }
    }
}
