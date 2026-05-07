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
        private AntdUI.Button GameStart;
        private AntdUI.Dropdown floatMenu;
        private AntdUI.Panel panelLaunch;

        private AntdUI.FormFloatButton? _floatBtn;
        private bool _floatExpanded = false;
        private AntdUI.Panel _toolSidebar;
        private AntdUI.TooltipComponent _toolTooltip;
        private CoverPictureBox _coverPictureBox;
        private NoticeCarouselPanel _noticePanel;
        private Image _coverImage;
        private string _coverTransitionKey = "";
        private string _coverTransitionSignature = "";
        private readonly CancellationTokenSource _coverCts = new();
        private const int SwitchAnimationDuration = 220;
        private bool _switchAnimationActive = false;
        private float _switchAnimationProgress = 0F;
        private Point _launchPanelHome;
        private Rectangle _noticePanelHome;
        private Point _toolSidebarHome;

        private bool _accountExpanded = false;
        private readonly List<AntdUI.Avatar> _subBtns = new();

        private EndfieldService _service;
        private enum GameState { Unknown, NotInstalled, HasUpdate, Ready, Downloading, Paused }
        private GameState _gameState = GameState.Unknown;
        private CancellationTokenSource _downloadCts;

        public GamePage(GameEntry game, Overview overview)
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
            BuildCoverImage();

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
                if (cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached))
                {
                    _gameState = !cached.IsInstalled ? GameState.NotInstalled
                               : cached.HasUpdate    ? GameState.HasUpdate
                                                     : GameState.Ready;

                    if (IsHandleCreated)
                        RefreshGameStartButton();
                    else
                        HandleCreated += (s, e) => RefreshGameStartButton();
                }
            }
            catch { }
        }


    }


}
