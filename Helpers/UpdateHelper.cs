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
        public const string FallbackUrl = "https://pan.quark.cn/s/54cd514d4236";

        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // 专用于文件下载，无超时限制（由 CancellationToken 控制）
        private static readonly HttpClient _downloadClient = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
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

        /// <summary>
        /// 流式下载文件到指定路径，通过 progress 回调汇报进度（0-100）和已下载/总大小字节数。
        /// 抛出异常时由调用方处理。
        /// </summary>
        /// <param name="url">下载 URL</param>
        /// <param name="destPath">目标文件完整路径</param>
        /// <param name="progress">进度回调：(percent 0-100, downloadedBytes, totalBytes)</param>
        /// <param name="ct">取消令牌</param>
        public static async Task DownloadAsync(
            string url,
            string destPath,
            Action<int, long, long> progress,
            System.Threading.CancellationToken ct = default)
        {
            using var response = await _downloadClient.GetAsync(
                url,
                System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var dir = System.IO.Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            var tmpPath = destPath + ".download";
            try
            {
                using var src  = await response.Content.ReadAsStreamAsync(ct);
                using var dest = System.IO.File.Create(tmpPath);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;
                    int pct = total > 0 ? (int)(downloaded * 100 / total) : -1;
                    progress?.Invoke(pct, downloaded, total);
                }
            }
            catch
            {
                try { System.IO.File.Delete(tmpPath); } catch { }
                throw;
            }

            System.IO.File.Move(tmpPath, destPath, overwrite: true);
        }
    }
}
