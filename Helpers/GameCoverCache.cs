using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace XelLauncher.Helpers
{
    internal static class GameCoverCache
    {
        private const string ClientCoverPattern = "client-cover-*";

        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly string[] ImageExtensions =
            [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

        public static string GetCachedCoverPath(string iconName)
        {
            var dir = GetGameCoverDir(iconName);
            if (!Directory.Exists(dir)) return null;

            return Directory.GetFiles(dir, ClientCoverPattern)
                .Where(IsLikelyImagePath)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault(IsLoadableImage);
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

        public static async Task<string> UpdateAsync(string iconName, string imageUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var dir = GetGameCoverDir(iconName);
            Directory.CreateDirectory(dir);

            var urlExtension = GetImageExtension(imageUrl);
            var baseTarget = Path.Combine(dir, $"client-cover-{HashUrl(imageUrl)}");
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
                        CleanupOldCovers(dir, pngPath);
                        LogHelper.Log($"Game cover cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    var webpPath = baseTarget + ".webp";
                    MoveReplacing(temp, webpPath);
                    CleanupOldCovers(dir, webpPath);
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
                        LogHelper.Log($"Game cover cached: {iconName} -> {pngPath}");
                        return pngPath;
                    }

                    TryDelete(temp);
                    return null;
                }

                MoveReplacing(temp, target);
                CleanupOldCovers(dir, target);
                LogHelper.Log($"Game cover cached: {iconName} -> {target}");
                return target;
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                LogHelper.LogError(ex, $"GameCoverCache.UpdateAsync({iconName})");
                return null;
            }
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
    }
}
