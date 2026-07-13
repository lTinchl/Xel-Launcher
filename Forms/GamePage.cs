using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public partial class GamePage : UserControl
    {
        private readonly GameEntry _game;
        private readonly Overview _overview;

        private AntdUI.Button btnArknightsWiki;
        private AntdUI.Button btnAccountManage;
        private AntdUI.Select accountSelect;
        private AntdUI.Button btnPreload;
        private AntdUI.Button GameStart;
        private AntdUI.Dropdown floatMenu;
        private AntdUI.Panel panelLaunch;

        private AntdUI.FormFloatButton? _floatBtn;
        private bool _floatExpanded = false;
        private AntdUI.Panel _toolSidebar;
        private AntdUI.TooltipComponent _leftTooltip;
        private AntdUI.TooltipComponent _topTooltip;
        private AntdUI.TooltipComponent _bottomTooltip;
        private CoverPictureBox _coverPictureBox;
        private NoticeCarouselPanel _noticePanel;
        private Image _coverImage;
        private string _coverTransitionKey = "";
        private string _coverTransitionSignature = "";
        private bool _coverAccentPaletteValid;
        private (Color Primary, Color PrimaryHover, Color PrimaryActive, Color Muted, Color MutedHover, Color MutedActive, Color Danger, Color DangerHover, Color Text) _coverAccentPalette;
        private readonly CancellationTokenSource _coverCts = new();
        private const int SwitchAnimationDurationMs = 360;
        private const int SwitchAnimationMinFrameIntervalMs = 7;
        private bool _switchAnimationActive = false;
        private float _switchAnimationProgress = 0F;
        private Point _launchPanelHome;
        private Rectangle _noticePanelHome;
        private Point _toolSidebarHome;

        private bool _accountExpanded = false;
        private readonly List<AntdUI.Avatar> _subBtns = new();

        private EndfieldService _service;
        private enum GameState { Unknown, NotInstalled, HasUpdate, HasPreload, Ready, Downloading, Paused, Repairing }
        private GameState _gameState = GameState.Unknown;
        private ActiveGameUpdate _activeUpdate;
        private string _repairingPath;
        private bool _preloadRunning;
        private bool _preloadCompleted;

        public GamePage(GameEntry game, Overview overview, Image initialCoverImage = null, string initialCoverPath = null)
        {
            _game = game;
            _overview = overview;
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            BuildLaunchPanel();
            BuildCoverImage(initialCoverImage, initialCoverPath);

            LoadAccountSelect();
            UpdateAccountControlsVisibility();

            // Apply cached game status before network check
            ApplyCachedGameStatus();

            _ = CheckGameStatusAsync();
        }

        private void ApplyCachedGameStatus()
        {
            try
            {
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                var path = entry?.RootPath ?? _game.RootPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var activeUpdate = GameUpdateManager.Find(path);
                    if (activeUpdate != null)
                    {
                        _activeUpdate = activeUpdate;
                        _gameState = activeUpdate.IsCancellationRequested ? GameState.Paused : GameState.Downloading;
                        if (IsHandleCreated)
                            RefreshGameStartButton();
                        else
                            HandleCreated += (s, e) => RefreshGameStartButton();
                        return;
                    }

                    if (GameUpdateManager.IsPaused(path))
                    {
                        _gameState = GameState.Paused;
                        if (IsHandleCreated)
                            RefreshGameStartButton();
                        else
                            HandleCreated += (s, e) => RefreshGameStartButton();
                        return;
                    }
                }

                if (cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached) &&
                    IsSameInstallPath(cached.InstallPath, path))
                {
                    var hasUpdate = cfg.CheckGameUpdates && cached.HasUpdate;
                    var hasPreload = cfg.CheckGameUpdates && !hasUpdate && cached.HasPreload;
                    _preloadCompleted = hasPreload &&
                                        IsPreloadCompletedForPath(cfg.GameStatusCache.Values, path,
                                            cached.PreloadVersion);
                    _gameState = !cached.IsInstalled ? GameState.NotInstalled
                               : hasUpdate           ? GameState.HasUpdate
                               : hasPreload          ? GameState.HasPreload
                                                     : GameState.Ready;

                    if (IsHandleCreated)
                        RefreshGameStartButton();
                    else
                        HandleCreated += (s, e) => RefreshGameStartButton();
                }
            }
            catch { }
        }

        private static bool IsSameInstallPath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            try
            {
                var left = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var right = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPreloadCompletedForPath(
            IEnumerable<CachedGameStatus> cachedStatuses,
            string installPath,
            string preloadVersion)
        {
            if (string.IsNullOrWhiteSpace(preloadVersion)) return false;

            return cachedStatuses.Any(cached =>
                cached.PreloadCompleted &&
                string.Equals(cached.PreloadVersion ?? "", preloadVersion, StringComparison.OrdinalIgnoreCase) &&
                IsSameInstallPath(cached.InstallPath, installPath));
        }


    }


}
