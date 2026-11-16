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
            _gameInfoBadge.SizeChanged += (s, e) => PositionGameInfoBadge();
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
                    var installedVersion = SharedRootManager.ReadInstalledVersion(entry.RootPath);
                    if (!string.IsNullOrWhiteSpace(installedVersion))
                        localVersion = installedVersion;
                    else if (string.IsNullOrWhiteSpace(localVersion))
                        localVersion = resolution.State?.BaseVersion ?? "";
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

            int left = (int)Math.Round(14 * _gameInfoBadge.DeviceDpi / 96F);
            int top = left;
            int maxX = Math.Max(0, _coverPictureBox.ClientSize.Width - _gameInfoBadge.Width - left);
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
