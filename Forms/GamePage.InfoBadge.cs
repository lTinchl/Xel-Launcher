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
            }
            catch { }

            if (string.IsNullOrWhiteSpace(localVersion))
                localVersion = "--";

            bool isEnglish = AntdUI.Localization.CurrentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            string versionText = isEnglish
                ? $"Local version: {localVersion}"
                : $"本地版本：{localVersion}";

            _gameInfoBadge.SetContent(GetChannelLabel(), versionText);
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
            return _game.IconName switch
            {
                "BiliArknights" or "BiliEndfield" => "Bilibili",
                "GlobalEndfield" => "Global",
                "PlayEndfield" => "Google Play",
                _ => "Official",
            };
        }
    }
}
