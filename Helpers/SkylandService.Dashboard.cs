using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers;

public sealed partial class SkylandService
{
    private static readonly SemaphoreSlim DashboardRolesLock = new(1, 1);
    private static readonly Dictionary<string, (DashboardRoleList List, long Expires)> DashboardRolesCache = new();

    internal static string DashboardTokenKey(IEnumerable<string> tokens) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens))));

    internal async Task<DashboardRoleList> GetDashboardRolesAsync(string iconName, CancellationToken cancellationToken, bool force = false)
    {
        var definition = GameChannelCatalog.Get(iconName);
        if (definition == null || definition.Channel is not ("Official" or "Bilibili"))
            return new(new(), "App.Game.Dashboard.TimeOnly");
        var tokens = SkylandTokenStorage.GetTokens(ConfigHelper.Load()).ToArray();
        if (tokens.Length == 0) return new(new(), "App.Game.Dashboard.LoginRequired");
        var account = DashboardTokenKey(tokens);
        var key = definition.IconName + ":" + account;
        await DashboardRolesLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var expired in DashboardRolesCache.Where(x => x.Value.Expires <= Environment.TickCount64).Select(x => x.Key).ToArray())
                DashboardRolesCache.Remove(expired);
            if (!force && DashboardRolesCache.TryGetValue(key, out var cached)) return cached.List;
            var roles = new Dictionary<string, DashboardRole>(StringComparer.Ordinal);
            bool failed = false;
            foreach (var token in tokens)
            {
                try
                {
                    var session = await LoginByTokenAsync(ParseUserToken(token), cancellationToken).ConfigureAwait(false);
                    var bindings = await GetBindingListAsync(session, cancellationToken).ConfigureAwait(false);
                    AddDashboardRoles(roles, definition, account, token, bindings);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                // One invalid credential must not hide characters from other accounts.
                // Do not log exception messages: authentication responses may contain secrets.
                catch { failed = true; }
            }
            var result = new DashboardRoleList(roles.Values.ToList(), failed
                ? "App.Game.Dashboard.LoadFailed" : "App.Game.Dashboard.NoRole");
            if (result.Roles.Count > 0)
                DashboardRolesCache[key] = (result, Environment.TickCount64 + DashboardCacheLifetimeMs);
            else DashboardRolesCache.Remove(key);
            return result;
        }
        finally { DashboardRolesLock.Release(); }
    }

    internal static void AddDashboardRoles(Dictionary<string, DashboardRole> roles, GameChannelDefinition definition,
        string account, string token, IEnumerable<SkylandBinding> bindings)
    {
        var appCode = definition.Family == GameFamily.Endfield ? "endfield" : "arknights";
        var channel = definition.Channel == "Bilibili" ? "2" : "1";
        foreach (var binding in bindings.Where(x => x.AppCode == appCode && x.ChannelMasterId == channel))
        {
            var candidates = appCode == "endfield"
                ? binding.Roles.Select(role => (Uid: role.RoleId, Name: role.Nickname, Binding: binding with { Roles = new() { role } }))
                : new[] { (Uid: binding.Uid, Name: binding.Nickname, Binding: binding) };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.Uid)) continue;
                if (!roles.TryGetValue(candidate.Uid, out var role))
                    roles[candidate.Uid] = role = new DashboardRole(candidate.Uid, candidate.Name, definition.IconName, account, appCode == "endfield");
                role.AddCredential(token, candidate.Binding);
            }
        }
    }

    internal Task<string> GetDashboardAsync(DashboardRole role, CancellationToken cancellationToken)
    {
        var key = $"{role.IconName}:{role.AccountKey}:{role.Uid}:{AntdUI.Localization.CurrentLanguage}:{CultureInfo.CurrentUICulture.Name}";
        return GetCachedDashboardAsync(key, async () =>
        {
            foreach (var candidate in role.Credentials.ToArray())
            {
                try
                {
                    var session = await LoginByTokenAsync(ParseUserToken(candidate.Token), cancellationToken).ConfigureAwait(false);
                    // Revalidate binding when refreshing expired data, including unbound characters.
                    var bindings = await GetBindingListAsync(session, cancellationToken).ConfigureAwait(false);
                    var matches = new Dictionary<string, DashboardRole>(StringComparer.Ordinal);
                    AddDashboardRoles(matches, GameChannelCatalog.Get(role.IconName), role.AccountKey, candidate.Token, bindings);
                    if (!matches.TryGetValue(role.Uid, out var current)) continue;
                    var binding = current.Credentials[0].Binding;
                    if (role.IsEndfield)
                    {
                        var result = await GetEndfieldDashboardAsync(binding, session, cancellationToken).ConfigureAwait(false);
                        if (result.Success) { role.PreferCredential(candidate); return result; }
                        continue;
                    }
                    var url = "https://zonai.skland.com/api/v1/game/player/info?uid=" + Uri.EscapeDataString(role.Uid);
                    using var request = CreateSignedRequest(HttpMethod.Get, url, null, session);
                    var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
                    EnsureApiOk(root, Localizer.GetRequiredString("App.Game.Dashboard.FetchData"));
                    var data = root["data"];
                    if (data != null) { role.PreferCredential(candidate); return (FormatArknightsDashboard(data, binding.Nickname), true); }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { }
            }
            throw new InvalidOperationException(Localizer.GetRequiredString("App.Game.Dashboard.LoadFailed"));
        }, cancellationToken);
    }
}

// Credentials stay in memory, never in configuration, labels, or generated record ToString().
internal sealed class DashboardRole
{
    public string Uid { get; }
    public string Nickname { get; }
    public string IconName { get; }
    public string AccountKey { get; }
    public bool IsEndfield { get; }
    internal List<DashboardCredential> Credentials { get; } = new();
    public DashboardRole(string uid, string nickname, string iconName, string accountKey, bool isEndfield)
    { Uid = uid; Nickname = nickname; IconName = iconName; AccountKey = accountKey; IsEndfield = isEndfield; }
    internal void AddCredential(string token, SkylandBinding binding)
    {
        if (!Credentials.Any(x => x.Token == token)) Credentials.Add(new DashboardCredential(token, binding));
    }
    internal void PreferCredential(DashboardCredential credential)
    {
        Credentials.Remove(credential);
        Credentials.Insert(0, credential);
    }
    public string Placeholder => IsEndfield
        ? SkylandService.FormatEndfieldDashboard(new JsonObject(), Nickname)
        : SkylandService.FormatArknightsDashboard(null, Nickname);
}

internal sealed class DashboardCredential
{
    internal string Token { get; }
    internal SkylandBinding Binding { get; }
    internal DashboardCredential(string token, SkylandBinding binding) { Token = token; Binding = binding; }
}

internal sealed record DashboardRoleList(List<DashboardRole> Roles, string EmptyMessageKey);
