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
        private const int NoticeAnimationDurationMs = 180;
        private System.Windows.Forms.Timer _noticeAnimationTimer;
        private Stopwatch _noticeAnimationWatch;
        private Rectangle _noticeAnimationFrom;
        private Rectangle _noticeAnimationTo;
        private bool _noticeAnimationEnabled;
        private bool _noticeUserCollapsed;

        private void BuildCoverImage(Image initialCoverImage, string initialCoverPath)
        {
            string imgFile = _game.IconName switch
            {
                "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield" => "End.jpg",
                _ => "Arknights.jpg",
            };
            string fallbackPath = FindIconResourceFile(imgFile);

            var img = initialCoverImage ?? GameCoverCache.TryLoadImage(fallbackPath);
            if (img == null) return;
            _coverTransitionKey = NormalizeCoverTransitionKey(
                string.IsNullOrWhiteSpace(initialCoverPath) ? fallbackPath : initialCoverPath);
            _coverTransitionSignature = CreateCoverTransitionSignature(img);
            _coverImage = img;
            var pb = new CoverPictureBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Bounds = new Rectangle(-2, -2, Width + 4, Height + 4),
                Image = img,
            };
            _coverPictureBox = pb;
            AddBuiltInToolButtons();
            LoadCustomToolButtons();
            _subBtns.Add(CreateAddCustomToolButton());

            Controls.Add(pb);
            _coverPictureBox.Controls.Add(panelLaunch);
            BuildToolSidebar();
            _noticePanel = new NoticeCarouselPanel(CreateFallbackBanners(img), CreateFallbackNotices())
            {
                Anchor = AnchorStyles.None,
            };
            _noticePanel.NoticeClick += NoticePanel_NoticeClick;
            _noticeUserCollapsed = LoadNoticeCollapsedPreference();
            _noticePanel.SetCollapsed(_noticeUserCollapsed);
            _noticePanel.CollapsedChanged += (s, e) =>
            {
                _noticeUserCollapsed = _noticePanel.IsCollapsed;
                SaveNoticeCollapsedPreference(_noticeUserCollapsed);
                AnimateNoticePanelToHome();
            };
            _coverPictureBox.Controls.Add(_noticePanel);
            bool cachedContentLoadStarted = false;
            HandleCreated += (s, e) => {
                PositionLaunchPanel();
                PositionToolSidebar();
                PositionNoticePanel();
                UpdateLaunchPanelColor();
                if (!cachedContentLoadStarted)
                {
                    cachedContentLoadStarted = true;
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed) return;
                        _ = ApplyCachedLauncherNoticeAsync();
                    }));
                }
                };
            SizeChanged += (s, e) =>
            {
                StretchCoverPictureBox();
                PositionLaunchPanel();
                PositionNoticePanel();
                PositionToolSidebar();
            };
            Disposed += (s, e) =>
            {
                StopNoticeAnimation(false);
                _noticeAnimationTimer?.Dispose();
                _noticeAnimationTimer = null;
            };
            StretchCoverPictureBox();
            PositionLaunchPanel();
            PositionNoticePanel();
            PositionToolSidebar();
            _noticeAnimationEnabled = true;
        }

        private void StretchCoverPictureBox()
        {
            if (_coverPictureBox == null || _coverPictureBox.IsDisposed) return;
            _coverPictureBox.Bounds = new Rectangle(-2, -2, Width + 4, Height + 4);
        }

        private static string FindIconResourceFile(string fileName)
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icon", fileName);
            if (File.Exists(iconPath)) return iconPath;

            return Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        }

        private async Task RefreshRemoteCoverAsync()
        {
            if (!GameCoverCache.TryBeginDailyCoverRefresh(_game.IconName))
            {
                LogHelper.Log($"Client cover daily refresh skipped: {_game.IconName}");
                return;
            }

            try
            {
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

                var beforePath = GameCoverCache.GetCachedCoverPath(_game.IconName);
                var beforeWrite = !string.IsNullOrEmpty(beforePath) && File.Exists(beforePath)
                    ? File.GetLastWriteTimeUtc(beforePath)
                    : DateTime.MinValue;
                var imagePath = await Task.Run(
                    () => GameCoverCache.UpdateAsync(_game.IconName, imageUrl, ct: token, forceRefresh: true),
                    token);
                if (string.IsNullOrEmpty(imagePath) || _coverCts.IsCancellationRequested)
                {
                    LogHelper.Log($"Client cover cache skipped: {_game.IconName}");
                    return;
                }
                if (!IsHandleCreated || IsDisposed) return;
                if (string.Equals(imagePath, beforePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(imagePath) &&
                    File.GetLastWriteTimeUtc(imagePath) == beforeWrite)
                    return;

                var image = await Task.Run(() => GameCoverCache.TryLoadImage(imagePath), token);
                if (image == null) return;
                if (!IsHandleCreated || IsDisposed || token.IsCancellationRequested)
                {
                    image.Dispose();
                    return;
                }

                ApplyCoverImage(image, imagePath);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.RefreshRemoteCoverAsync({_game.IconName})");
            }
            finally
            {
                GameCoverCache.MarkDailyCoverRefreshAttempt(_game.IconName);
            }
        }

        private void ApplyCoverImage(Image image, string imagePath)
        {
            if (_coverPictureBox == null || _coverPictureBox.IsDisposed)
            {
                image?.Dispose();
                return;
            }
            if (image == null) return;

            var oldImage = _coverImage;
            _coverImage = image;
            _coverAccentPaletteValid = false;
            _coverTransitionKey = NormalizeCoverTransitionKey(imagePath);
            _coverTransitionSignature = CreateCoverTransitionSignature(image);
            _coverPictureBox.Image = image;
            _noticePanel?.UpdateFallbackImage(image);
            UpdateLaunchPanelColor();
            _coverPictureBox.Invalidate();
            oldImage?.Dispose();
            LogHelper.Log($"Client cover applied: {_game.IconName} -> {imagePath}");
        }

        private void PositionLaunchPanel()
        {
            if (panelLaunch == null || _coverPictureBox == null) return;

            _launchPanelHome = GetLaunchPanelHome();
            if (_switchAnimationActive) return;

            panelLaunch.Location = _launchPanelHome;
            panelLaunch.BringToFront();
            _noticePanel?.BringToFront();
        }

        private void PositionNoticePanel()
        {
            if (_noticePanel == null || panelLaunch == null) return;

            _noticePanelHome = GetNoticePanelHome(GetLaunchPanelHome());
            if (_switchAnimationActive) return;

            _noticePanel.Visible = !_noticePanelHome.IsEmpty;
            if (!_noticePanel.Visible) return;

            StopNoticeAnimation(false);
            _noticePanel.Bounds = _noticePanelHome;
            _noticePanel.BringToFront();
        }

        private void AnimateNoticePanelToHome()
        {
            if (_noticePanel == null || panelLaunch == null || _coverPictureBox == null) return;

            _noticePanelHome = GetNoticePanelHome(GetLaunchPanelHome());
            if (_noticePanelHome.IsEmpty)
            {
                StopNoticeAnimation(false);
                _noticePanel.Visible = false;
                return;
            }

            _noticePanel.Visible = true;
            _noticePanel.BringToFront();

            if (!_noticeAnimationEnabled || !_noticePanel.IsHandleCreated || _noticePanel.Bounds.IsEmpty)
            {
                _noticePanel.Bounds = _noticePanelHome;
                return;
            }

            StartNoticeAnimation(_noticePanel.Bounds, _noticePanelHome);
        }

        private void StartNoticeAnimation(Rectangle from, Rectangle to)
        {
            if (from == to)
            {
                _noticePanel.Bounds = to;
                return;
            }

            _noticeAnimationFrom = from;
            _noticeAnimationTo = to;
            _noticeAnimationWatch = Stopwatch.StartNew();
            _noticeAnimationTimer ??= new System.Windows.Forms.Timer();
            AnimationFrameHelper.ApplyFrameInterval(_noticeAnimationTimer, this);
            _noticeAnimationTimer.Tick -= NoticeAnimationTimer_Tick;
            _noticeAnimationTimer.Tick += NoticeAnimationTimer_Tick;
            _noticeAnimationTimer.Start();
        }

        private void NoticeAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_noticePanel == null || _noticePanel.IsDisposed)
            {
                StopNoticeAnimation(false);
                return;
            }

            double progress = Math.Min(1D, _noticeAnimationWatch?.Elapsed.TotalMilliseconds / NoticeAnimationDurationMs ?? 1D);
            _noticePanel.Bounds = LerpRect(_noticeAnimationFrom, _noticeAnimationTo, progress);
            _noticePanel.Invalidate();

            if (progress < 1D) return;

            StopNoticeAnimation(true);
        }

        private void StopNoticeAnimation(bool finish)
        {
            if (_noticeAnimationTimer != null)
            {
                _noticeAnimationTimer.Stop();
                _noticeAnimationTimer.Tick -= NoticeAnimationTimer_Tick;
            }
            _noticeAnimationWatch?.Stop();
            _noticeAnimationWatch = null;

            if (finish && _noticePanel != null && !_noticePanel.IsDisposed)
                _noticePanel.Bounds = _noticeAnimationTo;
        }

        private static Rectangle LerpRect(Rectangle from, Rectangle to, double t)
        {
            t = Math.Max(0D, Math.Min(1D, t));
            int x = (int)Math.Round(from.X + (to.X - from.X) * t);
            int y = (int)Math.Round(from.Y + (to.Y - from.Y) * t);
            int width = (int)Math.Round(from.Width + (to.Width - from.Width) * t);
            int height = (int)Math.Round(from.Height + (to.Height - from.Height) * t);
            return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
        }

        private Point GetLaunchPanelHome()
        {
            if (panelLaunch == null || _coverPictureBox == null) return Point.Empty;

            int x = _coverPictureBox.Width - panelLaunch.Width - 16;
            int y = _coverPictureBox.Height - panelLaunch.Height - 12;
            return new Point(Math.Max(0, x), Math.Max(0, y));
        }

        private Rectangle GetNoticePanelHome(Point launchPanelHome)
        {
            if (_noticePanel == null || panelLaunch == null) return Rectangle.Empty;

            const int left = 28;
            const int minReadableWidth = 420;
            const int toolGap = 18;
            const int collapsedWidth = 190;
            const int collapsedHeight = 46;
            const int collapsedGap = 10;

            Rectangle CollapsedHome()
            {
                int rowY = launchPanelHome == Point.Empty
                    ? Math.Max(24, (_coverPictureBox?.Height ?? Height) - collapsedHeight - 12)
                    : launchPanelHome.Y + (panelLaunch.Height - collapsedHeight) / 2;
                int sameRowRight = launchPanelHome.X - collapsedGap;
                if (sameRowRight - left >= collapsedWidth)
                    return new Rectangle(left, Math.Max(24, rowY), collapsedWidth, collapsedHeight);

                int aboveY = launchPanelHome == Point.Empty
                    ? rowY
                    : launchPanelHome.Y - collapsedHeight - collapsedGap;
                int maxX = (_coverPictureBox?.Width ?? Width) - collapsedWidth - 28;
                int x = Math.Max(0, Math.Min(left, maxX));
                int y = Math.Max(24, aboveY);
                return new Rectangle(x, y, collapsedWidth, collapsedHeight);
            }

            if (_noticeUserCollapsed)
            {
                if (!_noticePanel.IsCollapsed)
                    _noticePanel.SetCollapsed(true);
                return CollapsedHome();
            }

            int rightLimit = _coverPictureBox?.Width - 28 ?? Width - 28;
            if (_toolSidebar != null)
            {
                var toolHome = GetToolSidebarHome();
                if (!toolHome.IsEmpty)
                    rightLimit = Math.Min(rightLimit, toolHome.X - toolGap);
            }
            if (launchPanelHome != Point.Empty)
                rightLimit = Math.Min(rightLimit, launchPanelHome.X - toolGap);

            int availableWidth = rightLimit - left;
            if (availableWidth < minReadableWidth)
            {
                _noticePanel.SetCollapsed(true);
                return CollapsedHome();
            }

            int noticeHeight = Width < 760 ? 132 : 150;
            int y = launchPanelHome.Y + panelLaunch.Height - noticeHeight;
            if (y < 24)
            {
                _noticePanel.SetCollapsed(true);
                return CollapsedHome();
            }

            if (_noticePanel.IsCollapsed)
                _noticePanel.SetCollapsed(false);

            int noticeWidth = Math.Min(660, availableWidth);
            return new Rectangle(left, y, noticeWidth, noticeHeight);
        }

        private bool LoadNoticeCollapsedPreference()
        {
            try
            {
                var cfg = ConfigHelper.Load();
                return cfg.NoticePanelCollapsed != null &&
                       cfg.NoticePanelCollapsed.TryGetValue(_game.IconName, out bool collapsed) &&
                       collapsed;
            }
            catch
            {
                return false;
            }
        }

        private void SaveNoticeCollapsedPreference(bool collapsed)
        {
            try
            {
                var cfg = ConfigHelper.Load();
                cfg.NoticePanelCollapsed ??= new Dictionary<string, bool>();
                if (collapsed)
                    cfg.NoticePanelCollapsed[_game.IconName] = true;
                else
                    cfg.NoticePanelCollapsed.Remove(_game.IconName);
                ConfigHelper.Save(cfg);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.SaveNoticeCollapsedPreference({_game.IconName})");
            }
        }

        private List<NoticeBannerItem> CreateFallbackBanners(Image image)
        {
            return new List<NoticeBannerItem>();
        }

        private List<NoticeItem> CreateFallbackNotices()
        {
            bool endfield = _game.IconName is "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield";
            if (endfield)
            {
                return new List<NoticeItem>
                {
                };
            }

            return new List<NoticeItem>
            {
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
                    var image = GameCoverCache.TryLoadCachedNoticeBanner(_game.IconName, banner.ImageUrl);
                    if (image == null)
                    {
                        var imagePath = await GameCoverCache.UpdateNoticeBannerAsync(_game.IconName, banner.ImageUrl, token).ConfigureAwait(false);
                        image = GameCoverCache.TryLoadImage(imagePath);
                    }
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

        private async Task ApplyCachedLauncherNoticeAsync()
        {
            if (_noticePanel == null) return;

            List<NoticeBannerItem> banners = null;
            try
            {
                var token = _coverCts.Token;
                var result = await Task.Run(() =>
                {
                    var content = GameCoverCache.GetCachedLauncherNoticeContent(_game.IconName);
                    if (content == null)
                        return (Banners: (List<NoticeBannerItem>)null, Notices: (List<NoticeItem>)null);

                    var loadedBanners = new List<NoticeBannerItem>();
                    foreach (var banner in (content.Banners ?? Array.Empty<LauncherBannerItem>()).Take(6))
                    {
                        var image = GameCoverCache.TryLoadCachedNoticeBanner(_game.IconName, banner.ImageUrl);
                        if (image != null)
                            loadedBanners.Add(new NoticeBannerItem(image, banner.JumpUrl ?? "", true));
                    }

                    return (Banners: loadedBanners, Notices: CreateNoticeItems(content.Notices));
                }, token);
                banners = result.Banners;

                if (banners == null || token.IsCancellationRequested || IsDisposed || _noticePanel.IsDisposed)
                    return;

                _noticePanel.SetContent(banners, result.Notices);
                banners = null;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"GamePage.ApplyCachedLauncherNoticeAsync({_game.IconName})");
            }
            finally
            {
                if (banners != null)
                {
                    foreach (var banner in banners)
                        if (banner.OwnsImage) banner.Image?.Dispose();
                }
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
    }
}
