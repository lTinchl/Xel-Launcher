using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers
{
    internal static class SkylandDeviceIdProvider
    {
        private const string Organization = "UWXspnCCJN4sfYlNfqps";
        private const string AppId = "default";
        private const string DeviceProfileUrl = "https://fp-it.portal101.cn/deviceprofile/v4";
        private const string PublicKey =
            "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCmxMNr7n8ZeT0tE1R9j/" +
            "mPixoinPkeM+k4VGIn/s0k7N5rJAfnZ0eMER+QhwFvshzo0LNmeUkpR8uIlU/" +
            "GEVr8mN28sKmwd2gpygqj0ePnBmOW4v0ZVwbSYK+izkhVFk2V/doLoMbWy6b+" +
            "UnA8mkjvg0iYWRByfRsK2gdl7llqCwIDAQAB";

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly SemaphoreSlim GenerateLock = new(1, 1);
        private static readonly object CacheLock = new();
        private static string _cachedDeviceId = "";

        private static readonly Dictionary<string, DesFieldRule> DesRules = new(StringComparer.Ordinal)
        {
            ["appId"] = new(true, "uy7mzc4h", "xx"),
            ["box"] = new(false, "", "jf"),
            ["canvas"] = new(true, "snrn887t", "yk"),
            ["clientSize"] = new(true, "cpmjjgsu", "zx"),
            ["organization"] = new(true, "78moqjfc", "dp"),
            ["os"] = new(true, "je6vk6t4", "pj"),
            ["platform"] = new(true, "pakxhcd2", "gm"),
            ["plugins"] = new(true, "v51m3pzl", "kq"),
            ["pmf"] = new(true, "2mdeslu3", "vw"),
            ["protocol"] = new(false, "", "protocol"),
            ["referer"] = new(true, "y7bmrjlc", "ab"),
            ["res"] = new(true, "whxqm2a7", "hf"),
            ["rtype"] = new(true, "x8o2h2bl", "lo"),
            ["sdkver"] = new(true, "9q3dcxp2", "sc"),
            ["status"] = new(true, "2jbrxxw4", "an"),
            ["subVersion"] = new(true, "eo3i2puh", "ns"),
            ["svm"] = new(true, "fzj3kaeh", "qr"),
            ["time"] = new(true, "q2t3odsk", "nb"),
            ["timezone"] = new(true, "1uv05lj5", "as"),
            ["tn"] = new(true, "x9nzj1bp", "py"),
            ["trees"] = new(true, "acfs0xo4", "pi"),
            ["ua"] = new(true, "k92crp1t", "bj"),
            ["url"] = new(true, "y95hjkoo", "cf"),
            ["version"] = new(false, "", "version"),
            ["vpw"] = new(true, "r9924ab5", "ca")
        };

        public static async Task<string> GetDeviceIdAsync(CancellationToken cancellationToken)
        {
            lock (CacheLock)
            {
                if (!string.IsNullOrWhiteSpace(_cachedDeviceId))
                    return _cachedDeviceId;
            }

            await GenerateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (CacheLock)
                {
                    if (!string.IsNullOrWhiteSpace(_cachedDeviceId))
                        return _cachedDeviceId;
                }

                var deviceId = await GenerateDeviceIdAsync(cancellationToken).ConfigureAwait(false);
                lock (CacheLock)
                    _cachedDeviceId = deviceId;
                return deviceId;
            }
            finally
            {
                GenerateLock.Release();
            }
        }

        public static void Invalidate(string deviceId)
        {
            lock (CacheLock)
            {
                if (string.Equals(_cachedDeviceId, deviceId, StringComparison.Ordinal))
                    _cachedDeviceId = "";
            }
        }

        private static async Task<string> GenerateDeviceIdAsync(CancellationToken cancellationToken)
        {
            var uid = Guid.NewGuid().ToString();
            var uidBytes = Encoding.UTF8.GetBytes(uid);
            var privateId = Md5Hex(uidBytes)[..16];

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey), out _);
            var encryptedUid = rsa.Encrypt(uidBytes, RSAEncryptionPadding.Pkcs1);

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var fingerprint = CreateFingerprint(nowMs);
            fingerprint["tn"] = Md5Hex(Encoding.UTF8.GetBytes(JoinFingerprintValues(fingerprint)));

            var encryptedFields = EncryptDesFields(fingerprint);
            var compactJson = JsonSerializer.Serialize(encryptedFields);
            var compressedJson = Gzip(Encoding.UTF8.GetBytes(compactJson));
            var encodedCompressedJson = Encoding.ASCII.GetBytes(Convert.ToBase64String(compressedJson));
            var encryptedData = EncryptAes(encodedCompressedJson, Encoding.UTF8.GetBytes(privateId));

            var requestJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["appId"] = AppId,
                ["compress"] = 2,
                ["data"] = Convert.ToHexString(encryptedData).ToLowerInvariant(),
                ["encode"] = 5,
                ["ep"] = Convert.ToBase64String(encryptedUid),
                ["organization"] = Organization,
                ["os"] = "web"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, DeviceProfileUrl)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(FormatGenerationError($"HTTP {(int)response.StatusCode}: {responseText}"));

            var root = JsonNode.Parse(responseText);
            var code = root?["code"]?.GetValue<int?>() ?? -1;
            var deviceId = root?["detail"]?["deviceId"]?.GetValue<string>() ?? "";
            if (code != 1100 || string.IsNullOrWhiteSpace(deviceId))
                throw new InvalidOperationException(FormatGenerationError(responseText));

            return "B" + deviceId;
        }

        private static Dictionary<string, object> CreateFingerprint(long nowMs)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["plugins"] =
                    "MicrosoftEdgePDFPluginPortableDocumentFormatinternal-pdf-viewer1," +
                    "MicrosoftEdgePDFViewermhjfbmdgcfjbbpaeojofohoefgiehjai1",
                ["ua"] =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 " +
                    "Safari/537.36 Edg/129.0.0.0",
                ["canvas"] = "259ffe69",
                ["timezone"] = -480,
                ["platform"] = "Win32",
                ["url"] = "https://www.skland.com/",
                ["referer"] = "",
                ["res"] = "1920_1080_24_1.25",
                ["clientSize"] = "0_0_1080_1920_1920_1080_1920_1080",
                ["status"] = "0011",
                ["vpw"] = Guid.NewGuid().ToString(),
                ["svm"] = nowMs,
                ["trees"] = Guid.NewGuid().ToString(),
                ["pmf"] = nowMs,
                ["protocol"] = 102,
                ["organization"] = Organization,
                ["appId"] = AppId,
                ["os"] = "web",
                ["version"] = "3.0.0",
                ["sdkver"] = "3.0.0",
                ["box"] = "",
                ["rtype"] = "all",
                ["smid"] = MakeSmid(),
                ["subVersion"] = "1.0.0",
                ["time"] = 0
            };
        }

        private static Dictionary<string, object> EncryptDesFields(Dictionary<string, object> values)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (!DesRules.TryGetValue(pair.Key, out var rule))
                {
                    result[pair.Key] = pair.Value;
                    continue;
                }

                result[rule.OutputName] = rule.Encrypt
                    ? Convert.ToBase64String(EncryptDes(
                        Encoding.UTF8.GetBytes(ToPythonString(pair.Value)),
                        Encoding.UTF8.GetBytes(rule.Key)))
                    : pair.Value;
            }

            return result;
        }

        private static byte[] EncryptDes(byte[] data, byte[] key)
        {
            using var des = DES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None;
            des.Key = key;
            using var encryptor = des.CreateEncryptor();
            var padded = PadWithZeroBlock(data, 8);
            return encryptor.TransformFinalBlock(padded, 0, padded.Length);
        }

        private static byte[] EncryptAes(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            aes.IV = Encoding.ASCII.GetBytes("0102030405060708");
            using var encryptor = aes.CreateEncryptor();
            var padded = PadWithZeroBlock(data, 16);
            return encryptor.TransformFinalBlock(padded, 0, padded.Length);
        }

        private static byte[] PadWithZeroBlock(byte[] data, int blockSize)
        {
            var paddingLength = blockSize - data.Length % blockSize;
            var padded = new byte[data.Length + paddingLength];
            Buffer.BlockCopy(data, 0, padded, 0, data.Length);
            return padded;
        }

        private static byte[] Gzip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
                gzip.Write(data, 0, data.Length);
            return output.ToArray();
        }

        private static string JoinFingerprintValues(Dictionary<string, object> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            foreach (var key in keys)
            {
                var value = values[key];
                switch (value)
                {
                    case int intValue:
                        builder.Append((long)intValue * 10000L);
                        break;
                    case long longValue:
                        builder.Append(longValue * 10000L);
                        break;
                    default:
                        builder.Append(ToPythonString(value));
                        break;
                }
            }
            return builder.ToString();
        }

        private static string MakeSmid()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var randomHash = Md5Hex(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            var value = timestamp + randomHash + "00";
            var suffix = Md5Hex(Encoding.UTF8.GetBytes("smsk_web_" + value))[..14];
            return value + suffix + "0";
        }

        private static string ToPythonString(object value)
        {
            return value switch
            {
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? ""
            };
        }

        private static string Md5Hex(byte[] data)
        {
            return Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
        }

        private static string FormatGenerationError(string detail)
        {
            return string.Format(
                AntdUI.Localization.Get(
                    "App.Skyland.Error.DeviceProfileFailed",
                    "生成森空岛设备信息失败：{0}"),
                detail);
        }

        private sealed record DesFieldRule(bool Encrypt, string Key, string OutputName);
    }
}
