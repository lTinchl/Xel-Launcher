using System;
using System.Drawing;
using XelLauncher.Helpers;

namespace XelLauncher.Forms;

public partial class GamePage
{
    private GameDataPanel _gameDataPanel;
    private bool _gameDataLoading;
    private void BuildGameDataPanel()
    {
        if (!GameChannelCatalog.IsFamily(_game.IconName, GameFamily.Arknights)) return;
        _gameDataPanel = new GameDataPanel { AccentColor = GetCoverAccentPalette().PrimaryHover };
        _coverPictureBox.Controls.Add(_gameDataPanel);
        void Position()
        {
            int margin = (int)Math.Round(14 * _gameDataPanel.DeviceDpi / 96F);
            _gameDataPanel.SetAvailableWidth(Math.Max(1, _coverPictureBox.ClientSize.Width - margin * 2));
            _gameDataPanel.Location = new Point(Math.Max(0, _coverPictureBox.ClientSize.Width - _gameDataPanel.Width - margin), margin);
            _gameDataPanel.BringToFront();
        }
        _gameDataPanel.LayoutMetricsChanged += (s, e) => Position();
        _coverPictureBox.Resize += (s, e) => Position();
        _gameDataPanel.Expanded += async (s, e) =>
        {
            if (_gameDataLoading) return;
            _gameDataLoading = true;
            _gameDataPanel.SetDetails("正在加载…");
            try
            {
                var text = await new SkylandService().GetDashboardAsync(_game.IconName, _coverCts.Token);
                if (!_gameDataPanel.IsDisposed) _gameDataPanel.SetDetails(text);
            }
            catch (OperationCanceledException) { }
            catch { if (!_gameDataPanel.IsDisposed) _gameDataPanel.SetDetails("数据加载失败，请收起后重试"); }
            finally { _gameDataLoading = false; }
        };
        _gameDataPanel.SetSummary(GamePlaytime.GetText("Arknights"));
        Position();
    }
}
