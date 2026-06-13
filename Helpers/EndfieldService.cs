using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Arknights.Management;
using Hi3Helper.Plugin.Arknights.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Hypergryph.Core.Management;
using Hi3Helper.Plugin.Endfield.Management;
using Hi3Helper.Plugin.Endfield.Management.PresetConfig;

namespace XelLauncher.Helpers
{
    public record GameStatus(bool IsInstalled, bool HasUpdate, string LocalVersion, string RemoteVersion);
    public record LauncherBannerItem(string ImageUrl, string JumpUrl);
    public record LauncherNoticeItem(string Category, string Title, string Date, string JumpUrl);
    public record LauncherNoticeContent(
        IReadOnlyList<LauncherBannerItem> Banners,
        IReadOnlyList<LauncherNoticeItem> Notices);

    public class EndfieldService : IDisposable
    {
        private static readonly HttpClient CoverHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly HttpClient RepairHttpClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private readonly string _iconName;
        private readonly PluginPresetConfigBase _preset;

        public EndfieldService(string iconName)
        {
            _iconName = iconName;
            _preset = iconName switch
            {
                "Arknights"      => new ArknightsCnPresetConfig(),
                "BiliArknights"  => new ArknightsBiliPresetConfig(),
                "Endfield"       => new EndfieldCnPresetConfig(),
                "BiliEndfield"   => new EndfieldBiliPresetConfig(),
                "GlobalEndfield" => new EndfieldGlobalPresetConfig(),
                "PlayEndfield"   => new EndfieldGlobalPresetConfig(), // GooglePlay 与国际服共用同一游戏文件
                _ => throw new ArgumentException($"Unknown game type: {iconName}", nameof(iconName))
            };
        }

        /// <summary>
        /// 检查游戏状态：是否已安装、是否有更新、本地/远端版本号。
        /// API 请求失败时返回 null。
        /// </summary>
        public async Task<GameStatus?> CheckStatusAsync(string installPath, CancellationToken ct = default)
        {
            var manager = _preset.GameManager;
            if (manager == null) return null;

            manager.SetGamePath(installPath);

            var cancelToken = Guid.NewGuid();
            manager.InitAsync(in cancelToken, out var initResult);
            int result = await initResult.AsTask<int>().ConfigureAwait(false);

            if (result != 0) return null;

            manager.IsGameInstalled(out bool isInstalled);
            manager.IsGameHasUpdate(out bool hasUpdate);
            manager.GetCurrentGameVersion(out GameVersion localVer);
            manager.GetApiGameVersion(out GameVersion remoteVer);

            return new GameStatus(isInstalled, hasUpdate, localVer.ToString(), remoteVer.ToString());
        }

        public async Task<string> GetClientCoverImageUrlAsync(CancellationToken ct = default)
        {
            return await GetClientMainBackgroundImageUrlAsync(ct).ConfigureAwait(false);
        }

        public async Task<LauncherNoticeContent> GetLauncherNoticeContentAsync(CancellationToken ct = default)
        {
            if (!TryGetMediaApiConfig(_iconName, out var webApiUrl, out var appCode, out var channel,
                    out var subChannel, out var seq, out var language))
                return new LauncherNoticeContent(Array.Empty<LauncherBannerItem>(), Array.Empty<LauncherNoticeItem>());

            var commonReq = new
            {
                appcode = appCode,
                language,
                channel,
                sub_channel = subChannel,
                platform = "Windows",
                source = "launcher"
            };

            var requestBody = new
            {
                seq,
                proxy_reqs = new object[]
                {
                    new
                    {
                        kind = "get_banner",
                        get_banner_req = commonReq
                    },
                    new
                    {
                        kind = "get_announcement",
                        get_announcement_req = commonReq
                    }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await CoverHttpClient.PostAsync(webApiUrl, content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

                if (!document.RootElement.TryGetProperty("proxy_rsps", out var proxyRsps) ||
                    proxyRsps.ValueKind != JsonValueKind.Array)
                    return new LauncherNoticeContent(Array.Empty<LauncherBannerItem>(), Array.Empty<LauncherNoticeItem>());

                var banners = new List<LauncherBannerItem>();
                var notices = new List<LauncherNoticeItem>();

                foreach (var proxyRsp in proxyRsps.EnumerateArray())
                {
                    if (!proxyRsp.TryGetProperty("kind", out var kindElement)) continue;
                    var kind = kindElement.GetString();

                    if (string.Equals(kind, "get_banner", StringComparison.OrdinalIgnoreCase))
                        ReadBanners(proxyRsp, banners);
                    else if (string.Equals(kind, "get_announcement", StringComparison.OrdinalIgnoreCase))
                        ReadAnnouncements(proxyRsp, notices);
                }

                return new LauncherNoticeContent(banners, notices);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return new LauncherNoticeContent(Array.Empty<LauncherBannerItem>(), Array.Empty<LauncherNoticeItem>());

                LogHelper.LogError(ex, $"EndfieldService.GetLauncherNoticeContentAsync({_iconName})");
                return new LauncherNoticeContent(Array.Empty<LauncherBannerItem>(), Array.Empty<LauncherNoticeItem>());
            }
        }

        private async Task<string> GetClientMainBackgroundImageUrlAsync(CancellationToken ct)
        {
            if (!TryGetMediaApiConfig(_iconName, out var webApiUrl, out var appCode, out var channel,
                    out var subChannel, out var seq, out var language))
                return null;

            var commonReq = new
            {
                appcode = appCode,
                language,
                channel,
                sub_channel = subChannel,
                platform = "Windows",
                source = "launcher"
            };

            var requestBody = new
            {
                seq,
                proxy_reqs = new[]
                {
                    new
                    {
                        kind = "get_main_bg_image",
                        get_main_bg_image_req = commonReq
                    }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await CoverHttpClient.PostAsync(webApiUrl, content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

                if (!document.RootElement.TryGetProperty("proxy_rsps", out var proxyRsps) ||
                    proxyRsps.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var proxyRsp in proxyRsps.EnumerateArray())
                {
                    if (!proxyRsp.TryGetProperty("kind", out var kind) ||
                        !string.Equals(kind.GetString(), "get_main_bg_image", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!proxyRsp.TryGetProperty("get_main_bg_image_rsp", out var backgroundRsp) ||
                        !backgroundRsp.TryGetProperty("main_bg_image", out var mainBgImage) ||
                        mainBgImage.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!mainBgImage.TryGetProperty("url", out var urlElement)) continue;

                    var url = urlElement.GetString();
                    if (IsLikelyImageUrl(url)) return url;
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return null;

                LogHelper.LogError(ex, $"EndfieldService.GetClientMainBackgroundImageUrlAsync({_iconName})");
            }

            return null;
        }

        private static bool IsLikelyImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.LocalPath : url;
            var ext = System.IO.Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return true;
            return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadBanners(JsonElement proxyRsp, List<LauncherBannerItem> banners)
        {
            if (!proxyRsp.TryGetProperty("get_banner_rsp", out var bannerRsp) ||
                !bannerRsp.TryGetProperty("banners", out var bannerArray) ||
                bannerArray.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in bannerArray.EnumerateArray())
            {
                var imageUrl = GetString(item, "url");
                if (!IsLikelyImageUrl(imageUrl)) continue;

                banners.Add(new LauncherBannerItem(imageUrl, GetString(item, "jump_url") ?? ""));
            }
        }

        private static void ReadAnnouncements(JsonElement proxyRsp, List<LauncherNoticeItem> notices)
        {
            if (!proxyRsp.TryGetProperty("get_announcement_rsp", out var announcementRsp) ||
                !announcementRsp.TryGetProperty("tabs", out var tabs) ||
                tabs.ValueKind != JsonValueKind.Array)
                return;

            foreach (var tab in tabs.EnumerateArray())
            {
                var category = GetString(tab, "tabName") ?? "公告";
                if (!tab.TryGetProperty("announcements", out var announcements) ||
                    announcements.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in announcements.EnumerateArray())
                {
                    var title = GetString(item, "content");
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    notices.Add(new LauncherNoticeItem(
                        category,
                        title,
                        FormatNoticeDate(GetString(item, "start_ts")),
                        GetString(item, "jump_url") ?? ""));
                }
            }
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static string FormatNoticeDate(string timestamp)
        {
            if (!long.TryParse(timestamp, out var value))
                return DateTime.Now.ToString("MM/dd");

            try
            {
                if (value < 10_000_000_000) value *= 1000;
                return DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().ToString("MM/dd");
            }
            catch
            {
                return DateTime.Now.ToString("MM/dd");
            }
        }

        private static bool TryGetMediaApiConfig(
            string iconName,
            out string webApiUrl,
            out string appCode,
            out string channel,
            out string subChannel,
            out string seq,
            out string language)
        {
            webApiUrl = appCode = channel = subChannel = seq = language = null;

            switch (iconName)
            {
                case "Arknights":
                    webApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
                    appCode = "GzD1CpaWgmSq1wew";
                    channel = "1";
                    subChannel = "1";
                    seq = "5";
                    language = "zh-cn";
                    return true;
                case "BiliArknights":
                    webApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
                    appCode = "GzD1CpaWgmSq1wew";
                    channel = "2";
                    subChannel = "2";
                    seq = "5";
                    language = "zh-cn";
                    return true;
                case "Endfield":
                    webApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
                    appCode = "6LL0KJuqHBVz33WK";
                    channel = "1";
                    subChannel = "1";
                    seq = "5";
                    language = "zh-cn";
                    return true;
                case "BiliEndfield":
                    webApiUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";
                    appCode = "6LL0KJuqHBVz33WK";
                    channel = "2";
                    subChannel = "2";
                    seq = "5";
                    language = "zh-cn";
                    return true;
                case "GlobalEndfield":
                case "PlayEndfield":
                    webApiUrl = "https://launcher.gryphline.com/api/proxy/web/batch_proxy";
                    appCode = "YDUTE5gscDZ229CW";
                    channel = "6";
                    subChannel = "6";
                    seq = "3";
                    language = "en-us";
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 下载并安装/更新游戏。
        /// onProgress 参数：(状态, 已下载字节数, 总字节数)
        /// </summary>
        public async Task InstallOrUpdateAsync(
            string installPath,
            Action<InstallProgressState, long, long> onProgress,
            CancellationToken ct = default)
        {
            var manager   = _preset.GameManager  ?? throw new InvalidOperationException("GameManager 未初始化");
            var installer = _preset.GameInstaller ?? throw new InvalidOperationException("GameInstaller 未初始化");

            manager.SetGamePath(installPath);
            manager.IsGameInstalled(out bool isInstalled);

            var currentState = InstallProgressState.Preparing;

            InstallProgressDelegate progressDelegate = (in InstallProgress p) =>
                onProgress(currentState, p.DownloadedBytes, p.TotalBytesToDownload);

            InstallProgressStateDelegate stateDelegate = state =>
            {
                currentState = state;
                onProgress(state, 0, 0);
            };

            var cancelToken = Guid.NewGuid();
            using var cancelRegistration = ct.Register(() => TryCancelPluginToken(cancelToken));
            ct.ThrowIfCancellationRequested();

            nint taskResult;
            if (isInstalled)
                installer.StartUpdateAsync(progressDelegate, stateDelegate, in cancelToken, out taskResult);
            else
                installer.StartInstallAsync(progressDelegate, stateDelegate, in cancelToken, out taskResult);

            await taskResult.AsTask().ConfigureAwait(false);
        }

        public async Task RepairAsync(
            string installPath,
            Action<InstallProgressState, long, long> onProgress,
            CancellationToken ct = default)
        {
            var manager = _preset.GameManager ?? throw new InvalidOperationException("GameManager 未初始化");
            if (manager is not HgGameManager hgManager)
                throw new InvalidOperationException("GameManager is not HgGameManager");

            manager.SetGamePath(installPath);

            var cancelToken = Guid.NewGuid();
            using var cancelRegistration = ct.Register(() => TryCancelPluginToken(cancelToken));
            ct.ThrowIfCancellationRequested();

            manager.InitAsync(in cancelToken, out var initResult);
            int result = await initResult.AsTask<int>().ConfigureAwait(false);
            if (result != 0)
                throw new InvalidOperationException("游戏信息初始化失败");

            manager.IsGameInstalled(out bool isInstalled);
            if (!isInstalled)
                throw new InvalidOperationException("未检测到已安装的游戏");

            var currentState = InstallProgressState.Preparing;

            InstallProgressDelegate progressDelegate = (in InstallProgress p) =>
                onProgress(currentState, p.DownloadedBytes, p.TotalBytesToDownload);

            InstallProgressStateDelegate stateDelegate = state =>
            {
                currentState = state;
                onProgress(state, 0, 0);
            };

            var repairer = new HgGameRepairer(RepairHttpClient, hgManager, installPath);
            await repairer.StartRepairAsync(progressDelegate, stateDelegate, ct).ConfigureAwait(false);
        }

        private static void TryCancelPluginToken(Guid cancelToken)
        {
            try
            {
                var vaultType = typeof(Hi3Helper.Plugin.Core.IPlugin).Assembly.GetType(
                    "Hi3Helper.Plugin.Core.Utility.ComCancellationTokenVault");
                var cancelMethod = vaultType?.GetMethod(
                    "CancelToken",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Guid).MakeByRefType(), typeof(bool) },
                    null);

                object[] args = { cancelToken, true };
                cancelMethod?.Invoke(null, args);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "EndfieldService.TryCancelPluginToken");
            }
        }

        public void Dispose()
        {
            _preset.GameInstaller?.Free();
            _preset.Dispose();
        }
    }
}
