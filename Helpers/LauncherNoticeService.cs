using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers;

public sealed record LauncherBannerItem(string ImageUrl, string JumpUrl);

public sealed record LauncherBannerAsset(string ImagePath, string JumpUrl);

public sealed record LauncherNoticeItem(string Category, string Title, string Date, string JumpUrl);

public sealed record LauncherNoticeContent(
    IReadOnlyList<LauncherBannerItem> Banners,
    IReadOnlyList<LauncherNoticeItem> Notices);

public sealed record LauncherNoticePayload(
    LauncherNoticeContent Content,
    IReadOnlyList<LauncherBannerAsset> BannerAssets)
{
    public IReadOnlyList<string> BannerImagePaths => BannerAssets.Select(x => x.ImagePath).ToArray();
}

public static class LauncherNoticeService
{
    private const string NoticeContentFileName = "launcher-notice.json";

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static LauncherNoticePayload? LoadCached(string iconName)
    {
        try
        {
            var path = GetNoticeContentPath(iconName);
            if (!File.Exists(path)) return null;

            var content = JsonSerializer.Deserialize<LauncherNoticeContent>(
                File.ReadAllText(path, Encoding.UTF8),
                JsonOptions);

            if (content == null) return null;
            return CreatePayload(iconName, content);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"LauncherNoticeService.LoadCached({iconName})");
            return null;
        }
    }

    public static async Task<LauncherNoticePayload?> RefreshAsync(string iconName, CancellationToken ct = default)
    {
        if (!TryGetApiConfig(iconName, out var webApiUrl, out var appCode, out var channel, out var subChannel, out var seq, out var language))
        {
            return null;
        }

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
                new { kind = "get_banner", get_banner_req = commonReq },
                new { kind = "get_announcement", get_announcement_req = commonReq }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(webApiUrl, content, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var banners = new List<LauncherBannerItem>();
            var notices = new List<LauncherNoticeItem>();

            if (document.RootElement.TryGetProperty("proxy_rsps", out var proxyRsps) &&
                proxyRsps.ValueKind == JsonValueKind.Array)
            {
                foreach (var proxyRsp in proxyRsps.EnumerateArray())
                {
                    var kind = GetString(proxyRsp, "kind");
                    if (string.Equals(kind, "get_banner", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadBanners(proxyRsp, banners);
                    }
                    else if (string.Equals(kind, "get_announcement", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadAnnouncements(proxyRsp, notices);
                    }
                }
            }

            if (banners.Count == 0 && notices.Count == 0) return null;

            var result = new LauncherNoticeContent(banners, notices);
            await SaveContentAsync(iconName, result, ct).ConfigureAwait(false);

            var bannerAssets = await DownloadBannersAsync(iconName, banners, ct).ConfigureAwait(false);
            return new LauncherNoticePayload(result, bannerAssets);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"LauncherNoticeService.RefreshAsync({iconName})");
            return null;
        }
    }

    public static LauncherNoticePayload CreatePayload(string iconName, LauncherNoticeContent content) =>
        new(content, FindCachedBannerAssets(iconName, content.Banners));

    private static void ReadBanners(JsonElement proxyRsp, List<LauncherBannerItem> banners)
    {
        if (!proxyRsp.TryGetProperty("get_banner_rsp", out var bannerRsp) ||
            !bannerRsp.TryGetProperty("banners", out var bannerArray) ||
            bannerArray.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in bannerArray.EnumerateArray())
        {
            var imageUrl = GetString(item, "url");
            if (string.IsNullOrWhiteSpace(imageUrl)) continue;
            banners.Add(new LauncherBannerItem(imageUrl, GetString(item, "jump_url") ?? ""));
        }
    }

    private static void ReadAnnouncements(JsonElement proxyRsp, List<LauncherNoticeItem> notices)
    {
        if (!proxyRsp.TryGetProperty("get_announcement_rsp", out var announcementRsp) ||
            !announcementRsp.TryGetProperty("tabs", out var tabs) ||
            tabs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var tab in tabs.EnumerateArray())
        {
            var category = GetString(tab, "tabName") ?? "公告";
            if (!tab.TryGetProperty("announcements", out var announcements) ||
                announcements.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

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

    private static async Task SaveContentAsync(string iconName, LauncherNoticeContent content, CancellationToken ct)
    {
        var dir = GetCoverDirectory(iconName);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(content, JsonOptions);
        await File.WriteAllTextAsync(GetNoticeContentPath(iconName), json, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<LauncherBannerAsset>> DownloadBannersAsync(string iconName, IReadOnlyList<LauncherBannerItem> banners, CancellationToken ct)
    {
        var assets = new List<LauncherBannerAsset>();

        foreach (var banner in banners.Take(6))
        {
            var imageUrl = banner.ImageUrl;
            if (string.IsNullOrWhiteSpace(imageUrl)) continue;

            var cached = FindCachedBanner(iconName, imageUrl);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                assets.Add(new LauncherBannerAsset(cached, banner.JumpUrl));
                continue;
            }

            try
            {
                var dir = GetCoverDirectory(iconName);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"notice-banner-{HashUrl(imageUrl)}{GetImageExtension(imageUrl)}");

                using var response = await Client.GetAsync(imageUrl, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var target = File.Create(path);
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
                assets.Add(new LauncherBannerAsset(path, banner.JumpUrl));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogHelper.LogError(ex, $"LauncherNoticeService.DownloadBannerAsync({iconName})");
            }
        }

        return assets;
    }

    private static IReadOnlyList<LauncherBannerAsset> FindCachedBannerAssets(string iconName, IReadOnlyList<LauncherBannerItem> banners)
    {
        var assets = banners
            .Take(6)
            .Select(x =>
            {
                var path = FindCachedBanner(iconName, x.ImageUrl);
                return string.IsNullOrWhiteSpace(path) ? null : new LauncherBannerAsset(path, x.JumpUrl);
            })
            .Where(x => x != null)
            .Cast<LauncherBannerAsset>()
            .ToList();

        if (assets.Count > 0) return assets;

        var fallback = FindCachedBanner(iconName, null);
        return string.IsNullOrWhiteSpace(fallback) ? [] : [new LauncherBannerAsset(fallback, "")];
    }

    private static string? FindCachedBanner(string iconName, string? imageUrl)
    {
        var dir = GetCoverDirectory(iconName);
        if (!Directory.Exists(dir)) return null;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var prefix = $"notice-banner-{HashUrl(imageUrl)}";
            var exact = Directory.EnumerateFiles(dir, $"{prefix}.*").FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(exact)) return exact;
        }

        return Directory
            .EnumerateFiles(dir, "notice-banner-*.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string GetNoticeContentPath(string iconName) =>
        Path.Combine(GetCoverDirectory(iconName), NoticeContentFileName);

    private static string GetCoverDirectory(string iconName)
    {
        return Path.Combine(ConfigHelper.ConfigDir, "GameCovers", NormalizeIconName(iconName));
    }

    private static string NormalizeIconName(string iconName) => iconName switch
    {
        "BiliArknights" => "Arknights",
        "BiliEndfield" => "Endfield",
        "PlayEndfield" => "GlobalEndfield",
        _ => iconName,
    };

    private static string GetImageExtension(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(ext)) return ext;
        }

        return ".png";
    }

    private static string HashUrl(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string FormatNoticeDate(string? timestamp)
    {
        if (!long.TryParse(timestamp, out var value)) return DateTime.Now.ToString("MM/dd");

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

    internal static bool TryGetApiConfig(
        string iconName,
        out string webApiUrl,
        out string appCode,
        out string channel,
        out string subChannel,
        out string seq,
        out string language)
    {
        webApiUrl = appCode = channel = subChannel = seq = language = "";

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
            default:
                return false;
        }
    }
}
