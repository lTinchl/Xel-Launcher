// Helpers/UpdateHelper.cs
using System;
using System.Net;
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
        public const string DownloadSourceGitHub = "github";
        public const string DownloadSourceNetdisk = "netdisk";
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

                
                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var version = tagName.TrimStart('v', 'V');
                var changelog = root.GetProperty("body").GetString() ?? "";
                var releaseUrl = root.GetProperty("html_url").GetString() ?? "";
                DateTimeOffset? publishedAt = null;
                if (root.TryGetProperty("published_at", out var publishedElement) &&
                    DateTimeOffset.TryParse(publishedElement.GetString(), out var parsedPublishedAt))
                {
                    publishedAt = parsedPublishedAt;
                }

                string setupUrl = null;
                string portableUrl = null;
                long? setupSize = null;
                long? portableSize = null;

                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        var url  = asset.GetProperty("browser_download_url").GetString() ?? "";
                        long? size = asset.TryGetProperty("size", out var sizeElement) &&
                                     sizeElement.TryGetInt64(out var parsedSize)
                            ? parsedSize
                            : null;
                        if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            setupUrl = url;
                            setupSize = size;
                        }
                        else if (name.EndsWith("-Portable.zip", StringComparison.OrdinalIgnoreCase))
                        {
                            portableUrl = url;
                            portableSize = size;
                        }
                    }
                }

                return new UpdateInfo
                {
                    LatestVersion       = version,
                    Changelog           = changelog,
                    PublishedAt         = publishedAt,
                    SetupDownloadUrl    = setupUrl,
                    SetupSizeBytes      = setupSize,
                    PortableDownloadUrl = portableUrl,
                    PortableSizeBytes   = portableSize,
                    ReleasePageUrl      = releaseUrl
                };
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.LogError(ex, "UpdateHelper.CheckAsync - Timeout");
                return null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden &&
                                                  ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                LogHelper.Log("Update check skipped: GitHub API rate limit exceeded");
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
        public static AppUpdateState GetCachedState()
        {
            var cfg = ConfigHelper.Load();
            return cfg.UpdateState ?? new AppUpdateState();
        }

        public static string NormalizeDownloadSource(string source)
        {
            return string.Equals(source, DownloadSourceNetdisk, StringComparison.OrdinalIgnoreCase)
                ? DownloadSourceNetdisk
                : DownloadSourceGitHub;
        }

        public static bool IsNetdiskDownloadSource(string source) =>
            NormalizeDownloadSource(source) == DownloadSourceNetdisk;

        public static async Task<AppUpdateState> CheckAndPersistAsync(string currentVersion)
        {
            var cfg = ConfigHelper.Load();
            cfg.UpdateState ??= new AppUpdateState();
            cfg.UpdateState.LastCheckedAtUtc = DateTimeOffset.UtcNow.ToString("O");

            var info = await CheckAsync();
            if (info == null)
            {
                ConfigHelper.Save(cfg);
                return null;
            }

            SaveInfo(cfg.UpdateState, info, IsNewer(currentVersion, info.LatestVersion));
            ConfigHelper.Save(cfg);
            return cfg.UpdateState;
        }

        public static UpdateInfo ToUpdateInfo(AppUpdateState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.LatestVersion))
                return null;

            return new UpdateInfo
            {
                LatestVersion = state.LatestVersion,
                Changelog = state.Changelog,
                PublishedAt = state.PublishedAt,
                SetupDownloadUrl = state.SetupDownloadUrl,
                SetupSizeBytes = state.SetupSizeBytes,
                PortableDownloadUrl = state.PortableDownloadUrl,
                PortableSizeBytes = state.PortableSizeBytes,
                ReleasePageUrl = state.ReleasePageUrl
            };
        }

        public static bool ShouldShowCachedUpdate(AppConfig cfg, string currentVersion)
        {
            var state = cfg?.UpdateState;
            if (state == null || !state.HasUpdate) return false;
            if (string.IsNullOrWhiteSpace(state.LatestVersion)) return false;

            return IsNewer(currentVersion, state.LatestVersion);
        }

        public static bool ShouldShowUpdateReminder(AppConfig cfg, string currentVersion)
        {
            if (!ShouldShowCachedUpdate(cfg, currentVersion)) return false;

            var state = cfg.UpdateState;
            if (string.Equals(state.SkippedVersion, state.LatestVersion, StringComparison.OrdinalIgnoreCase))
                return false;

            // Older configs used a global switch. Limit it to the version for which the
            // user originally made that choice so every later release still gets a first reminder.
            if (state.DisableReminder &&
                string.Equals(cfg.LastNotifiedVersion, state.LatestVersion, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static void SaveInfo(AppUpdateState state, UpdateInfo info, bool hasUpdate)
        {
            var previousLatestVersion = state.LatestVersion;
            state.HasUpdate = hasUpdate;
            state.LatestVersion = info.LatestVersion ?? "";
            state.Changelog = info.Changelog ?? "";
            state.PublishedAt = info.PublishedAt;
            state.SetupDownloadUrl = info.SetupDownloadUrl ?? "";
            state.SetupSizeBytes = info.SetupSizeBytes;
            state.PortableDownloadUrl = info.PortableDownloadUrl ?? "";
            state.PortableSizeBytes = info.PortableSizeBytes;
            state.ReleasePageUrl = info.ReleasePageUrl ?? "";

            if (hasUpdate &&
                !string.Equals(previousLatestVersion, state.LatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                state.SkippedVersion = "";
                state.DisableReminder = false;
            }
        }

        public static bool IsNewer(string currentVersion, string latestVersion)
        {
            try
            {
                var current = NormalizeVersion(currentVersion);
                var latest  = NormalizeVersion(latestVersion);
                if (current == null || latest == null) return false;
                return latest > current;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"UpdateHelper.IsNewer({currentVersion}, {latestVersion})");
                return false;
            }
        }

        private static Version NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;

            var core = version.Trim().TrimStart('v', 'V');
            var prereleaseIndex = core.IndexOf('-');
            if (prereleaseIndex >= 0)
                core = core[..prereleaseIndex];

            var metadataIndex = core.IndexOf('+');
            if (metadataIndex >= 0)
                core = core[..metadataIndex];

            return Version.TryParse(core, out var parsed) ? parsed : null;
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
