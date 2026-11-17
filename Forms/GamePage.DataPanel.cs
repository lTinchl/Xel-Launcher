using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XelLauncher.Helpers;

namespace XelLauncher.Forms;

public partial class GamePage
{
    private GameDataPanel _gameDataPanel;
    private bool _gameDataLoading;
    private List<DashboardRole> _gameDataRoles = new();
    private string _gameDataSelectedUid;
    private string _gameDataTokenKey;
    private int _gameDataVersion;
    private CancellationTokenSource _gameDataCts;
    public void ApplyGameDataVisibility(bool visible)
    {
        if (_gameDataPanel != null) _gameDataPanel.Visible = visible;
    }
    private void BuildGameDataPanel()
    {
        var channel = GameChannelCatalog.Get(_game.IconName);
        if (channel?.Family is not (GameFamily.Arknights or GameFamily.Endfield)) return;
        _gameDataPanel = new GameDataPanel(channel.Channel is "Official" or "Bilibili")
        { AccentColor = GetCoverAccentPalette().PrimaryHover };
        var cfg = ConfigHelper.Load();
        _gameDataPanel.Visible = cfg.ShowGameData;
        _coverPictureBox.Controls.Add(_gameDataPanel);
        void Position()
        {
            int margin = (int)Math.Round(14 * _gameDataPanel.DeviceDpi / 96F);
            _gameDataPanel.SetAvailableWidth(Math.Max(1, _coverPictureBox.ClientSize.Width - margin * 2));
            PositionGameDataPanel();
            _gameDataPanel.BringToFront();
        }
        _gameDataPanel.LayoutMetricsChanged += (s, e) => Position();
        _coverPictureBox.Resize += (s, e) => Position();
        _gameDataPanel.Expanded += async (s, e) => await RefreshGameDashboardAsync();
        _gameDataPanel.VisibleChanged += async (s, e) => await RefreshGameDashboardAsync();
        _gameDataPanel.AccountSwitchRequested += async (s, direction) => await SwitchGameDashboardAsync(direction);
        var tokenTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        tokenTimer.Tick += async (s, e) =>
        {
            if (IsDisposed || !_gameDataPanel.Visible || !_gameDataPanel.IsExpanded) return;
            try
            {
                if (_gameDataTokenKey != GetDashboardTokenKey()) await RefreshGameDashboardAsync(true);
            }
            catch { /* A later tick can retry a transient configuration read failure. */ }
        };
        tokenTimer.Start();
        Disposed += (s, e) => { tokenTimer.Dispose(); _gameDataVersion++; _gameDataCts?.Cancel(); };
        _gameDataPanel.SetExpanded(cfg.GameDataExpanded);
        _gameDataPanel.ExpansionChanged += (s, e) =>
        {
            try { ConfigHelper.Update(config => config.GameDataExpanded = _gameDataPanel.IsExpanded); }
            catch (Exception ex) { LogHelper.LogError(ex, "Save game dashboard expansion state"); }
        };
        _gameDataPanel.SetSummary(GamePlaytime.GetText(channel.GameId));
        Position();
    }
    private void PositionGameDataPanel()
    {
        if (_gameDataPanel == null || _gameDataPanel.IsDisposed || _coverPictureBox == null) return;
        int margin = (int)Math.Round(14 * _gameDataPanel.DeviceDpi / 96F);
        int offset = (int)Math.Round((_gameDataPanel.Height + margin) * _switchAnimationProgress);
        _gameDataPanel.Location = new Point(
            Math.Max(0, _coverPictureBox.ClientSize.Width - _gameDataPanel.Width - margin), margin - offset);
    }
    private string GetDashboardTokenKey() => SkylandService.DashboardTokenKey(SkylandTokenStorage.GetTokens(ConfigHelper.Load()));
    private bool IsCurrentDashboardRequest(int version, CancellationToken token) =>
        !token.IsCancellationRequested && !IsDisposed && !_gameDataPanel.IsDisposed && version == _gameDataVersion;

    private async Task RefreshGameDashboardAsync(bool force = false)
    {
        if (IsDisposed || Disposing || (_gameDataLoading && !force) || _gameDataPanel == null || _gameDataPanel.IsDisposed ||
            !_gameDataPanel.Visible || !_gameDataPanel.IsExpanded) return;
        _gameDataCts?.Cancel();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_coverCts.Token);
        _gameDataCts = cts;
        int version = ++_gameDataVersion;
        _gameDataLoading = true;
        try
        {
            _gameDataTokenKey = GetDashboardTokenKey();
            _gameDataRoles = new();
            _gameDataPanel.SetAccountPosition(0, 0);
            _gameDataPanel.SetDetails(Localizer.GetRequiredString("App.Game.Dashboard.Loading"));
            var roles = await new SkylandService().GetDashboardRolesAsync(_game.IconName, cts.Token, force);
            if (!IsCurrentDashboardRequest(version, cts.Token)) return;
            if (_gameDataTokenKey != GetDashboardTokenKey()) { await RefreshGameDashboardAsync(true); return; }
            _gameDataRoles = roles.Roles;
            if (_gameDataRoles.Count == 0)
            {
                _gameDataPanel.SetDetails(Localizer.GetRequiredString(roles.EmptyMessageKey));
                return;
            }
            var cfg = ConfigHelper.Load();
            var selected = _gameDataSelectedUid ?? cfg.GameDataSelectedUids?.GetValueOrDefault(GameChannelCatalog.Get(_game.IconName).IconName);
            var role = _gameDataRoles.FirstOrDefault(x => x.Uid == selected) ?? _gameDataRoles[0];
            await ShowDashboardRoleAsync(role, version, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch { if (IsCurrentDashboardRequest(version, cts.Token)) _gameDataPanel.SetStatus(Localizer.GetRequiredString("App.Game.Dashboard.LoadFailed")); }
        finally { if (version == _gameDataVersion) { _gameDataLoading = false; _gameDataCts = null; } }
    }
    private async Task SwitchGameDashboardAsync(int direction)
    {
        if (IsDisposed || Disposing || _gameDataRoles.Count < 2) return;
        try
        {
            if (_gameDataTokenKey != GetDashboardTokenKey()) { await RefreshGameDashboardAsync(true); return; }
            int index = _gameDataRoles.FindIndex(x => x.Uid == _gameDataSelectedUid);
            var role = _gameDataRoles[(index + direction + _gameDataRoles.Count) % _gameDataRoles.Count];
            _gameDataCts?.Cancel();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_coverCts.Token);
            _gameDataCts = cts;
            int version = ++_gameDataVersion;
            try
            {
                ConfigHelper.Update(cfg => (cfg.GameDataSelectedUids ??= new())[role.IconName] = role.Uid);
            }
            catch (Exception ex) { LogHelper.LogError(ex, "Save game dashboard selection"); }
            try { await ShowDashboardRoleAsync(role, version, cts.Token); }
            finally { if (version == _gameDataVersion) { _gameDataLoading = false; _gameDataCts = null; } }
        }
        catch (OperationCanceledException) { }
        catch { /* ShowDashboardRoleAsync displays request failures for the selected role. */ }
    }
    private async Task ShowDashboardRoleAsync(DashboardRole role, int version, CancellationToken token, bool retry = true)
    {
        _gameDataSelectedUid = role.Uid;
        _gameDataPanel.SetAccountPosition(_gameDataRoles.IndexOf(role), _gameDataRoles.Count);
        _gameDataPanel.SetDetails(role.Placeholder, placeholder: true);
        _gameDataPanel.SetStatus(Localizer.GetRequiredString("App.Game.Dashboard.Loading"));
        try
        {
            var text = await new SkylandService().GetDashboardAsync(role, token);
            if (!IsCurrentDashboardRequest(version, token)) return;
            if (_gameDataTokenKey != GetDashboardTokenKey()) { await RefreshGameDashboardAsync(true); return; }
            _gameDataPanel.SetDetails(text);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            if (!IsCurrentDashboardRequest(version, token)) return;
            if (retry)
            {
                try
                {
                    var roles = await new SkylandService().GetDashboardRolesAsync(_game.IconName, token, true);
                    if (!IsCurrentDashboardRequest(version, token)) return;
                    if (_gameDataTokenKey != GetDashboardTokenKey()) { await RefreshGameDashboardAsync(true); return; }
                    if (roles.Roles.Count > 0 && !roles.Roles.Any(x => x.Uid == role.Uid))
                    {
                        _gameDataRoles = roles.Roles;
                        await ShowDashboardRoleAsync(_gameDataRoles[0], version, token, false);
                        return;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch { /* Retain the selected role's placeholder and report failure below. */ }
            }
            if (IsCurrentDashboardRequest(version, token))
                _gameDataPanel.SetStatus(Localizer.GetRequiredString("App.Game.Dashboard.LoadFailed"));
        }
    }
}
