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
            if (_isSwitchingGame)
            {
                _pendingGame = g;
                _pendingGameForceReload |= forceReload;
                UpdateSelectedGameButton(g);
                return;
            }

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
            GamePage newPage = null;
            System.Drawing.Image initialCoverImage = null;
            System.Drawing.Image oldCoverImage = null;
            bool layoutSuspended = false;

            try
            {
                var initialCover = await System.Threading.Tasks.Task.Run(() =>
                {
                    var image = GameCoverCache.TryLoadCachedCover(g.IconName, out var path);
                    return (Image: image, Path: path);
                });
                initialCoverImage = initialCover.Image;

                bool hasDifferentPendingGame = _pendingGame != null &&
                    (_pendingGame.IconName != g.IconName ||
                     _pendingGame.RootPath != g.RootPath ||
                     _pendingGame.Name != g.Name);
                if (hasDifferentPendingGame)
                    return;

                newPage = new GamePage(g, this, initialCoverImage, initialCover.Path)
                {
                    Visible = false
                };
                initialCoverImage = null;
                SetSurfaceColor(panelMain, System.Drawing.Color.Black);
                panelMain.SuspendLayout();
                layoutSuspended = true;
                panelMain.Controls.Add(newPage);
                newPage.SendToBack();
                newPage.Bounds = panelMain.ClientRectangle;
                newPage.PerformLayout();
                newPage.UpdateLaunchPanelColor();
                bool sameCover = oldPage == null || oldPage.IsDisposed || oldPage.HasSameCoverAs(newPage);
                if (!sameCover)
                {
                    oldCoverImage = oldPage.CloneCoverImageForTransition();
                    newPage.PrepareCoverFadeFrom(oldCoverImage);
                    oldCoverImage = null;
                }
                newPage.PrepareSwitchInStart();
                panelMain.ResumeLayout(true);
                layoutSuspended = false;

                if (oldPage != null && !oldPage.IsDisposed)
                    await oldPage.PlaySwitchOutAsync();

                panelMain.SuspendLayout();
                layoutSuspended = true;

                _currentGamePage = newPage;
                newPage.Visible = true;
                newPage.BringToFront();
                newPage.Update();

                if (oldPage != null)
                {
                    panelMain.Controls.Remove(oldPage);
                    oldPage.Dispose();
                }

                panelMain.ResumeLayout(true);
                layoutSuspended = false;
                var activePage = newPage;
                newPage = null;
                activePage.UpdateLaunchPanelColor();
                UpdateSelectedGameButton(g);
                await activePage.PlaySwitchInAsync();
            }
            finally
            {
                if (layoutSuspended)
                    panelMain.ResumeLayout(true);
                newPage?.Dispose();
                initialCoverImage?.Dispose();
                oldCoverImage?.Dispose();
                _isSwitchingGame = false;

                var pendingGame = _pendingGame;
                var pendingForceReload = _pendingGameForceReload;
                _pendingGame = null;
                _pendingGameForceReload = false;
                if (pendingGame != null && !IsDisposed)
                    BeginInvoke(() => SelectGame(pendingGame, pendingForceReload));
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
