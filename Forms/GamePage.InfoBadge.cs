using System;
using System.Drawing;
using System.Linq;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class GamePage
    {
        private void BuildGameInfoBadge()
        {
            if (_coverPictureBox == null || _coverPictureBox.IsDisposed) return;

            _gameInfoBadge = new GameInfoBadgeControl();
            _coverPictureBox.Controls.Add(_gameInfoBadge);
            RefreshGameInfoBadge();
            PositionGameInfoBadge();
        }

        private void RefreshGameInfoBadge()
        {
            if (_gameInfoBadge == null || _gameInfoBadge.IsDisposed) return;

            string localVersion = "";
            string targetChannel = GetChannelLabel();
            string installedChannel = targetChannel;
            try
            {
                var config = ConfigHelper.Load();
                var entry = config.Games.FirstOrDefault(game => game.IconName == _game.IconName);
                localVersion = entry?.LocalVersion ?? "";
                if (string.IsNullOrWhiteSpace(localVersion) &&
                    config.GameStatusCache.TryGetValue(_game.IconName, out var cached))
                {
                    localVersion = cached.LocalVersion ?? "";
                }

                if (!string.IsNullOrWhiteSpace(entry?.RootPath))
                {
                    var resolution = SharedRootManager.Resolve(
                        config,
                        _game.IconName,
                        entry.RootPath,
                        detectBaseChannel: false,
                        out _);
                    installedChannel = resolution.Base?.ChannelLabel ?? targetChannel;
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(localVersion))
                localVersion = "--";

            string targetText = string.Format(
                Localizer.GetRequiredString("App.Game.InfoBadge.Target"),
                targetChannel);
            string versionText = string.Format(
                Localizer.GetRequiredString("App.Game.InfoBadge.Installed"),
                installedChannel,
                localVersion);

            _gameInfoBadge.SetContent(targetText, versionText);
            _gameInfoBadge.AccentColor = GetCoverAccentPalette().PrimaryHover;
            PositionGameInfoBadge();
        }

        private void PositionGameInfoBadge()
        {
            if (_gameInfoBadge == null || _gameInfoBadge.IsDisposed || _coverPictureBox == null) return;

            const int left = 14;
            const int top = 14;
            int maxX = Math.Max(8, _coverPictureBox.ClientSize.Width - _gameInfoBadge.Width - 12);
            _gameInfoBadge.Location = new Point(Math.Min(left, maxX), top);
            _gameInfoBadge.BringToFront();
        }

        private string GetChannelLabel()
        {
            return GameChannelCatalog.Get(_game.IconName)?.ChannelLabel
                   ?? "Official";
        }
    }
}
