using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers;

public static class LauncherBackgroundService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static async Task<string?> RefreshAsync(string iconName, CancellationToken ct = default)
    {
        if (!LauncherNoticeService.TryGetApiConfig(
                iconName,
                out var webApiUrl,
                out var appCode,
                out var channel,
                out var subChannel,
                out var seq,
                out var language))
        {
            return null;
        }

        var request = new
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
                    get_main_bg_image_req = request
                }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(webApiUrl, content, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct).ConfigureAwait(false);
            var imageUrl = ReadBackgroundUrl(document.RootElement);
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var cachedPath = FindCachedBackground(iconName, imageUrl);
            if (!string.IsNullOrWhiteSpace(cachedPath)) return cachedPath;

            return await DownloadAsync(iconName, imageUrl, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"LauncherBackgroundService.RefreshAsync({iconName})");
            return null;
        }
    }

    private static string? ReadBackgroundUrl(JsonElement root)
    {
        if (!root.TryGetProperty("proxy_rsps", out var responses) ||
            responses.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var response in responses.EnumerateArray())
        {
            if (!response.TryGetProperty("kind", out var kind) ||
                !string.Equals(kind.GetString(), "get_main_bg_image", StringComparison.OrdinalIgnoreCase) ||
                !response.TryGetProperty("get_main_bg_image_rsp", out var backgroundResponse) ||
                !backgroundResponse.TryGetProperty("main_bg_image", out var background) ||
                background.ValueKind != JsonValueKind.Object ||
                !background.TryGetProperty("url", out var urlElement))
            {
                continue;
            }

            var url = urlElement.GetString();
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }

    private static async Task<string?> DownloadAsync(string iconName, string imageUrl, CancellationToken ct)
    {
        var directory = GetCoverDirectory(iconName);
        Directory.CreateDirectory(directory);

        var targetPath = Path.Combine(
            directory,
            $"client-cover-{HashUrl(imageUrl)}{GetImageExtension(imageUrl)}");
        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using var response = await Client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = File.Create(temporaryPath))
            {
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            File.Move(temporaryPath, targetPath, true);
            return targetPath;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string? FindCachedBackground(string iconName, string imageUrl)
    {
        var directory = GetCoverDirectory(iconName);
        if (!Directory.Exists(directory)) return null;

        var prefix = $"client-cover-{HashUrl(imageUrl)}";
        return Directory
            .EnumerateFiles(directory, $"{prefix}.*")
            .FirstOrDefault(IsSupportedImagePath);
    }

    private static string GetCoverDirectory(string iconName) =>
        Path.Combine(ConfigHelper.ConfigDir, "GameCovers", NormalizeIconName(iconName));

    private static string NormalizeIconName(string iconName) => iconName switch
    {
        "BiliArknights" => "Arknights",
        "BiliEndfield" => "Endfield",
        "PlayEndfield" => "GlobalEndfield",
        _ => iconName,
    };

    private static string GetImageExtension(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
            if (extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp") return extension;
        }

        return ".jpg";
    }

    private static bool IsSupportedImagePath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp";

    private static string HashUrl(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
