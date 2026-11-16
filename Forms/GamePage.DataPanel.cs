using System;
using System.Drawing;
using XelLauncher.Helpers;

namespace XelLauncher.Forms;

public partial class GamePage
{
    private GameDataPanel _gameDataPanel;
    private bool _gameDataLoading;
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
    private async System.Threading.Tasks.Task RefreshGameDashboardAsync()
    {
        if (_gameDataLoading || _gameDataPanel == null || _gameDataPanel.IsDisposed ||
            !_gameDataPanel.Visible || !_gameDataPanel.IsExpanded) return;
        _gameDataLoading = true;
        _gameDataPanel.SetDetails(Localizer.GetRequiredString("App.Game.Dashboard.Loading"));
        try
        {
            var text = await new SkylandService().GetDashboardAsync(_game.IconName, _coverCts.Token);
            if (!_gameDataPanel.IsDisposed) _gameDataPanel.SetDetails(text);
        }
        catch (OperationCanceledException) { }
        catch { if (!_gameDataPanel.IsDisposed) _gameDataPanel.SetDetails(Localizer.GetRequiredString("App.Game.Dashboard.LoadFailed")); }
        finally { _gameDataLoading = false; }
    }
}
