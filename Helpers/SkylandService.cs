using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers
{
    public sealed class SkylandService
    {
        private const int MinAccountSignDelaySeconds = 10;
        private const int MaxAccountSignDelaySeconds = 20;

        private const string AppCode = "4ca99fa6b56cc2ba";
        private const string UserAgent = "Mozilla/5.0 (Linux; Android 12; SM-A5560 Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36; SKLand/1.52.1";
        private const string ScanLoginUrl = "https://as.hypergryph.com/general/v1/gen_scan/login";
        private const string ScanStatusUrl = "https://as.hypergryph.com/general/v1/scan_status";
        private const string TokenByScanCodeUrl = "https://as.hypergryph.com/user/auth/v1/token_by_scan_code";
        private const string SendPhoneCodeUrl = "https://as.hypergryph.com/general/v1/send_phone_code";
        private const string TokenByPhoneCodeUrl = "https://as.hypergryph.com/user/auth/v2/token_by_phone_code";
        private const string TokenByPasswordUrl = "https://as.hypergryph.com/user/auth/v1/token_by_phone_password";
        private const string GrantCodeUrl = "https://as.hypergryph.com/user/oauth2/v2/grant";
        private const string CredCodeUrl = "https://zonai.skland.com/web/v1/user/auth/generate_cred_by_code";
        private const string BindingUrl = "https://zonai.skland.com/api/v1/game/player/binding";
        private const string ArknightsSignUrl = "https://zonai.skland.com/api/v1/game/attendance";
        private const string EndfieldSignUrl = "https://zonai.skland.com/web/v1/game/endfield/attendance";

        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        public async Task<SkylandScanLogin> CreateScanLoginAsync(CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, ScanLoginUrl);
            request.Content = JsonContent("{}");
            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureAuthOk(root, AntdUI.Localization.Get("App.Skyland.Action.CreateScanLogin", "创建扫码登录"));

            var data = root["data"];
            var scanId = data?["scanId"]?.GetValue<string>() ?? "";
            var scanUrl = data?["scanUrl"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(scanId) || string.IsNullOrWhiteSpace(scanUrl))
                throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.ScanMissing", "创建扫码登录失败：返回结果缺少 scanId 或 scanUrl。"));

            return new SkylandScanLogin(scanId, scanUrl);
        }

        public async Task<SkylandScanStatus> GetScanStatusAsync(string scanId, CancellationToken cancellationToken = default)
        {
            var url = $"{ScanStatusUrl}?scanId={Uri.EscapeDataString(scanId)}";
            using var request = CreateRequest(HttpMethod.Get, url);
            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);

            var status = root["status"]?.GetValue<int?>() ?? root["code"]?.GetValue<int?>() ?? -1;
            var message = root["msg"]?.GetValue<string>() ?? root["message"]?.GetValue<string>() ?? "";
            var scanCode = root["data"]?["scanCode"]?.GetValue<string>() ?? "";
            return new SkylandScanStatus(status, message, scanCode);
        }

        public async Task<string> WaitForScanTokenAsync(string scanId, IProgress<string> progress, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var status = await GetScanStatusAsync(scanId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(status.ScanCode))
                    return await LoginByScanCodeAsync(status.ScanCode, cancellationToken).ConfigureAwait(false);

                if (status.Status == 100) progress?.Report(AntdUI.Localization.Get("App.Skyland.Progress.WaitScan", "等待扫码..."));
                else if (status.Status == 101) progress?.Report(AntdUI.Localization.Get("App.Skyland.Progress.WaitConfirm", "已扫码，等待在森空岛 App 内确认登录..."));
                else if (status.Status == 102) throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.QrExpired", "二维码已过期，请重新生成。"));
                else progress?.Report(string.Format(AntdUI.Localization.Get("App.Skyland.Progress.ScanStatus", "等待扫码状态：{0} {1}"), status.Status, status.Message).Trim());

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(AntdUI.Localization.Get("App.Skyland.Error.ScanTimeout", "扫码登录超时，请重新生成二维码。"));
        }

        public async Task<string> LoginByScanCodeAsync(string scanCode, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, TokenByScanCodeUrl);
            request.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["scanCode"] = scanCode
            }));

            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureAuthOk(root, AntdUI.Localization.Get("App.Skyland.Action.ScanLogin", "扫码登录"));

            var data = root["data"];
            var token = data?["content"]?.GetValue<string>() ?? data?["token"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.ScanTokenMissing", "扫码登录失败：返回结果缺少 token。"));

            return token;
        }

        public async Task SendPhoneCodeAsync(string phone, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException(AntdUI.Localization.Get("App.Skyland.Error.PhoneRequired", "请输入手机号"), nameof(phone));

            using var request = CreateRequest(HttpMethod.Post, SendPhoneCodeUrl);
            request.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["phone"] = phone.Trim(),
                ["type"] = 2
            }));

            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureAuthOk(root, AntdUI.Localization.Get("App.Skyland.Action.SendCode", "发送验证码"));
        }

        public async Task<string> LoginByPhoneCodeAsync(string phone, string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException(AntdUI.Localization.Get("App.Skyland.Error.PhoneRequired", "请输入手机号"), nameof(phone));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException(AntdUI.Localization.Get("App.Skyland.Error.CodeRequired", "请输入短信验证码。"), nameof(code));

            using var request = CreateRequest(HttpMethod.Post, TokenByPhoneCodeUrl);
            request.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["phone"] = phone.Trim(),
                ["code"] = code.Trim()
            }));

            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ExtractLoginToken(root, AntdUI.Localization.Get("App.Skyland.Action.SmsLogin", "手机号验证码登录"));
        }

        public async Task<string> LoginByPasswordAsync(string phone, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException(AntdUI.Localization.Get("App.Skyland.Error.AccountRequired", "请输入账号"), nameof(phone));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException(AntdUI.Localization.Get("App.Skyland.Error.PasswordRequired", "请输入密码"), nameof(password));

            using var request = CreateRequest(HttpMethod.Post, TokenByPasswordUrl);
            request.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["phone"] = phone.Trim(),
                ["password"] = password
            }));

            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ExtractLoginToken(root, AntdUI.Localization.Get("App.Skyland.Action.PasswordLogin", "账号密码登录"));
        }

        public async Task<List<string>> SignAllAsync(IEnumerable<string> tokens, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            var messages = new List<string>();
            var tokenList = tokens.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (tokenList.Count == 0) throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.TokenRequiredEnv", "请先添加 SKYLAND_TOKEN。"));

            for (var i = 0; i < tokenList.Count; i++)
            {
                var accountLabel = string.Format(AntdUI.Localization.Get("App.Skyland.Log.Account", "账号 {0}"), i + 1);
                progress?.Report(string.Format(AntdUI.Localization.Get("App.Skyland.Log.ProcessAccount", "开始处理{0}..."), accountLabel));

                try
                {
                    var session = await LoginByTokenAsync(ParseUserToken(tokenList[i]), cancellationToken).ConfigureAwait(false);
                    var bindings = await GetBindingListAsync(session, cancellationToken).ConfigureAwait(false);

                    if (bindings.Count == 0)
                    {
                        var message = string.Format(AntdUI.Localization.Get("App.Skyland.Log.NoBindings", "[{0}] 未找到可签到的明日方舟或终末地绑定角色。"), accountLabel);
                        messages.Add(message);
                        progress?.Report(message);
                        continue;
                    }

                    foreach (var binding in bindings)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string result = binding.AppCode switch
                        {
                            "arknights" => await SignArknightsAsync(binding, session, cancellationToken).ConfigureAwait(false),
                            "endfield" => await SignEndfieldAsync(binding, session, cancellationToken).ConfigureAwait(false),
                            _ => ""
                        };

                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            result = $"[{accountLabel}] {result}";
                            messages.Add(result);
                            progress?.Report(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var message = string.Format(AntdUI.Localization.Get("App.Skyland.Log.AccountFailed", "[{0}] 签到失败：{1}"), accountLabel, ex.Message);
                    messages.Add(message);
                    progress?.Report(message);
                }

                if (i < tokenList.Count - 1)
                {
                    var delaySeconds = Random.Shared.Next(MinAccountSignDelaySeconds, MaxAccountSignDelaySeconds + 1);
                    progress?.Report(string.Format(AntdUI.Localization.Get("App.Skyland.Log.WaitNext", "等待 {0} 秒后继续处理下一个账号..."), delaySeconds));
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
                }
            }

            return messages;
        }

        public static List<string> SplitTokens(string value)
        {
            return (value ?? "")
                .Replace("\r", "\n")
                .Replace(';', '\n')
                .Replace(',', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        private async Task<SkylandSession> LoginByTokenAsync(string token, CancellationToken cancellationToken)
        {
            using var grantRequest = CreateRequest(HttpMethod.Post, GrantCodeUrl);
            grantRequest.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["appCode"] = AppCode,
                ["token"] = token,
                ["type"] = 0
            }));
            var grantRoot = await SendJsonAsync(grantRequest, cancellationToken).ConfigureAwait(false);
            EnsureAuthOk(grantRoot, AntdUI.Localization.Get("App.Skyland.Action.GetGrantCode", "使用 token 获取授权码"));
            var grantCode = grantRoot["data"]?["code"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(grantCode))
                throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.GrantCodeMissing", "使用 token 获取授权码失败：返回结果缺少 code。"));

            using var credRequest = CreateRequest(HttpMethod.Post, CredCodeUrl);
            credRequest.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["code"] = grantCode,
                ["kind"] = 1
            }));
            var credRoot = await SendJsonAsync(credRequest, cancellationToken).ConfigureAwait(false);
            EnsureApiOk(credRoot, AntdUI.Localization.Get("App.Skyland.Action.GetCred", "获取 cred"));

            var data = credRoot["data"];
            var cred = data?["cred"]?.GetValue<string>() ?? "";
            var signToken = data?["token"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(cred) || string.IsNullOrWhiteSpace(signToken))
                throw new InvalidOperationException(AntdUI.Localization.Get("App.Skyland.Error.CredMissing", "获取 cred 失败：返回结果缺少 cred 或 token。"));

            return new SkylandSession(cred, signToken);
        }

        private async Task<List<SkylandBinding>> GetBindingListAsync(SkylandSession session, CancellationToken cancellationToken)
        {
            using var request = CreateSignedRequest(HttpMethod.Get, BindingUrl, null, session);
            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureApiOk(root, AntdUI.Localization.Get("App.Skyland.Action.GetBindings", "获取绑定角色列表"));

            var result = new List<SkylandBinding>();
            foreach (var game in root["data"]?["list"]?.AsArray() ?? new JsonArray())
            {
                var appCode = game?["appCode"]?.GetValue<string>() ?? "";
                if (appCode is not ("arknights" or "endfield")) continue;

                foreach (var item in game?["bindingList"]?.AsArray() ?? new JsonArray())
                {
                    if (item is not JsonObject obj) continue;
                    result.Add(ParseBinding(appCode, obj));
                }
            }

            return result;
        }

        private async Task<string> SignArknightsAsync(SkylandBinding binding, SkylandSession session, CancellationToken cancellationToken)
        {
            var body = ToPythonStyleJson(new[]
            {
                new KeyValuePair<string, string>("uid", binding.Uid),
                new KeyValuePair<string, string>("gameId", binding.ChannelMasterId)
            });

            using var request = CreateSignedRequest(HttpMethod.Post, ArknightsSignUrl, body, session);
            request.Content = JsonContent(body);
            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);

            var title = string.Format(AntdUI.Localization.Get("App.Skyland.Log.RoleTitle", "[{0}] 角色 {1}({2})"), binding.GameName, binding.Nickname, binding.ChannelName);
            if (!IsApiOk(root))
                return string.Format(AntdUI.Localization.Get("App.Skyland.Log.SignFailed", "{0} 签到失败：{1}"), title, GetErrorMessage(root));

            var awards = new List<string>();
            foreach (var item in root["data"]?["awards"]?.AsArray() ?? new JsonArray())
            {
                var resource = item?["resource"];
                var name = resource?["name"]?.GetValue<string>() ?? "";
                var count = item?["count"]?.GetValue<int?>() ?? 1;
                if (!string.IsNullOrWhiteSpace(name)) awards.Add($"{name}x{count}");
            }

            return string.Format(AntdUI.Localization.Get("App.Skyland.Log.SignSuccess", "{0} 签到成功，获得 {1}"), title, string.Join(AntdUI.Localization.Get("App.Skyland.ListSeparator", "、"), awards));
        }

        private async Task<string> SignEndfieldAsync(SkylandBinding binding, SkylandSession session, CancellationToken cancellationToken)
        {
            var results = new List<string>();
            foreach (var role in binding.Roles)
            {
                using var request = CreateSignedRequest(HttpMethod.Post, EndfieldSignUrl, "", session);
                request.Content = JsonContent("");
                request.Headers.TryAddWithoutValidation("sk-game-role", $"3_{role.RoleId}_{role.ServerId}");
                request.Headers.TryAddWithoutValidation("referer", "https://game.skland.com/");
                request.Headers.TryAddWithoutValidation("origin", "https://game.skland.com/");

                var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
                var title = string.Format(AntdUI.Localization.Get("App.Skyland.Log.RoleTitle", "[{0}] 角色 {1}({2})"), binding.GameName, role.Nickname, binding.ChannelName);
                if (!IsApiOk(root))
                {
                    results.Add(string.Format(AntdUI.Localization.Get("App.Skyland.Log.SignFailed", "{0} 签到失败：{1}"), title, GetErrorMessage(root)));
                    continue;
                }

                var awards = new List<string>();
                var infoMap = root["data"]?["resourceInfoMap"]?.AsObject();
                foreach (var award in root["data"]?["awardIds"]?.AsArray() ?? new JsonArray())
                {
                    var id = award?["id"]?.GetValue<string>() ?? "";
                    if (infoMap == null || string.IsNullOrEmpty(id) || infoMap[id] == null) continue;

                    var info = infoMap[id];
                    var name = info?["name"]?.GetValue<string>() ?? "";
                    var count = info?["count"]?.GetValue<int?>() ?? 1;
                    if (!string.IsNullOrWhiteSpace(name)) awards.Add($"{name}x{count}");
                }

                results.Add(string.Format(AntdUI.Localization.Get("App.Skyland.Log.SignSuccess", "{0} 签到成功，获得 {1}"), title, string.Join(AntdUI.Localization.Get("App.Skyland.ListSeparator", "、"), awards)));
            }

            return string.Join(Environment.NewLine, results);
        }

        private HttpRequestMessage CreateSignedRequest(HttpMethod method, string url, string body, SkylandSession session)
        {
            var request = CreateRequest(method, url);
            request.Headers.TryAddWithoutValidation("cred", session.Cred);

            var uri = new Uri(url);
            var bodyOrQuery = method == HttpMethod.Get ? uri.Query.TrimStart('?') : body ?? "";
            var (sign, headers) = GenerateSignature(session.SignToken, uri.AbsolutePath, bodyOrQuery);
            request.Headers.TryAddWithoutValidation("sign", sign);
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return request;
        }

        private static (string Sign, Dictionary<string, string> Headers) GenerateSignature(string token, string path, string bodyOrQuery)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
            var headers = new Dictionary<string, string>
            {
                ["platform"] = "3",
                ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
                ["dId"] = "",
                ["vName"] = "1.0.0"
            };

            var headerJson = $"{{\"platform\":\"3\",\"timestamp\":\"{headers["timestamp"]}\",\"dId\":\"\",\"vName\":\"1.0.0\"}}";
            var source = path + (bodyOrQuery ?? "") + headers["timestamp"] + headerJson;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(token));
            var hmacHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
            var md5Hex = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(hmacHex))).ToLowerInvariant();
            return (md5Hex, headers);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
            request.Headers.TryAddWithoutValidation("Connection", "close");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "com.hypergryph.skland");
            return request;
        }

        private static StringContent JsonContent(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static async Task<JsonNode> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {text}");

            return JsonNode.Parse(text) ?? new JsonObject();
        }

        private static void EnsureAuthOk(JsonNode root, string action)
        {
            var status = root["status"]?.GetValue<int?>() ?? root["code"]?.GetValue<int?>() ?? -1;
            if (status != 0)
                throw new InvalidOperationException(string.Format(AntdUI.Localization.Get("App.Skyland.Error.ActionFailed", "{0}失败：{1}"), action, GetErrorMessage(root)));
        }

        private static string ExtractLoginToken(JsonNode root, string action)
        {
            EnsureAuthOk(root, action);
            var token = root["data"]?["token"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(string.Format(AntdUI.Localization.Get("App.Skyland.Error.TokenMissing", "{0}失败：返回结果缺少 token。"), action));
            return token;
        }

        private static void EnsureApiOk(JsonNode root, string action)
        {
            if (!IsApiOk(root))
                throw new InvalidOperationException(string.Format(AntdUI.Localization.Get("App.Skyland.Error.ActionFailed", "{0}失败：{1}"), action, GetErrorMessage(root)));
        }

        private static bool IsApiOk(JsonNode root)
        {
            return (root["code"]?.GetValue<int?>() ?? -1) == 0;
        }

        private static string GetErrorMessage(JsonNode root)
        {
            return root["message"]?.GetValue<string>()
                ?? root["msg"]?.GetValue<string>()
                ?? root.ToJsonString();
        }

        private static string ParseUserToken(string value)
        {
            try
            {
                var root = JsonNode.Parse(value);
                return root?["data"]?["content"]?.GetValue<string>() ?? value;
            }
            catch
            {
                return value;
            }
        }

        private static SkylandBinding ParseBinding(string appCode, JsonObject obj)
        {
            var roles = new List<SkylandRole>();
            foreach (var role in obj["roles"]?.AsArray() ?? new JsonArray())
            {
                roles.Add(new SkylandRole(
                    role?["roleId"]?.GetValue<string>() ?? "",
                    role?["serverId"]?.GetValue<string>() ?? "",
                    role?["nickname"]?.GetValue<string>() ?? ""));
            }

            return new SkylandBinding(
                appCode,
                obj["gameName"]?.GetValue<string>() ?? appCode,
                obj["channelName"]?.GetValue<string>() ?? "",
                obj["nickName"]?.GetValue<string>() ?? "",
                obj["uid"]?.GetValue<string>() ?? "",
                obj["channelMasterId"]?.GetValue<string>() ?? "",
                roles);
        }

        private static string ToPythonStyleJson(IEnumerable<KeyValuePair<string, string>> values)
        {
            return "{" + string.Join(", ", values.Select(x =>
                $"{JsonSerializer.Serialize(x.Key)}: {JsonSerializer.Serialize(x.Value)}")) + "}";
        }
    }

    public sealed record SkylandScanLogin(string ScanId, string ScanUrl);
    public sealed record SkylandScanStatus(int Status, string Message, string ScanCode);
    public sealed record SkylandSession(string Cred, string SignToken);
    public sealed record SkylandBinding(string AppCode, string GameName, string ChannelName, string Nickname, string Uid, string ChannelMasterId, List<SkylandRole> Roles);
    public sealed record SkylandRole(string RoleId, string ServerId, string Nickname);
}
