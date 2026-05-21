using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace XelLauncher.Helpers
{
    internal static class GameCoverCache
    {
        private const string ClientCoverPattern = "client-cover-*";
        private const string NoticeBannerPattern = "notice-banner-*";
        private const string NoticeContentFileName = "launcher-notice.json";
        private const string ClientCoverRefreshStampFileName = "client-cover-refresh.txt";

        private static readonly object CoverRefreshLock = new();
        private static readonly HashSet<string> CoverRefreshAttempts = new(StringComparer.OrdinalIgnoreCase);

        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly string[] ImageExtensions =
            [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static string GetCachedCoverPath(string iconName)
        {
            var dir = GetGameCoverDir(iconName);
            if (!Directory.Exists(dir)) return null;

            return Directory.GetFiles(dir, ClientCoverPattern)
                .Where(IsLikelyImagePath)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault(IsLoadableImage);
        }

        public static LauncherNoticeContent GetCachedLauncherNoticeContent(string iconName)
        {
            try
            {
                var path = GetNoticeContentPath(iconName);
                if (!File.Exists(path)) return null;

                var json = File.ReadAllText(path, Encoding.UTF8);
                var dto = JsonSerializer.Deserialize<LauncherNoticeCacheDto>(json, JsonOptions);
                if (dto == null) return null;

                return new LauncherNoticeContent(
                    dto.Banners ?? new List<LauncherBannerItem>(),
                    dto.Notices ?? new List<LauncherNoticeItem>());
            }
            catch
            {
                return null;
            }
        }

        public static async Task SaveLauncherNoticeContentAsync(string iconName, LauncherNoticeContent content, CancellationToken ct = default)
        {
            if (content == null) return;

            try
            {
                var dir = GetGameCoverDir(iconName);
                Directory.CreateDirectory(dir);

                var dto = new LauncherNoticeCacheDto
                {
                    Banners = content.Banners?.ToList() ?? new List<LauncherBannerItem>(),
                    Notices = content.Notices?.ToList() ?? new List<LauncherNoticeItem>()
                };

                var json = JsonSerializer.Serialize(dto, JsonOptions);
                await File.WriteAllTextAsync(GetNoticeContentPath(iconName), json, Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GameCoverCache.SaveLauncherNoticeContentAsync({iconName})");
            }
        }

        public static string GetCachedNoticeBannerPath(string iconName, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var dir = GetGameCoverDir(iconName);
            if (!Directory.Exists(dir)) return null;

            var baseTarget = Path.Combine(dir, $"notice-banner-{HashUrl(imageUrl)}");
            return TryGetCachedPath(baseTarget, GetImageExtension(imageUrl));
        }

        public static System.Drawing.Image TryLoadImage(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var image = System.Drawing.Image.FromStream(stream);
                return new Bitmap(image);
            }
            catch
            {
                var pngPath = Path.ChangeExtension(path, ".png");
                if (string.Equals(path, pngPath, StringComparison.OrdinalIgnoreCase))
                    return null;

                if (!File.Exists(pngPath) && !TryConvertImageToPng(path, pngPath))
                    return null;

                try
                {
                    using var stream = File.OpenRead(pngPath);
                    using var image = System.Drawing.Image.FromStream(stream);
                    return new Bitmap(image);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool TryBeginDailyCoverRefresh(string iconName)
        {
            var dir = GetGameCoverDir(iconName);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var attemptKey = $"{dir}|{today}";

            lock (CoverRefreshLock)
            {
                if (CoverRefreshAttempts.Contains(attemptKey))
                    return false;

                var cachedPath = GetCachedCoverPath(iconName);
                if (!string.IsNullOrEmpty(cachedPath) && string.Equals(ReadCoverRefreshStamp(dir), today, StringComparison.Ordinal))
                    return false;

                CoverRefreshAttempts.Add(attemptKey);
                return true;
            }
        }

        public static void MarkDailyCoverRefreshAttempt(string iconName)
        {
            try
            {
                var dir = GetGameCoverDir(iconName);
                Directory.CreateDirectory(dir);
                File.WriteAllText(GetCoverRefreshStampPath(dir), DateTime.Now.ToString("yyyy-MM-dd"), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GameCoverCache.MarkDailyCoverRefreshAttempt({iconName})");
            }
        }

        public static async Task<string> UpdateAsync(string iconName, string imageUrl, TimeSpan? refreshAfter = null, CancellationToken ct = default, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var dir = GetGameCoverDir(iconName);
            Directory.CreateDirectory(dir);

            var urlExtension = GetImageExtension(imageUrl);
            var baseTarget = Path.Combine(dir, $"client-cover-{HashUrl(imageUrl)}");
            var cachedPath = TryGetCachedPath(baseTarget, urlExtension);
            if (cachedPath != null && !forceRefresh && !IsCacheExpired(cachedPath, refreshAfter)) return cachedPath;

            var temp = baseTarget + ".download.tmp";
            try
            {
                var metadata = LoadClientCoverMetadata(baseTarget);
                if (cachedPath != null && await IsRemoteCoverUnchangedAsync(imageUrl, metadata, ct).ConfigureAwait(false))
                {
                    LogHelper.Log($"Game cover unchanged, skip download: {iconName} -> {cachedPath}");
                    return cachedPath;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                ApplyConditionalHeaders(request, metadata);

                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified && cachedPath != null)
                {
                    LogHelper.Log($"Game cover not modified, skip download: {iconName} -> {cachedPath}");
                    return cachedPath;
                }

                response.EnsureSuccessStatusCode();

                await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var dest = File.Create(temp))
                {
                    await source.CopyToAsync(dest, ct).ConfigureAwait(false);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var extension = GetImageExtensionFromContentType(contentType);
                if (string.IsNullOrEmpty(extension) && IsSupportedImageExtension(urlExtension))
                    extension = urlExtension;
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                if (IsWebpFile(extension, temp))
                {
                    var pngPath = baseTarget + ".png";
                    if (TryConvertImageToPng(temp, pngPath))
                    {
                        TryDelete(temp);
                        CleanupOldCovers(dir, pngPath);
                        SaveClientCoverMetadata(baseTarget, response);
                        LogHelper.Log($"Game cover cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    var webpPath = baseTarget + ".webp";
                    MoveReplacing(temp, webpPath);
                    CleanupOldCovers(dir, webpPath);
                    SaveClientCoverMetadata(baseTarget, response);
                    LogHelper.Log($"Game cover cached (WebP fallback): {iconName} -> {webpPath}");
                    return webpPath;
                }

                var target = baseTarget + NormalizeImageExtension(extension);
                if (!IsLoadableImage(temp))
                {
                    var pngPath = baseTarget + ".png";
                    if (TryConvertImageToPng(temp, pngPath))
                    {
                        TryDelete(temp);
                        CleanupOldCovers(dir, pngPath);
                        SaveClientCoverMetadata(baseTarget, response);
                        LogHelper.Log($"Game cover cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    TryDelete(temp);
                    return cachedPath;
                }

                MoveReplacing(temp, target);
                CleanupOldCovers(dir, target);
                SaveClientCoverMetadata(baseTarget, response);
                LogHelper.Log($"Game cover cached: {iconName} -> {target}");
                return target;
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                LogHelper.LogError(ex, $"GameCoverCache.UpdateAsync({iconName})");
                return cachedPath;
            }
        }

        public static async Task<string> UpdateNoticeBannerAsync(string iconName, string imageUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var dir = GetGameCoverDir(iconName);
            Directory.CreateDirectory(dir);

            var urlExtension = GetImageExtension(imageUrl);
            var baseTarget = Path.Combine(dir, $"notice-banner-{HashUrl(imageUrl)}");
            var cachedPath = TryGetCachedPath(baseTarget, urlExtension);
            if (cachedPath != null) return cachedPath;

            var temp = baseTarget + ".download.tmp";
            try
            {
                using var response = await Client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var dest = File.Create(temp))
                {
                    await source.CopyToAsync(dest, ct).ConfigureAwait(false);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var extension = GetImageExtensionFromContentType(contentType);
                if (string.IsNullOrEmpty(extension) && IsSupportedImageExtension(urlExtension))
                    extension = urlExtension;
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                if (IsWebpFile(extension, temp))
                {
                    var pngPath = baseTarget + ".png";
                    if (TryConvertImageToPng(temp, pngPath))
                    {
                        TryDelete(temp);
                        LogHelper.Log($"Notice banner cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    var webpPath = baseTarget + ".webp";
                    MoveReplacing(temp, webpPath);
                    LogHelper.Log($"Notice banner cached (WebP fallback): {iconName} -> {webpPath}");
                    return webpPath;
                }

                var target = baseTarget + NormalizeImageExtension(extension);
                if (!IsLoadableImage(temp))
                {
                    var pngPath = baseTarget + ".png";
                    if (TryConvertImageToPng(temp, pngPath))
                    {
                        TryDelete(temp);
                        LogHelper.Log($"Notice banner cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    TryDelete(temp);
                    return null;
                }

                MoveReplacing(temp, target);
                LogHelper.Log($"Notice banner cached: {iconName} -> {target}");
                return target;
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                LogHelper.LogError(ex, $"GameCoverCache.UpdateNoticeBannerAsync({iconName})");
                return null;
            }
        }

        public static void CleanupNoticeBanners(string iconName, IEnumerable<string> keepImageUrls)
        {
            try
            {
                var dir = GetGameCoverDir(iconName);
                if (!Directory.Exists(dir)) return;

                var keepHashes = new HashSet<string>(
                    (keepImageUrls ?? Array.Empty<string>())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(HashUrl),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var file in Directory.GetFiles(dir, NoticeBannerPattern))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var hash = fileName.StartsWith("notice-banner-", StringComparison.OrdinalIgnoreCase)
                        ? fileName["notice-banner-".Length..]
                        : "";
                    if (!keepHashes.Contains(hash))
                        TryDelete(file);
                }
            }
            catch { }
        }

        private static bool IsLoadableImage(string path)
        {
            using var image = TryLoadImage(path);
            return image != null;
        }

        private static string TryGetCachedPath(string baseTarget, string preferredExtension)
        {
            var extensions = ImageExtensions
                .Prepend(".png")
                .Prepend(NormalizeImageExtension(preferredExtension))
                .Where(IsSupportedImageExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var extension in extensions)
            {
                var path = baseTarget + extension;
                if (File.Exists(path) && IsLoadableImage(path))
                    return path;
            }

            return null;
        }

        private static bool TryConvertImageToPng(string sourcePath, string pngPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
                var tempPng = pngPath + ".tmp";
                using (var image = ImageSharpImage.Load(sourcePath))
                using (var output = File.Create(tempPng))
                {
                    image.Save(output, new PngEncoder());
                }

                MoveReplacing(tempPng, pngPath);
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(pngPath + ".tmp");
                Debug.WriteLine($"[GameCoverCache] Image to PNG conversion failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsWebpFile(string extension, string path)
        {
            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                Span<byte> header = stackalloc byte[12];
                using var stream = File.OpenRead(path);
                if (stream.Read(header) < header.Length) return false;

                return header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                       header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P';
            }
            catch
            {
                return false;
            }
        }

        private static string GetGameCoverDir(string iconName)
        {
            var normalizedName = iconName switch
            {
                "BiliArknights" => "Arknights",
                "BiliEndfield" => "Endfield",
                "PlayEndfield" => "GlobalEndfield",
                _ => iconName
            };
            return Path.Combine(ConfigHelper.ConfigDir, "GameCovers", SanitizeFileName(normalizedName));
        }

        private static bool IsCacheExpired(string path, TimeSpan? refreshAfter)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return true;
            if (!refreshAfter.HasValue || refreshAfter.Value <= TimeSpan.Zero) return false;

            try
            {
                return DateTime.UtcNow - File.GetLastWriteTimeUtc(path) >= refreshAfter.Value;
            }
            catch
            {
                return true;
            }
        }

        private static async Task<bool> IsRemoteCoverUnchangedAsync(string imageUrl, ClientCoverMetadata metadata, CancellationToken ct)
        {
            if (metadata == null || !metadata.HasValidator) return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return false;

                var remote = CreateClientCoverMetadata(response);
                return remote.HasValidator && metadata.Matches(remote);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"[GameCoverCache] HEAD duplicate check failed: {ex.Message}");
                return false;
            }
        }

        private static void ApplyConditionalHeaders(HttpRequestMessage request, ClientCoverMetadata metadata)
        {
            if (metadata == null) return;

            if (!string.IsNullOrWhiteSpace(metadata.ETag))
            {
                try { request.Headers.TryAddWithoutValidation("If-None-Match", metadata.ETag); }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(metadata.LastModified))
            {
                try { request.Headers.TryAddWithoutValidation("If-Modified-Since", metadata.LastModified); }
                catch { }
            }
        }

        private static ClientCoverMetadata LoadClientCoverMetadata(string baseTarget)
        {
            try
            {
                var path = GetClientCoverMetadataPath(baseTarget);
                if (!File.Exists(path)) return null;

                return JsonSerializer.Deserialize<ClientCoverMetadata>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveClientCoverMetadata(string baseTarget, HttpResponseMessage response)
        {
            try
            {
                var metadata = CreateClientCoverMetadata(response);
                if (!metadata.HasValidator) return;

                File.WriteAllText(GetClientCoverMetadataPath(baseTarget), JsonSerializer.Serialize(metadata, JsonOptions), Encoding.UTF8);
            }
            catch { }
        }

        private static ClientCoverMetadata CreateClientCoverMetadata(HttpResponseMessage response)
        {
            var lastModified = response.Content.Headers.LastModified?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(lastModified) && response.Headers.TryGetValues("Last-Modified", out var values))
                lastModified = values.FirstOrDefault() ?? "";

            return new ClientCoverMetadata
            {
                ETag = response.Headers.ETag?.ToString() ?? "",
                LastModified = lastModified,
                ContentLength = response.Content.Headers.ContentLength
            };
        }

        private static string GetClientCoverMetadataPath(string baseTarget)
        {
            return baseTarget + ".metadata.json";
        }

        private static string GetNoticeContentPath(string iconName)
        {
            return Path.Combine(GetGameCoverDir(iconName), NoticeContentFileName);
        }

        private static string GetCoverRefreshStampPath(string dir)
        {
            return Path.Combine(dir, ClientCoverRefreshStampFileName);
        }

        private static string ReadCoverRefreshStamp(string dir)
        {
            try
            {
                var path = GetCoverRefreshStampPath(dir);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : "";
            }
            catch
            {
                return "";
            }
        }

        private static bool IsLikelyImagePath(string pathOrUrl)
        {
            var extension = GetImageExtension(pathOrUrl);
            return IsSupportedImageExtension(extension);
        }

        private static bool IsSupportedImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            return ImageExtensions.Contains(NormalizeImageExtension(extension), StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
            return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        }

        private static string GetImageExtension(string pathOrUrl)
        {
            try
            {
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri))
                    return NormalizeImageExtension(Path.GetExtension(uri.LocalPath));
            }
            catch { }

            return NormalizeImageExtension(Path.GetExtension(pathOrUrl));
        }

        private static string GetImageExtensionFromContentType(string contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/pjpeg" => ".jpg",
                "image/png" or "image/x-png" => ".png",
                "image/bmp" or "image/x-ms-bmp" => ".bmp",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => string.Empty
            };
        }

        private static string HashUrl(string imageUrl)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl));
            return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
        }

        private static string SanitizeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }

        private static void CleanupOldCovers(string dir, string keepPath)
        {
            foreach (var file in Directory.GetFiles(dir, ClientCoverPattern))
            {
                if (string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase)) continue;
                TryDelete(file);
            }

            foreach (var file in Directory.GetFiles(dir, "cover-*"))
                TryDelete(file);
        }

        private static void MoveReplacing(string source, string destination)
        {
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(source, destination);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private sealed class LauncherNoticeCacheDto
        {
            public List<LauncherBannerItem> Banners { get; set; } = new();
            public List<LauncherNoticeItem> Notices { get; set; } = new();
        }

        private sealed class ClientCoverMetadata
        {
            public string ETag { get; set; } = "";
            public string LastModified { get; set; } = "";
            public long? ContentLength { get; set; }

            public bool HasValidator =>
                !string.IsNullOrWhiteSpace(ETag) ||
                !string.IsNullOrWhiteSpace(LastModified) ||
                ContentLength.HasValue;

            public bool Matches(ClientCoverMetadata other)
            {
                if (other == null) return false;

                if (!string.IsNullOrWhiteSpace(ETag) && !string.IsNullOrWhiteSpace(other.ETag))
                    return string.Equals(ETag, other.ETag, StringComparison.Ordinal);

                if (!string.IsNullOrWhiteSpace(LastModified) && !string.IsNullOrWhiteSpace(other.LastModified))
                    return string.Equals(LastModified, other.LastModified, StringComparison.Ordinal);

                return ContentLength.HasValue &&
                       other.ContentLength.HasValue &&
                       ContentLength.Value == other.ContentLength.Value;
            }
        }
    }
}
