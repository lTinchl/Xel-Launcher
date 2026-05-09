using System;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private async void SelectGame(GameEntry g, bool forceReload = false)
        {
            if (g == null) return;
            if (_isSwitchingGame) return;

            bool sameGame = _currentGamePage != null
                && _currentGame != null
                && _currentGame.IconName == g.IconName
                && _currentGame.RootPath == g.RootPath
                && _currentGame.Name == g.Name;
            if (sameGame && !forceReload)
            {
                UpdateSelectedGameButton(g);
                return;
            }

            _isSwitchingGame = true;
            _currentGame = g;
            UpdateSelectedGameButton(g);
            var oldPage = _currentGamePage;
            System.Drawing.Image oldCover = null;

            try
            {
                if (oldPage != null && !oldPage.IsDisposed)
                    await oldPage.PlaySwitchOutAsync();

                var newPage = new GamePage(g, this);
                SetSurfaceColor(panelMain, System.Drawing.Color.Black);
                panelMain.Controls.Add(newPage);
                newPage.SendToBack();
                panelMain.PerformLayout();
                newPage.PerformLayout();
                newPage.UpdateLaunchPanelColor();
                if (oldPage != null && !oldPage.IsDisposed && !oldPage.HasSameCoverAs(newPage))
                {
                    oldCover = oldPage.CloneCoverImageForTransition();
                    newPage.PrepareCoverFadeFrom(oldCover);
                    oldCover = null;
                }
                newPage.PrepareSwitchInStart();

                if (oldPage != null)
                {
                    panelMain.Controls.Remove(oldPage);
                    oldPage.Dispose();
                }

                _currentGamePage = newPage;
                UpdateSelectedGameButton(g);
                _currentGamePage.BringToFront();
                await _currentGamePage.PlaySwitchInAsync();
            }
            finally
            {
                oldCover?.Dispose();
                _isSwitchingGame = false;
            }
        }

        private void UpdateSelectedGameButton(GameEntry g)
        {
            var config = ConfigHelper.Load();
            var accent = _currentGamePage?.GetCoverAccentPalette().Primary ?? AntdUI.Style.Db.Primary;
            for (int i = 0; i < _sidebarBtns.Count && i < config.Games.Count; i++)
            {
                _sidebarBtns[i].AccentColor = accent;
                _sidebarBtns[i].Selected = config.Games[i].IconName == g.IconName;
            }
            _sidebarSelectionColor = accent;
            PositionSidebarSelectionIndicator(true);
        }
    }
}
