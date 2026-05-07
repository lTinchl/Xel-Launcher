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
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Bounds = new Rectangle(-2, -2, Width + 4, Height + 4),
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
                Anchor = AnchorStyles.None,
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
                StretchCoverPictureBox();
                PositionLaunchPanel();
                PositionNoticePanel();
                PositionToolSidebar();
            };
            StretchCoverPictureBox();
            PositionLaunchPanel();
            PositionNoticePanel();
            PositionToolSidebar();
        }

        private void StretchCoverPictureBox()
        {
            if (_coverPictureBox == null || _coverPictureBox.IsDisposed) return;
            _coverPictureBox.Bounds = new Rectangle(-2, -2, Width + 4, Height + 4);
        }

        private Image LoadCoverImage(string fallbackPath)
        {
            var cachedPath = GameCoverCache.GetCachedCoverPath(_game.IconName);
            if (!string.IsNullOrEmpty(cachedPath))
            {
                var cached = GameCoverCache.TryLoadImage(cachedPath);
                if (cached != null)
                {
                    _coverTransitionKey = NormalizeCoverTransitionKey(cachedPath);
                    _coverTransitionSignature = CreateCoverTransitionSignature(cached);
                    return cached;
                }
            }

            if (!File.Exists(fallbackPath)) return null;

            var fallback = GameCoverCache.TryLoadImage(fallbackPath);
            if (fallback != null)
            {
                _coverTransitionKey = NormalizeCoverTransitionKey(fallbackPath);
                _coverTransitionSignature = CreateCoverTransitionSignature(fallback);
            }
            return fallback;
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
        }

        private void PositionNoticePanel()
        {
            if (_noticePanel == null || panelLaunch == null) return;

            _noticePanelHome = GetNoticePanelHome(GetLaunchPanelHome());
            if (_switchAnimationActive) return;

            _noticePanel.Bounds = _noticePanelHome;
            _noticePanel.BringToFront();
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

            int maxWidth = Width - panelLaunch.Width - 96;
            int noticeWidth = Math.Min(660, Math.Max(420, maxWidth));
            if (Width < 760) noticeWidth = Math.Max(320, Width - 56);

            int noticeHeight = Width < 760 ? 132 : 150;
            int y = launchPanelHome.Y + panelLaunch.Height - noticeHeight;
            if (y < 24) y = 24;

            return new Rectangle(28, y, noticeWidth, noticeHeight);
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
    }
}
