using System;
using System.Collections.Generic;
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
    public class GamePage : UserControl
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
        private readonly CancellationTokenSource _coverCts = new();

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

        private void BuildLaunchPanel()
        {
            btnArknightsWiki = new AntdUI.Button
            {
                IconSvg = "PlusOutlined",
                Type = AntdUI.TTypeMini.Default,
                BackColor = AntdUI.Config.IsDark ? AppTheme.DarkSurfaceActive : Color.White,
                Size = new Size(52, 52),
                Location = new Point(0, 0),
                BorderWidth = 0,
                Radius = 26,
                WaveSize = 4,
            };

            var tooltip = new AntdUI.TooltipComponent();
            tooltip.SetTip(btnArknightsWiki, AntdUI.Localization.Get("App.Game.Toolbox", "小工具"));
            btnArknightsWiki.Click += btnArknightsWiki_Click;

            btnAccountManage = new AntdUI.Button
            {
                IconSvg = "UserOutlined",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(52, 52),
                Location = new Point(0, 0),
                BorderWidth = 0,
                Radius = 24,
                WaveSize = 4,
            };
            btnAccountManage.Click += btnAccountManage_Click;

            accountSelect = new AntdUI.Select
            {
                Location = new Point(56, 0),
                Size = new Size(164, 52),
                Radius = 24,
                BorderWidth = 1F,
                PlaceholderText = AntdUI.Localization.Get("App.Game.SelectAccount", "  选择账号"),
                Font = new Font("Microsoft YaHei UI", 11F),
                DropDownRadius = 8,
                Placement = AntdUI.TAlignFrom.TL,
            };

            GameStart = new AntdUI.Button
            {
                BackExtend = "135, #6253E1, #04BEFE",
                IconSvg = "PoweroffOutlined",
                Text = AntdUI.Localization.Get("App.Game.Start", "开始游戏"),
                Location = new Point(224, 0),
                Size = new Size(168, 52),
                BorderWidth = 0,
                Radius = 24,
                WaveSize = 4,
                LoadingWaveColor = Color.FromArgb(60, 255, 255, 255),
                Type = AntdUI.TTypeMini.Primary,
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            };
            GameStart.Click += GameStart_Click;

            floatMenu = new AntdUI.Dropdown
            {
                BackExtend = "135, #04BEFE, #3a7bd5",
                IconSvg = "MenuOutlined",
                Location = new Point(392, 0),
                Size = new Size(56, 52),
                BorderWidth = 0,
                Radius = 24,
                Type = AntdUI.TTypeMini.Primary,
                Placement = AntdUI.TAlignFrom.TR,
                DropDownArrow = false,
                DropDownRadius = 8,
            };
            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Setting", "游戏设置"), "setting").SetIcon("SettingOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "setting")
                {
                    BeginInvoke(() =>
                    {
                        floatMenu.SelectedValue = null;
                        AntdUI.Drawer.open(_overview, new GameSettingForm(_game, _overview, UpdateAccountControlsVisibility, () => _ = CheckGameStatusAsync()), AntdUI.TAlignMini.Right);
                    });
                }
            };
            floatMenu.Items.Add(new AntdUI.SelectItem(AntdUI.Localization.Get("App.Game.Deledwonload", "清理下载缓存"), "Deledwonload").SetIcon("DeleteOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "Deledwonload")
                {
                    BeginInvoke(() =>
                    {
                        floatMenu.SelectedValue = null;
                        var cfg = ConfigHelper.Load();
                        var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                        string path = entry?.RootPath ?? _game.RootPath;
                        string cachePath = Path.Combine(path, "Diffs");
                        if (Directory.Exists(cachePath))
                        {
                            try
                            {
                                Directory.Delete(cachePath, true);
                                AntdUI.Message.success(_overview, AntdUI.Localization.Get("App.Game.ClearCacheSuccess", "下载缓存已清理"));
                            }
                            catch (Exception ex)
                            {
                                AntdUI.Message.error(_overview, string.Format(AntdUI.Localization.Get("App.Game.ClearCacheFailed", "清理下载缓存失败: {0}"), ex.Message));
                            }
                        }
                        else
                        {
                            AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.NoCache", "未找到下载缓存"));
                        }
                    });
                }
            };

            panelLaunch = new AntdUI.Panel
            {
                Radius = 26,
                Shadow = 0,
                ShadowOpacity = 0F,
                BackColor = Color.Transparent,
                Size = new Size(448, 52),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            panelLaunch.Controls.Add(btnAccountManage);
            panelLaunch.Controls.Add(accountSelect);
            panelLaunch.Controls.Add(GameStart);
            panelLaunch.Controls.Add(floatMenu);

        }

        private void BuildCoverImage()
        {
            string imgFile = _game.IconName switch
            {
                "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield" => "End.jpg",
                _ => "Arknights.jpg",
            };
            string fallbackPath = Path.Combine(AppContext.BaseDirectory, "Resources", imgFile);

            var img = LoadCoverImage(fallbackPath);
            if (img == null) return;
            _coverImage = img;
            var pb = new CoverPictureBox
            {
                Dock = DockStyle.Fill,
                Image = img,
            };
            _coverPictureBox = pb;
            switch (_game.IconName)
            {
                case "Arknights":
                case "BiliArknights":
                    _subBtns.Add(CreateSubButton(Properties.Resources.Arknights_Toolbox, btnArkntools_Click, "Arkntools"));
                    _subBtns.Add(CreateSubButton(Properties.Resources.PRTS_WIKI, btnPrtsWiki_Click, "PRTS Wiki"));
                    _subBtns.Add(CreateSubButton(Properties.Resources.Arknights_Yituliu, btnYituliu_Click, "一图流"));
                    break;
                case "Endfield":
                case "BiliEndfield":
                    _subBtns.Add(CreateSubButton(Properties.Resources.End_Yituliu, btnEndYituliu_Click, "终末地一图流"));
                    _subBtns.Add(CreateSubButton(Properties.Resources.warfarin, btnWarfarin_Click, "Warfarin Wiki"));
                    break;
                case "GlobalEndfield":
                case "PlayEndfield":
                    _subBtns.Add(CreateSubButton(Properties.Resources.endfieldtools, btnEndfieldtools_Click, "Endfield Tools"));
                    break;
                default:
                    break;
            }

            Controls.Add(pb);
            _coverPictureBox.Controls.Add(panelLaunch);
            BuildToolSidebar();
            _noticePanel = new NoticeCarouselPanel(CreateFallbackBanners(img), CreateFallbackNotices())
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            };
            _noticePanel.NoticeClick += NoticePanel_NoticeClick;
            ApplyCachedLauncherNotice();
            _coverPictureBox.Controls.Add(_noticePanel);
            HandleCreated += (s, e) => {
                PositionLaunchPanel();
                PositionToolSidebar();
                PositionNoticePanel();
                UpdateLaunchPanelColor();
                };
            SizeChanged += (s, e) =>
            {
                PositionLaunchPanel();
                PositionNoticePanel();
                PositionToolSidebar();
            };
            PositionLaunchPanel();
            PositionNoticePanel();
            PositionToolSidebar();
        }

        private Image LoadCoverImage(string fallbackPath)
        {
            var cachedPath = GameCoverCache.GetCachedCoverPath(_game.IconName);
            if (!string.IsNullOrEmpty(cachedPath))
            {
                var cached = GameCoverCache.TryLoadImage(cachedPath);
                if (cached != null) return cached;
            }

            return File.Exists(fallbackPath) ? GameCoverCache.TryLoadImage(fallbackPath) : null;
        }

        private async Task RefreshRemoteCoverAsync()
        {
            try
            {
                var cachedPath = GameCoverCache.GetCachedCoverPath(_game.IconName);
                if (!string.IsNullOrEmpty(cachedPath))
                {
                    LogHelper.Log($"Client cover cache hit, skip refresh: {_game.IconName} -> {cachedPath}");
                    return;
                }

                var service = _service;
                if (service == null) return;

                LogHelper.Log($"Refreshing client cover: {_game.IconName}");
                var token = _coverCts.Token;
                var imageUrl = await service.GetClientCoverImageUrlAsync(token);
                if (string.IsNullOrEmpty(imageUrl))
                {
                    LogHelper.Log($"Client cover URL empty: {_game.IconName}");
                    return;
                }

                LogHelper.Log($"Client cover URL: {_game.IconName} -> {imageUrl}");

                var imagePath = await GameCoverCache.UpdateAsync(_game.IconName, imageUrl, token);
                if (string.IsNullOrEmpty(imagePath) || _coverCts.IsCancellationRequested)
                {
                    LogHelper.Log($"Client cover cache skipped: {_game.IconName}");
                    return;
                }
                if (!IsHandleCreated || IsDisposed) return;

                BeginInvoke(() => ApplyCoverImage(imagePath));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.RefreshRemoteCoverAsync({_game.IconName})");
            }
        }

        private void ApplyCoverImage(string imagePath)
        {
            if (_coverPictureBox == null || _coverPictureBox.IsDisposed) return;

            var image = GameCoverCache.TryLoadImage(imagePath);
            if (image == null)
            {
                LogHelper.Log($"Client cover load failed: {_game.IconName} -> {imagePath}");
                return;
            }

            var oldImage = _coverImage;
            _coverImage = image;
            _coverPictureBox.Image = image;
            _noticePanel?.UpdateFallbackImage(image);
            _coverPictureBox.Invalidate();
            oldImage?.Dispose();
            LogHelper.Log($"Client cover applied: {_game.IconName} -> {imagePath}");
        }

        private void PositionLaunchPanel()
        {
            if (panelLaunch == null || _coverPictureBox == null) return;

            int x = _coverPictureBox.Width - panelLaunch.Width - 16;
            int y = _coverPictureBox.Height - panelLaunch.Height - 12;
            panelLaunch.Location = new Point(Math.Max(0, x), Math.Max(0, y));
            panelLaunch.BringToFront();
        }

        private void PositionNoticePanel()
        {
            if (_noticePanel == null || panelLaunch == null) return;

            int maxWidth = Width - panelLaunch.Width - 96;
            int noticeWidth = Math.Min(660, Math.Max(420, maxWidth));
            if (Width < 760) noticeWidth = Math.Max(320, Width - 56);

            int noticeHeight = Width < 760 ? 132 : 150;
            int y = panelLaunch.Bottom - noticeHeight;
            if (y < 24) y = 24;

            _noticePanel.Bounds = new Rectangle(28, y, noticeWidth, noticeHeight);
            _noticePanel.BringToFront();
        }

        private List<NoticeBannerItem> CreateFallbackBanners(Image image)
        {
            return new List<NoticeBannerItem>
            {
                new NoticeBannerItem(image, "", false)
            };
        }

        private List<NoticeItem> CreateFallbackNotices()
        {
            bool endfield = _game.IconName is "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield";
            if (endfield)
            {
                return new List<NoticeItem>
                {
                    new NoticeItem("公告", "《明日方舟：终末地》最新资讯", "05/01", "https://endfield.hypergryph.com/"),
                    new NoticeItem("活动", "技术测试与招募信息请以官方公告为准", "04/25", "https://endfield.hypergryph.com/"),
                    new NoticeItem("资讯", "多平台版本与启动配置持续适配中", "04/20", "https://endfield.hypergryph.com/")
                };
            }

            return new List<NoticeItem>
            {
                new NoticeItem("公告", "《明日方舟》七周年庆典即将开启", "04/25", "https://ak.hypergryph.com/news"),
                new NoticeItem("活动", "限时寻访·庆典【承诺】限时寻访即将开启", "04/25", "https://ak.hypergryph.com/news"),
                new NoticeItem("通知", "公开招募标签强制刷新通知", "04/25", "https://ak.hypergryph.com/news")
            };
        }

        private async Task RefreshLauncherNoticeAsync()
        {
            var service = _service;
            if (service == null || _noticePanel == null) return;

            try
            {
                var token = _coverCts.Token;
                var content = await service.GetLauncherNoticeContentAsync(token).ConfigureAwait(false);
                if ((content.Banners?.Count ?? 0) == 0 && (content.Notices?.Count ?? 0) == 0)
                    return;

                await GameCoverCache.SaveLauncherNoticeContentAsync(_game.IconName, content, token).ConfigureAwait(false);

                var remoteBanners = content.Banners ?? Array.Empty<LauncherBannerItem>();
                var banners = new List<NoticeBannerItem>();
                foreach (var banner in remoteBanners.Take(6))
                {
                    var imagePath = GameCoverCache.GetCachedNoticeBannerPath(_game.IconName, banner.ImageUrl);
                    if (string.IsNullOrEmpty(imagePath))
                        imagePath = await GameCoverCache.UpdateNoticeBannerAsync(_game.IconName, banner.ImageUrl, token).ConfigureAwait(false);

                    var image = GameCoverCache.TryLoadImage(imagePath);
                    if (image != null)
                        banners.Add(new NoticeBannerItem(image, banner.JumpUrl ?? "", true));
                }

                GameCoverCache.CleanupNoticeBanners(_game.IconName, remoteBanners.Take(6).Select(x => x.ImageUrl));
                var notices = CreateNoticeItems(content.Notices);

                if (!IsHandleCreated || IsDisposed || token.IsCancellationRequested) return;
                BeginInvoke(() =>
                {
                    if (_noticePanel == null || _noticePanel.IsDisposed) return;
                    _noticePanel.SetContent(banners, notices);
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.RefreshLauncherNoticeAsync({_game.IconName})");
            }
        }

        private void ApplyCachedLauncherNotice()
        {
            if (_noticePanel == null) return;

            try
            {
                var content = GameCoverCache.GetCachedLauncherNoticeContent(_game.IconName);
                if (content == null) return;

                var banners = new List<NoticeBannerItem>();
                foreach (var banner in (content.Banners ?? Array.Empty<LauncherBannerItem>()).Take(6))
                {
                    var imagePath = GameCoverCache.GetCachedNoticeBannerPath(_game.IconName, banner.ImageUrl);
                    var image = GameCoverCache.TryLoadImage(imagePath);
                    if (image != null)
                        banners.Add(new NoticeBannerItem(image, banner.JumpUrl ?? "", true));
                }

                var notices = CreateNoticeItems(content.Notices);
                _noticePanel.SetContent(banners, notices);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.ApplyCachedLauncherNotice({_game.IconName})");
            }
        }

        private static List<NoticeItem> CreateNoticeItems(IEnumerable<LauncherNoticeItem> notices)
        {
            return (notices ?? Array.Empty<LauncherNoticeItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Take(30)
                .Select(x => new NoticeItem(x.Category, x.Title, x.Date, x.JumpUrl))
                .ToList();
        }

        private void NoticePanel_NoticeClick(object sender, NoticeItem e)
        {
            if (!string.IsNullOrWhiteSpace(e?.Url))
                TabHeaderForm.Open(e.Url);
        }

        private bool IsAccountGame => _game?.IconName is "Arknights" or "Endfield" or "GlobalEndfield";

        private void UpdateAccountControlsVisibility()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            bool hideAccounts = !(entry?.AccountSwitchEnabled ?? false) || !IsAccountGame;
            btnAccountManage.Visible = !hideAccounts;
            accountSelect.Visible = !hideAccounts;

            int targetWidth = hideAccounts ? 224 : 448;
            int targetGS = hideAccounts ? 0 : 224;
            int targetFM = hideAccounts ? 168 : 392;
            panelLaunch.Width = targetWidth;
            GameStart.Location = new Point(targetGS, 0);
            floatMenu.Location = new Point(targetFM, 0);
            PositionLaunchPanel();
            PositionNoticePanel();
        }

        public void LoadAccountSelect()
        {
            var cfg = ConfigHelper.Load();
            accountSelect.Items.Clear();

            Dictionary<string, string> accounts;
            List<string> order;
            string defaultId;
            HashSet<string> disabled;

            if (_game?.IconName == "Endfield")
            {
                accounts = cfg.EndfieldAccounts;
                order = cfg.EndfieldAccountOrder;
                defaultId = cfg.EndfieldDefaultAccount;
                disabled = cfg.EndfieldDisabledAccounts;
            }
            else if (_game?.IconName == "GlobalEndfield")
            {
                accounts = cfg.GlobalEndfieldAccounts;
                order = cfg.GlobalEndfieldAccountOrder;
                defaultId = cfg.GlobalEndfieldDefaultAccount;
                disabled = cfg.GlobalEndfieldDisabledAccounts;
            }
            else
            {
                accounts = cfg.Accounts;
                order = cfg.AccountOrder;
                defaultId = cfg.DefaultAccount;
                disabled = cfg.DisabledAccounts;
            }

            var ordered = order.Where(id => accounts.ContainsKey(id)).ToList();
            foreach (var id in accounts.Keys)
                if (!ordered.Contains(id)) ordered.Add(id);
            foreach (var id in ordered)
                if (!disabled.Contains(id))
                    accountSelect.Items.Add(new AntdUI.SelectItem("  " + accounts[id], id));
            if (!string.IsNullOrEmpty(defaultId) && !disabled.Contains(defaultId))
                accountSelect.SelectedValue = defaultId;
            else if (accountSelect.Items.Count > 0)
                accountSelect.SelectedValue = ((AntdUI.SelectItem)accountSelect.Items[0]).Tag;
            else
                accountSelect.SelectedValue = null;
        }

        public void UpdateLaunchPanelColor()
        {
            panelLaunch.BackExtend = "";
            panelLaunch.Back = Color.Transparent;
            panelLaunch.BackColor = Color.Transparent;

            if (AntdUI.Config.IsDark)
            {
                if (_toolSidebar != null) _toolSidebar.Back = Color.FromArgb(188, 34, 37, 43);
                return;
            }
            if (_toolSidebar != null) _toolSidebar.Back = Color.FromArgb(188, 34, 37, 43);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _coverCts.Cancel(); } catch { }
                _coverCts.Dispose();
                _downloadCts?.Dispose();
                _toolTooltip?.Dispose();
            }
            base.Dispose(disposing);
        }

        //璐﹀彿绠＄悊鎸夐挳璋冪敤閫昏緫
        private void btnAccountManage_Click(object sender, EventArgs e)
        {
            var form = new AccountManagerForm(_overview, this, _game.IconName);
            AntdUI.Modal.open(new AntdUI.Modal.Config(_overview, AntdUI.Localization.Get("App.Game.AccountManage", "账号管理"), form)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
                MaskClosable = true,
            });
        }

        private void btnArkntools_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://arkntools.app");
        }

        private void btnPrtsWiki_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://prts.wiki");
        }

        private void btnYituliu_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://ark.yituliu.cn");
        }

        private void btnEndYituliu_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://ef.yituliu.cn/");
        }

        private void btnWarfarin_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://warfarin.wiki");
        }

        private void btnEndfieldtools_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://endfieldtools.dev/");
        }

        private AntdUI.Avatar CreateSubButton(System.Drawing.Icon icon, EventHandler clickHandler, string tip)
        {
            var bmp = new Bitmap(42, 42);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon.ToBitmap(), 5, 5, 32, 32);
            }
            var btn = new AntdUI.Avatar
            {
                Image = bmp,
                ImageFit = AntdUI.TFit.Cover,
                BackColor = Color.Transparent,
                BorderWidth = 0,
                Size = new Size(42, 42),
                Radius = 21,
                Round = true,
                Cursor = Cursors.Hand,
                Visible = true,
            };
            btn.Click += clickHandler;
            EnsureToolTooltip().SetTip(btn, tip);
            return btn;
        }

        private AntdUI.TooltipComponent EnsureToolTooltip()
        {
            return _toolTooltip ??= new AntdUI.TooltipComponent
            {
                ArrowAlign = AntdUI.TAlign.Left,
                Delay = 300,
                Radius = 8,
            };
        }

        private void BuildToolSidebar()
        {
            if (_subBtns.Count == 0 || _coverPictureBox == null) return;

            int item = 42;
            int pad = 8;
            _toolSidebar = new AntdUI.Panel
            {
                Size = new Size(56, pad * 2 + _subBtns.Count * item),
                Radius = 24,
                Shadow = 0,
                ShadowOpacity = 0F,
                BackColor = Color.Transparent,
                Back = Color.FromArgb(188, 34, 37, 43),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };

            for (int i = 0; i < _subBtns.Count; i++)
            {
                var btn = _subBtns[i];
                btn.Visible = true;
                btn.Size = new Size(42, 42);
                btn.Radius = 21;
                btn.Round = true;
                btn.BackColor = Color.Transparent;
                btn.Location = new Point(7, pad + i * item);
                _toolSidebar.Controls.Add(btn);
            }

            _coverPictureBox.Controls.Add(_toolSidebar);
            _toolSidebar.BringToFront();
        }

        private void PositionToolSidebar()
        {
            if (_toolSidebar == null || _coverPictureBox == null) return;

            int availableHeight = Math.Max(1, _coverPictureBox.Height);
            int x = _coverPictureBox.Width - _toolSidebar.Width - 22;
            int y = Math.Max(76, (availableHeight - _toolSidebar.Height) / 2);
            _toolSidebar.Location = new Point(Math.Max(0, x), Math.Max(0, y));
            _toolSidebar.BringToFront();
        }

        private void PositionSubButtons()
        {
            if (_subBtns.Count == 0) return;
            int x = btnArknightsWiki.Right + 4;
            int btnH = _subBtns[0].Height;
            int cy = btnArknightsWiki.Top + (btnArknightsWiki.Height - btnH) / 2;
            for (int i = 0; i < _subBtns.Count; i++)
                _subBtns[i].Location = new Point(x + i * (btnH + 4), cy);
        }
        //+鍙锋寜閽睍寮€/鏀惰捣瀛愭寜閽?
        private void btnArknightsWiki_Click(object sender, EventArgs e)
        {
            _accountExpanded = !_accountExpanded;

            if (_accountExpanded)
            {
                btnArknightsWiki.Type = AntdUI.TTypeMini.Primary;
                btnArknightsWiki.BackColor = null;
                PositionSubButtons();
                foreach (var btn in _subBtns) btn.Visible = true;
            }
            else
            {
                btnArknightsWiki.Type = AntdUI.TTypeMini.Default;
                btnArknightsWiki.BackColor = AntdUI.Config.IsDark ? AppTheme.DarkSurfaceActive : Color.White;
                foreach (var btn in _subBtns) btn.Visible = false;
            }
        }

        //wiki鎮诞鎸夐挳瀹氫綅
        private void PositionAddBtnInBar(System.Windows.Forms.Panel bar)
        {
            btnArknightsWiki.Location = new Point(16, (bar.Height - btnArknightsWiki.Height) / 2);
        }

        private async Task<bool> CheckGameStatusAsync()
        {
            try { _service = new EndfieldService(_game.IconName); }
            catch { return false; }

            _ = RefreshRemoteCoverAsync();
            _ = RefreshLauncherNoticeAsync();

            try
            {
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                string path = entry?.RootPath ?? _game.RootPath;
                if (string.IsNullOrEmpty(path)) return false;

                var status = await _service.CheckStatusAsync(path);
                if (status == null) return false;

                var hasUpdate = status.HasUpdate &&
                                !string.Equals(status.LocalVersion, status.RemoteVersion,
                                    StringComparison.OrdinalIgnoreCase);

                _gameState = !status.IsInstalled ? GameState.NotInstalled
                           : hasUpdate           ? GameState.HasUpdate
                                                 : GameState.Ready;

                // Write cache after live check completes
                var cfgToUpdate = ConfigHelper.Load();
                cfgToUpdate.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = status.IsInstalled,
                    HasUpdate = hasUpdate,
                    LocalVersion = status.LocalVersion ?? "",
                    RemoteVersion = status.RemoteVersion ?? ""
                };
                var entryToUpdate = cfgToUpdate.Games.Find(g => g.IconName == _game.IconName);
                if (entryToUpdate != null && !string.IsNullOrEmpty(status.LocalVersion))
                    entryToUpdate.LocalVersion = status.LocalVersion;
                ConfigHelper.Save(cfgToUpdate);

                if (IsHandleCreated)
                    BeginInvoke(() => RefreshGameStartButton());

                return true;
            }
            catch { return false; }
        }

        private void RefreshGameStartButton()
        {
            switch (_gameState)
            {
                case GameState.NotInstalled:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Install", "安装游戏");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                case GameState.HasUpdate:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Update", "更新游戏");
                    GameStart.IconSvg = "SyncOutlined";
                    break;
                case GameState.Downloading:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Pause", "暂停");
                    GameStart.IconSvg = "PauseOutlined";
                    break;
                case GameState.Paused:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Resume", "继续");
                    GameStart.IconSvg = "DownloadOutlined";
                    break;
                default:
                    GameStart.Text = AntdUI.Localization.Get("App.Game.Start", "开始游戏");
                    GameStart.IconSvg = "PoweroffOutlined";
                    break;
            }
        }

        private void InstallOrUpdateGame()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            if (string.IsNullOrEmpty(path))
            {
                path = Helpers.DialogHelper.BrowseFolder(
                    _overview?.IsHandleCreated == true ? _overview.Handle : IntPtr.Zero,
                    AntdUI.Localization.Get("App.Game.SelectInstallDir", "选择游戏安装目录"));
                if (path == null) return;
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null) { e2.RootPath = path; ConfigHelper.Save(cfg2); }
            }

            _downloadCts?.Dispose();
            _downloadCts = new CancellationTokenSource();
            _gameState = GameState.Downloading;
            RefreshGameStartButton();

            var capturedPath = path;
            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Install.Init", "初始化..."), async config =>
            {
                long lastTick = 0;
                try
                {
                    await _service.InstallOrUpdateAsync(capturedPath, (state, downloaded, total) =>
                    {
                        string label;
                        if (state.HasFlag(InstallProgressState.Completed))
                            label = AntdUI.Localization.Get("App.Game.Install.Completed", "完成");
                        else if (state.HasFlag(InstallProgressState.Download))
                            label = FormatDownloadProgress(downloaded, total);
                        else if (state.HasFlag(InstallProgressState.Install))
                            label = AntdUI.Localization.Get("App.Game.Install.Installing", "安装中...");
                        else if (state.HasFlag(InstallProgressState.Updating))
                            label = AntdUI.Localization.Get("App.Game.Install.Updating", "更新中...");
                        else if (state.HasFlag(InstallProgressState.Verify))
                            label = AntdUI.Localization.Get("App.Game.Install.Verifying", "校验中...");
                        else if (state.HasFlag(InstallProgressState.Removing))
                            label = AntdUI.Localization.Get("App.Game.Install.Removing", "清理中...");
                        else
                            return;

                        if ((state.HasFlag(InstallProgressState.Install) ||
                             state.HasFlag(InstallProgressState.Updating) ||
                             state.HasFlag(InstallProgressState.Verify)) &&
                            total > 0)
                        {
                            label = FormatStageProgress(label, downloaded, total);
                        }

                        long now = Environment.TickCount64;
                        if (now - lastTick < 800) return;
                        lastTick = now;
                        config.Text = label;
                        config.Refresh();
                    }, _downloadCts.Token);

                    MarkGameReadyAfterInstall(capturedPath);
                    await CheckGameStatusAsync();
                    config.OK(AntdUI.Localization.Get("App.Game.Install.Success", "安装/更新完成"));
                }
                catch (Exception ex) when (IsCancellation(ex))
                {
                    _gameState = GameState.Paused;
                    config.OK(AntdUI.Localization.Get("App.Game.Install.Paused", "已暂停"));
                }
                catch (Exception ex)
                {
                    _gameState = GameState.Unknown;
                    _ = CheckGameStatusAsync();
                    config.Error(ex.Message);
                }
                finally
                {
                    BeginInvoke(() =>
                    {
                        if (!GameStart.IsDisposed) RefreshGameStartButton();
                    });
                }
            });
        }

        private static bool IsCancellation(Exception ex) =>
            ex is OperationCanceledException ||
            (ex is AggregateException aex && aex.InnerExceptions.Count > 0 &&
             aex.InnerExceptions[0] is OperationCanceledException);

        private void MarkGameReadyAfterInstall(string installPath)
        {
            _gameState = GameState.Ready;

            try
            {
                var cfg = ConfigHelper.Load();
                cfg.GameStatusCache.TryGetValue(_game.IconName, out var cached);

                cfg.GameStatusCache[_game.IconName] = new CachedGameStatus
                {
                    IsInstalled = true,
                    HasUpdate = false,
                    LocalVersion = cached?.RemoteVersion ?? cached?.LocalVersion ?? "",
                    RemoteVersion = cached?.RemoteVersion ?? ""
                };

                var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
                if (entry != null)
                {
                    if (!string.IsNullOrEmpty(installPath))
                        entry.RootPath = installPath;
                    if (!string.IsNullOrEmpty(cached?.RemoteVersion))
                        entry.LocalVersion = cached.RemoteVersion;
                }

                ConfigHelper.Save(cfg);
            }
            catch { }

            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() =>
                {
                    if (!GameStart.IsDisposed) RefreshGameStartButton();
                });
        }

        private static string FormatDownloadProgress(long downloaded, long total)
        {
            if (total <= 0) return null;
            double pct   = (double)downloaded / total * 100;
            double dlMB  = downloaded / 1048576.0;
            double totMB = total / 1048576.0;
            return $"{dlMB:F1} / {totMB:F1} MB  ({pct:F0}%)";
        }

        private static string FormatStageProgress(string stage, long current, long total)
        {
            var progress = FormatDownloadProgress(current, total);
            return string.IsNullOrWhiteSpace(progress) ? stage : $"{stage} {progress}";
        }

        private async void GameStart_Click(object sender, EventArgs e)
        {
            if (GameStart.Loading) return;

            if (_gameState == GameState.NotInstalled || _gameState == GameState.HasUpdate || _gameState == GameState.Paused)
            {
                InstallOrUpdateGame();
                return;
            }

            if (_gameState == GameState.Downloading)
            {
                _downloadCts?.Cancel();
                return;
            }

            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            string path = entry?.RootPath ?? _game.RootPath;

            var official = cfg.Games.Find(g => g.IconName == "Arknights");
            var bilibili = cfg.Games.Find(g => g.IconName == "BiliArknights");
            bool sameRoot = official != null && bilibili != null &&
                !string.IsNullOrEmpty(official.RootPath) && !string.IsNullOrEmpty(bilibili.RootPath) &&
                Path.GetFullPath(official.RootPath).Equals(
                    Path.GetFullPath(bilibili.RootPath),
                    StringComparison.OrdinalIgnoreCase);

            // Endfield 涓夋湇锛氬彧瑕佸綋鍓嶆父鎴忎笌浠绘剰鍙︿竴涓?Endfield 鏈嶈矾寰勭浉鍚屽氨鎵ц鏇挎崲
            bool isEndfield = _game.IconName == "Endfield" || _game.IconName == "BiliEndfield" || _game.IconName == "GlobalEndfield" || _game.IconName == "PlayEndfield";
            bool endfieldSameRoot = false;
            if (isEndfield && !string.IsNullOrEmpty(path))
            {
                var endfieldIcons = new[] { "Endfield", "BiliEndfield", "GlobalEndfield","PlayEndfield" };
                foreach (var other in endfieldIcons)
                {
                    if (other == _game.IconName) continue;
                    var otherEntry = cfg.Games.Find(g => g.IconName == other);
                    if (otherEntry != null && !string.IsNullOrEmpty(otherEntry.RootPath) &&
                        Path.GetFullPath(path).Equals(Path.GetFullPath(otherEntry.RootPath), StringComparison.OrdinalIgnoreCase))
                    {
                        endfieldSameRoot = true;
                        break;
                    }
                }
            }

            bool needSwitch = isEndfield ? endfieldSameRoot : sameRoot;

            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.Game.WarnSelectDir", "请先选择游戏根目录"));
                path = Helpers.DialogHelper.BrowseFolder(
                    _overview?.IsHandleCreated == true ? _overview.Handle : IntPtr.Zero,
                    AntdUI.Localization.Get("App.Game.SelectDirTitle", "选择「{0}」游戏根目录").Replace("{0}", _game.GetLocalizedName()));
                if (path == null) return;
                string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
                if (!File.Exists(Path.Combine(path, exeName)))
                {
                    AntdUI.Message.error(_overview, string.Format(AntdUI.Localization.Get("App.Game.ExeNotFound", "所选目录中未找到 {0}"), exeName));
                    return;
                }
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null) { e2.RootPath = path; ConfigHelper.Save(cfg2); }

                // 璺緞鍒氳鏇存柊锛岄噸鏂拌绠?sameRoot / endfieldSameRoot / zipPath
                var cfg3 = ConfigHelper.Load();
                official = cfg3.Games.Find(g => g.IconName == "Arknights");
                bilibili = cfg3.Games.Find(g => g.IconName == "BiliArknights");
                sameRoot = official != null && bilibili != null &&
                    !string.IsNullOrEmpty(official.RootPath) && !string.IsNullOrEmpty(bilibili.RootPath) &&
                    Path.GetFullPath(official.RootPath).Equals(Path.GetFullPath(bilibili.RootPath), StringComparison.OrdinalIgnoreCase);

                endfieldSameRoot = false;
                if (isEndfield)
                {
                    var endfieldIcons2 = new[] { "Endfield", "BiliEndfield", "GlobalEndfield", "PlayEndfield" };
                    foreach (var other in endfieldIcons2)
                    {
                        if (other == _game.IconName) continue;
                        var otherEntry = cfg3.Games.Find(g => g.IconName == other);
                        if (otherEntry != null && !string.IsNullOrEmpty(otherEntry.RootPath) &&
                            Path.GetFullPath(path).Equals(Path.GetFullPath(otherEntry.RootPath), StringComparison.OrdinalIgnoreCase))
                        {
                            endfieldSameRoot = true;
                            break;
                        }
                    }
                }

                needSwitch = isEndfield ? endfieldSameRoot : sameRoot;
            }

            if (needSwitch)
            {
                await GameLauncher.KillArknightsProcesses(isEndfield);
            }

            GameStart.LoadingWaveValue = 0;
            GameStart.Loading = true;
            AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.Game.Loading", "加载中..."), async (config) =>
            {
                try
                {
                    if (_game.IconName == "Arknights")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                        }
                    }
                    else if (_game.IconName == "Endfield")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreEndfieldAccount(selectedAccountId);
                        }
                    }
                    else if (_game.IconName == "GlobalEndfield")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "切换账号中...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreGlobalEndfieldAccount(selectedAccountId);
                        }
                    }
                    if (needSwitch)
                    {
                        bool usedHardLink = false;
                        await GameLauncher.SwitchServerWithResult(path, _game.IconName, msg =>
                        {
                            config.Text = msg;
                            config.Refresh();
                        }, isEndfield, result => usedHardLink = result);

                        if (!usedHardLink)
                        {
                            _overview.BeginInvoke(new Action(() =>
                                AntdUI.Message.info(_overview,
                                    AntdUI.Localization.Get("App.Game.HardLinkTip",
                                        "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"))
                            ));
                        }
                    }
                    // 闅忔満娉㈡氮鍔ㄧ敾锛?~3 绉掞級
                    if (needSwitch && _game.IconName == "Arknights")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = AntdUI.Localization.Get("App.Game.SwitchingAccount", "Switching account...");
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                        }
                    }

                    var rng = new Random();
                    int totalMs = rng.Next(1000, 3001);
                    int steps = 100;
                    int stepMs = totalMs / steps;
                    for (int i = 0; i <= steps; i++)
                    {
                        GameStart.LoadingWaveValue = i / (float)steps;
                        await Task.Delay(stepMs);
                    }

                    GameLauncher.StartArknights(path, _game.IconName);

                    // 妫€娴嬪埌娓告垙杩涚▼鍚庢墠鏄剧ず鍚姩鎴愬姛
                    string procName = (isEndfield) ? "Endfield" : "Arknights";
                    config.Text = AntdUI.Localization.Get("App.Game.WaitingProcess", "等待游戏进程...");
                    config.Refresh();
                    System.Diagnostics.Process gameProc = null;
                    for (int i = 0; i < 30 && gameProc == null; i++)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(procName);
                        if (procs.Length > 0) gameProc = procs[0];
                        else await Task.Delay(1000);
                    }
                    config.OK(AntdUI.Localization.Get("App.Game.LaunchSuccess", "游戏启动成功"));
                    var latestCfg = ConfigHelper.Load();
                    if (latestCfg.CloseAfterLaunch)
                    {
                        _overview.Invoke(new Action(() => Application.Exit()));
                    }
                    else if (latestCfg.HideToTrayOnLaunch)
                    {
                        _overview.Invoke(new Action(() => _overview.HideToTray()));
                        var overviewRef = _overview;
                        var capturedProc = gameProc;
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                if (capturedProc != null)
                                {
                                    try
                                    {
                                        capturedProc.EnableRaisingEvents = true;
                                        capturedProc.WaitForExit();
                                    }
                                    catch
                                    {
                                        // 鏃犳潈鐩戝惉鏃讹紝鏀逛负杞杩涚▼鏄惁杩樺瓨鍦?
                                        while (!capturedProc.HasExited)
                                            System.Threading.Thread.Sleep(3000);
                                    }
                                }
                            }
                            catch { }
                            finally
                            {
                                overviewRef.ShowFromTray();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.LogError(ex, "GameStart");
                    config.Error(ex.Message);
                }
                if (!GameStart.IsDisposed)
                {
                    try
                    {
                        _overview.Invoke(new Action(() => { if (!GameStart.IsDisposed) GameStart.Loading = false; }));
                    }
                    catch
                    {
                        if (!GameStart.IsDisposed) GameStart.Loading = false;
                    }
                }
            });
        }
    }

    class CoverPictureBox : Control
    {
        private Image _image;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image Image
        {
            get => _image;
            set
            {
                _image = value;
                Invalidate();
            }
        }

        public CoverPictureBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null) return;
            var g = e.Graphics;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // cover 妯″紡锛氶摵婊℃帶浠讹紝灞呬腑瑁佸壀
            float srcRatio = (float)Image.Width / Image.Height;
            float dstRatio = (float)Width / Height;
            RectangleF src;
            if (srcRatio > dstRatio)
            {
                float srcW = Image.Height * dstRatio;
                float srcX = (Image.Width - srcW) / 2f;
                src = new RectangleF(srcX, 0, srcW, Image.Height);
            }
            else
            {
                float srcH = Image.Width / dstRatio;
                float srcY = (Image.Height - srcH) / 2f;
                src = new RectangleF(0, srcY, Image.Width, srcH);
            }
            g.DrawImage(Image, new RectangleF(0, 0, Width, Height), src, GraphicsUnit.Pixel);
        }
    }
}
