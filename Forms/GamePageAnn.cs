using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
public class NoticeItem
    {
        public NoticeItem(string tag, string title, string date, string url)
        {
            Tag = tag;
            Title = title;
            Date = date;
            Url = url;
        }

        public string Tag { get; }
        public string Title { get; }
        public string Date { get; }
        public string Url { get; }
    }

    class NoticeBannerItem
    {
        public NoticeBannerItem(Image image, string url, bool ownsImage)
        {
            Image = image;
            Url = url;
            OwnsImage = ownsImage;
        }

        public Image Image { get; set; }
        public string Url { get; }
        public bool OwnsImage { get; }
    }

    public delegate void NoticeClickHandler(object sender, NoticeItem item);

    class NoticeCarouselPanel : Control
    {
private readonly List<NoticeBannerItem> _banners;
        private readonly List<NoticeItem> _notices;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly System.Windows.Forms.Timer _slideTimer;
        private int _selectedBannerIndex;
        private int _noticeScroll;
        private bool _scrollDragging;
        private bool _touchScrolling;
        private int _scrollDragOffset;
        private int _touchStartY;
        private int _touchStartScroll;
        private bool _bannerTouching;
        private bool _bannerTouchMoved;
        private int _bannerTouchStartX;
        private int _bannerTouchStartY;
        private int _bannerDragOffset;
        private bool _bannerAnimating;
        private int _bannerSlideStartOffset;
        private int _bannerSlideTargetOffset;
        private int _bannerSlideStep;
        private long _bannerSlideStartTick;
        private Rectangle _bannerRect;
        private Rectangle _listRect;
        private Rectangle _scrollTrackRect;
        private Rectangle _scrollThumbRect;
        private string _selectedCategory = "";
        private int _hoverNoticeSourceIndex = -1;
        private List<string> _categories = new();
        private readonly List<NoticeHit> _noticeHits = new();
        private readonly List<CategoryHit> _categoryHits = new();
        private readonly System.Windows.Forms.Timer _categoryUnderlineTimer;
        private readonly System.Windows.Forms.Timer _categoryTextTimer;
        private readonly Dictionary<string, float> _categoryTextProgress = new(StringComparer.OrdinalIgnoreCase);
        private RectangleF _categoryUnderlineBounds;
        private RectangleF _categoryUnderlineTarget;
        private bool _categoryUnderlineInitialized;
        private Rectangle _collapseToggleRect;

        public event NoticeClickHandler NoticeClick;
        public event EventHandler CollapsedChanged;

        public bool IsCollapsed { get; private set; }
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ToggleBackColor { get; set; } = Color.FromArgb(92, 255, 255, 255);

        public void SetCollapsed(bool collapsed)
        {
            if (IsCollapsed == collapsed) return;
            IsCollapsed = collapsed;
            _bannerTouching = false;
            _bannerTouchMoved = false;
            _scrollDragging = false;
            _touchScrolling = false;
            Capture = false;
            Invalidate();
        }

        public NoticeCarouselPanel(List<NoticeBannerItem> banners, List<NoticeItem> notices)
        {
            _banners = banners ?? new List<NoticeBannerItem>();
            _notices = notices ?? new List<NoticeItem>();
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.UserPaint, true);
            try { BackColor = Color.Transparent; }
            catch { BackColor = Color.FromArgb(34, 37, 43); }

            _timer = new System.Windows.Forms.Timer { Interval = 4200 };
            _timer.Tick += (s, e) => Next();
            _slideTimer = new System.Windows.Forms.Timer { Interval = AnimationFrameHelper.GetFrameInterval(this) };
            _slideTimer.Tick += (s, e) => UpdateBannerSlide();
            _categoryUnderlineTimer = new System.Windows.Forms.Timer { Interval = AnimationFrameHelper.GetFrameInterval(this) };
            _categoryUnderlineTimer.Tick += (s, e) => UpdateCategoryUnderline();
            _categoryTextTimer = new System.Windows.Forms.Timer { Interval = AnimationFrameHelper.GetFrameInterval(this) };
            _categoryTextTimer.Tick += (s, e) => UpdateCategoryTextAnimation();
            if (_banners.Count > 1) _timer.Start();
        }

        public void SetContent(List<NoticeBannerItem> banners, List<NoticeItem> notices)
        {
            if (banners != null && banners.Count > 0)
            {
                DisposeReplacedBannerImages(banners);
                _banners.Clear();
                _banners.AddRange(banners);
                _selectedBannerIndex = 0;
                _bannerDragOffset = 0;
                _bannerAnimating = false;
                _slideTimer.Stop();
            }

            if (notices != null && notices.Count > 0)
            {
                _notices.Clear();
                _notices.AddRange(notices);
                RebuildCategories();
            }

            _timer.Stop();
            if (_banners.Count > 1) _timer.Start();
            Invalidate();
        }

        public void UpdateFallbackImage(Image image)
        {
            if (_banners.Count == 0 || _banners[0].Image == image) return;
            if (string.IsNullOrEmpty(_banners[0].Url))
                _banners[0].Image = image;
            Invalidate();
        }

        private void Next()
        {
            if (_banners.Count == 0) return;
            if (_bannerTouching || _bannerAnimating) return;
            if (_banners.Count > 1 && _bannerRect.Width > 0)
                StartBannerSlide(0, -_bannerRect.Width, 1);
            else
                SelectNextBanner();
        }

        private void SelectNextBanner()
        {
            if (_banners.Count == 0) return;
            _selectedBannerIndex = (_selectedBannerIndex + 1) % _banners.Count;
        }

        private void SelectPreviousBanner()
        {
            if (_banners.Count == 0) return;
            _selectedBannerIndex--;
            if (_selectedBannerIndex < 0) _selectedBannerIndex = _banners.Count - 1;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!IsInsidePanel(e.Location)) return;

            if (_collapseToggleRect.Contains(e.Location))
            {
                ToggleCollapsed();
                return;
            }

            if (IsCollapsed) return;

            foreach (var hit in _categoryHits)
            {
                if (!hit.Rect.Contains(e.Location)) continue;
                if (string.Equals(_selectedCategory, hit.Category, StringComparison.OrdinalIgnoreCase)) return;
                _selectedCategory = hit.Category;
                _noticeScroll = 0;
                _hoverNoticeSourceIndex = -1;
                StartCategoryTextAnimation();
                Invalidate();
                return;
            }

            if (_scrollThumbRect.Contains(e.Location))
            {
                _scrollDragging = true;
                _scrollDragOffset = e.Y - _scrollThumbRect.Top;
                Capture = true;
                return;
            }

            if (_scrollTrackRect.Contains(e.Location) && _notices.Count > VisibleNoticeRows)
            {
                SetScrollFromThumbTop(e.Y - _scrollThumbRect.Height / 2);
                Invalidate();
                return;
            }

            if (_bannerRect.Contains(e.Location) && _banners.Count > 0)
            {
                _bannerTouching = true;
                _bannerTouchMoved = false;
                _bannerTouchStartX = e.X;
                _bannerTouchStartY = e.Y;
                _bannerDragOffset = 0;
                _bannerAnimating = false;
                _slideTimer.Stop();
                _timer.Stop();
                Capture = true;
                return;
            }

            foreach (var hit in _noticeHits)
            {
                if (hit.Rect.Contains(e.Location))
                {
                    NoticeClick?.Invoke(this, hit.Item);
                    return;
                }
            }

            if (_listRect.Contains(e.Location) && FilteredNotices.Count > VisibleNoticeRows)
            {
                _touchScrolling = true;
                _touchStartY = e.Y;
                _touchStartScroll = _noticeScroll;
                Capture = true;
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsCollapsed) return;
            if (_scrollDragging)
            {
                SetScrollFromThumbTop(e.Y - _scrollDragOffset);
                Invalidate();
                return;
            }

            if (_touchScrolling)
            {
                int deltaRows = (int)Math.Round((_touchStartY - e.Y) / 24D);
                _noticeScroll = _touchStartScroll + deltaRows;
                ClampNoticeScroll();
                Invalidate();
                return;
            }

            if (_bannerTouching && _banners.Count > 1)
            {
                int dx = e.X - _bannerTouchStartX;
                int dy = e.Y - _bannerTouchStartY;
                if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;
                if (Math.Abs(dx) < Math.Abs(dy)) return;

                _bannerDragOffset = Math.Max(-_bannerRect.Width, Math.Min(_bannerRect.Width, dx));
                if (Math.Abs(_bannerDragOffset) > 8)
                    _bannerTouchMoved = true;
                Invalidate();
                return;
            }

            UpdateHoveredNotice(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (IsCollapsed)
            {
                _bannerTouching = false;
                _bannerTouchMoved = false;
                _scrollDragging = false;
                _touchScrolling = false;
                Capture = false;
                return;
            }
            if (_bannerTouching && !_bannerTouchMoved && _bannerRect.Contains(e.Location) && _banners.Count > 0)
            {
                var url = _banners[_selectedBannerIndex].Url;
                if (!string.IsNullOrWhiteSpace(url))
                    TabHeaderForm.Open(url);
            }
            else if (_bannerTouching && _bannerTouchMoved && _banners.Count > 1)
            {
                int threshold = Math.Max(42, _bannerRect.Width / 4);
                if (_bannerDragOffset <= -threshold)
                    StartBannerSlide(_bannerDragOffset, -_bannerRect.Width, 1);
                else if (_bannerDragOffset >= threshold)
                    StartBannerSlide(_bannerDragOffset, _bannerRect.Width, -1);
                else
                    StartBannerSlide(_bannerDragOffset, 0, 0);
            }

            _bannerTouching = false;
            _bannerTouchMoved = false;
            _scrollDragging = false;
            _touchScrolling = false;
            Capture = false;
            if (!_bannerAnimating)
            {
                _bannerDragOffset = 0;
                if (_banners.Count > 1) _timer.Start();
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (IsCollapsed) return;
            if (FilteredNotices.Count <= VisibleNoticeRows) return;

            _noticeScroll += e.Delta < 0 ? 1 : -1;
            _hoverNoticeSourceIndex = -1;
            ClampNoticeScroll();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_scrollDragging || _touchScrolling || _bannerTouching || _bannerAnimating) return;
            if (_hoverNoticeSourceIndex == -1) return;

            _hoverNoticeSourceIndex = -1;
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            _noticeHits.Clear();
            _categoryHits.Clear();
            _collapseToggleRect = Rectangle.Empty;

            if (IsCollapsed)
            {
                PaintCollapsed(g);
                return;
            }

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = RoundedRect(rect, 16))
            using (var bg = new SolidBrush(Color.FromArgb(188, 34, 37, 43)))
            using (var border = new Pen(Color.FromArgb(34, 255, 255, 255), 1))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            PaintCollapseToggle(g, false);

            int pad = 8;
            int thumbW = Width >= 560 ? 242 : 154;
            if (Width < 410) thumbW = 0;
            int thumbH = Height - pad * 2;
            _bannerRect = thumbW > 0 ? new Rectangle(pad, pad, thumbW, thumbH) : Rectangle.Empty;

            if (thumbW > 0)
            {
                using (var imgPath = RoundedRect(_bannerRect, 10))
                {
                    g.SetClip(imgPath);
                    PaintBannerImages(g, _bannerRect);
                    g.ResetClip();
                }

                using var overlay = new LinearGradientBrush(
                    _bannerRect,
                    Color.FromArgb(10, 0, 0, 0),
                    Color.FromArgb(110, 0, 0, 0),
                    LinearGradientMode.Vertical);
                g.FillRectangle(overlay, _bannerRect);
                PaintDots(g, _bannerRect);
            }

            int textX = pad + thumbW + (thumbW > 0 ? 20 : 0);
            int textW = Width - textX - pad;
            if (textW <= 80) return;

            using var titleFont = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            using var tabFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            using var rowFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            using var rowFontMuted = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);

            PaintCategoryTabs(g, textX, 13, textW, titleFont, tabFont);

            int rowTop = 54;
            int rowH = 28;
            var notices = FilteredNotices;
            ClampNoticeScroll();
            int rowCount = Math.Min(VisibleNoticeRows, notices.Count);
            int scrollBarWidth = notices.Count > rowCount ? 12 : 0;
            _listRect = new Rectangle(textX - 6, rowTop, textW + 2, rowCount * rowH);
            for (int i = 0; i < rowCount; i++)
            {
                int sourceIndex = _noticeScroll + i;
                if (sourceIndex >= notices.Count) break;

                var item = notices[sourceIndex];
                bool hovered = sourceIndex == _hoverNoticeSourceIndex;
                var fore = hovered ? Color.White : Color.FromArgb(145, 255, 255, 255);
                var font = hovered ? rowFont : rowFontMuted;
                var titleRect = new Rectangle(textX, rowTop + i * rowH, textW - 56 - scrollBarWidth, rowH);
                var dateRect = new Rectangle(textX + textW - 52 - scrollBarWidth, rowTop + i * rowH, 52, rowH);
                _noticeHits.Add(new NoticeHit(new Rectangle(textX - 6, rowTop + i * rowH + 2, textW - scrollBarWidth + 2, rowH - 4), item, sourceIndex));

                if (hovered)
                {
                    using var activeBg = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
                    using var activePath = RoundedRect(new Rectangle(textX - 6, rowTop + i * rowH + 2, textW - scrollBarWidth + 2, rowH - 4), 5);
                    g.FillPath(activeBg, activePath);
                }

                TextRenderer.DrawText(g, $"[{item.Tag}] {item.Title}", font, titleRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, item.Date, rowFontMuted, dateRect, Color.FromArgb(130, 255, 255, 255),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            PaintScrollBar(g, textX + textW - 8, rowTop + 3, rowCount * rowH - 6);
        }

        private void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
            _bannerTouching = false;
            _bannerTouchMoved = false;
            _scrollDragging = false;
            _touchScrolling = false;
            Capture = false;
            Invalidate();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PaintCollapsed(Graphics g)
        {
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            _collapseToggleRect = rect;
            using (var shadow = new SolidBrush(Color.FromArgb(42, 0, 0, 0)))
            using (var shadowPath = RoundedRect(new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height), 18))
            {
                g.FillPath(shadow, shadowPath);
            }

            using (var path = RoundedRect(rect, 18))
            using (var bg = new LinearGradientBrush(rect,
                BlendWithAlpha(ToggleBackColor, Color.FromArgb(18, 21, 28), 0.72F, 222),
                BlendWithAlpha(ToggleBackColor, Color.FromArgb(18, 21, 28), 0.86F, 226),
                LinearGradientMode.Vertical))
            using (var border = new Pen(Color.FromArgb(54, 185, 206, 230), 1F))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            using var titleFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            TextRenderer.DrawText(g,
                NoticeTitle,
                titleFont,
                new Rectangle(18, 1, Math.Max(40, Width - 68), Height - 2),
                Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            PaintCollapseToggle(g, true);
        }

        private void PaintCollapseToggle(Graphics g, bool collapsed)
        {
            int size = collapsed ? 22 : 28;
            int x = collapsed ? Width - size - 17 : Width - size - 10;
            int y = collapsed ? (Height - size) / 2 : 9;
            var iconRect = new Rectangle(x, y, size, size);
            if (!collapsed) _collapseToggleRect = iconRect;

            using var path = RoundedRect(iconRect, size / 2);
            if (collapsed)
            {
                using var hover = new SolidBrush(Color.FromArgb(22, 255, 255, 255));
                g.FillPath(hover, path);
            }
            else
            {
                using var bg = new SolidBrush(Color.FromArgb(54, 255, 255, 255));
                g.FillPath(bg, path);
            }

            using var pen = new Pen(Color.FromArgb(225, 255, 255, 255), 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            if (collapsed)
            {
                using var arrowPen = new Pen(Color.FromArgb(224, 255, 255, 255), 1.8F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round,
                };
                var p1 = new PointF(iconRect.Left + size * 0.32F, iconRect.Top + size * 0.58F);
                var p2 = new PointF(iconRect.Left + size * 0.50F, iconRect.Top + size * 0.40F);
                var p3 = new PointF(iconRect.Left + size * 0.68F, iconRect.Top + size * 0.58F);
                g.DrawLines(arrowPen, new[] { p1, p2, p3 });
            }
            else
            {
                var p1 = new PointF(iconRect.Left + size * 0.28F, iconRect.Top + size * 0.40F);
                var p2 = new PointF(iconRect.Left + size * 0.50F, iconRect.Top + size * 0.62F);
                var p3 = new PointF(iconRect.Left + size * 0.72F, iconRect.Top + size * 0.40F);
                g.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }

        private static Color BlendWithAlpha(Color baseColor, Color mixColor, float amount, int alpha)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            int r = (int)Math.Round(baseColor.R + (mixColor.R - baseColor.R) * amount);
            int g = (int)Math.Round(baseColor.G + (mixColor.G - baseColor.G) * amount);
            int b = (int)Math.Round(baseColor.B + (mixColor.B - baseColor.B) * amount);
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), r, g, b);
        }

        private Image CurrentBannerImage =>
            _banners.Count == 0 ? null : _banners[Math.Min(_selectedBannerIndex, _banners.Count - 1)].Image;

        private void PaintBannerImages(Graphics g, Rectangle rect)
        {
            int offset = (_bannerTouching || _bannerAnimating) ? _bannerDragOffset : 0;
            DrawCoverImage(g, CurrentBannerImage, OffsetRect(rect, offset));

            if (_banners.Count <= 1 || offset == 0) return;

            if (offset < 0)
                DrawCoverImage(g, GetBannerImage(_selectedBannerIndex + 1), OffsetRect(rect, rect.Width + offset));
            else
                DrawCoverImage(g, GetBannerImage(_selectedBannerIndex - 1), OffsetRect(rect, -rect.Width + offset));
        }

        private Image GetBannerImage(int index)
        {
            if (_banners.Count == 0) return null;

            index %= _banners.Count;
            if (index < 0) index += _banners.Count;
            return _banners[index].Image;
        }

        private static Rectangle OffsetRect(Rectangle rect, int offsetX)
        {
            return new Rectangle(rect.X + offsetX, rect.Y, rect.Width, rect.Height);
        }

        private void StartBannerSlide(int startOffset, int targetOffset, int step)
        {
            if (_banners.Count <= 1 && step != 0)
                return;

            _timer.Stop();
            _bannerAnimating = true;
            _bannerSlideStartOffset = startOffset;
            _bannerSlideTargetOffset = targetOffset;
            _bannerSlideStep = step;
            _bannerSlideStartTick = Environment.TickCount64;
            _bannerDragOffset = startOffset;
            AnimationFrameHelper.ApplyFrameInterval(_slideTimer, this);
            _slideTimer.Start();
            Invalidate();
        }

        private void UpdateBannerSlide()
        {
            const int duration = 280;
            double progress = Math.Min(1D, (Environment.TickCount64 - _bannerSlideStartTick) / (double)duration);
            _bannerDragOffset = _bannerSlideStartOffset + (int)Math.Round((_bannerSlideTargetOffset - _bannerSlideStartOffset) * progress);

            if (progress >= 1D)
            {
                _slideTimer.Stop();
                if (_bannerSlideStep > 0)
                    SelectNextBanner();
                else if (_bannerSlideStep < 0)
                    SelectPreviousBanner();

                _bannerDragOffset = 0;
                _bannerAnimating = false;
                _bannerSlideStep = 0;
                if (_banners.Count > 1) _timer.Start();
            }

            Invalidate();
        }

        private void UpdateHoveredNotice(Point location)
        {
            int hoveredIndex = -1;
            foreach (var hit in _noticeHits)
            {
                if (!hit.Rect.Contains(location)) continue;
                hoveredIndex = hit.SourceIndex;
                break;
            }

            if (hoveredIndex == _hoverNoticeSourceIndex) return;

            _hoverNoticeSourceIndex = hoveredIndex;
            Invalidate();
        }

        private List<NoticeItem> FilteredNotices =>
            string.IsNullOrEmpty(_selectedCategory)
                ? _notices
                : _notices.Where(x => string.Equals(x.Tag, _selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();

        private void PaintCategoryTabs(Graphics g, int x, int y, int width, Font activeFont, Font tabFont)
        {
            if (_categories.Count == 0) RebuildCategories();
            EnsureCategoryTextProgress();

            int currentX = x;
            RectangleF? activeUnderline = null;
            for (int i = 0; i < _categories.Count; i++)
            {
                string category = _categories[i];
                bool active = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase);
                float progress = _categoryTextProgress.TryGetValue(category, out var value)
                    ? value
                    : active ? 1F : 0F;
                var maxSize = TextRenderer.MeasureText(category, activeFont, Size.Empty, TextFormatFlags.NoPadding);
                int tabW = Math.Min(Math.Max(maxSize.Width + 16, 48), 86);
                if (currentX + tabW > x + width) break;

                using var font = new Font(
                    tabFont.FontFamily,
                    tabFont.Size + (activeFont.Size - tabFont.Size) * progress,
                    FontStyle.Bold);
                var size = TextRenderer.MeasureText(category, font, Size.Empty, TextFormatFlags.NoPadding);
                int textAlpha = ClampAlpha((int)Math.Round(120 + 135 * progress));
                var tabRect = new Rectangle(currentX, y, tabW, 28);
                _categoryHits.Add(new CategoryHit(tabRect, category));
                TextRenderer.DrawText(g, category, font, tabRect,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                if (active)
                {
                    int underlineWidth = Math.Max(18, Math.Min(tabW - 8, (int)(maxSize.Width * 0.72F)));
                    activeUnderline = new RectangleF(
                        currentX + Math.Max(0, (maxSize.Width - underlineWidth) / 2F),
                        y + 25,
                        underlineWidth,
                        3);
                }

                currentX += tabW + 8;
            }

            if (activeUnderline.HasValue)
            {
                SetCategoryUnderlineTarget(activeUnderline.Value);
                using var accent = new SolidBrush(Color.FromArgb(255, 225, 0));
                using var path = RoundedRect(Rectangle.Round(_categoryUnderlineBounds), 1);
                g.FillPath(accent, path);
            }
        }

        private void EnsureCategoryTextProgress()
        {
            foreach (var category in _categories)
            {
                if (!_categoryTextProgress.ContainsKey(category))
                    _categoryTextProgress[category] = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase) ? 1F : 0F;
            }
        }

        private void StartCategoryTextAnimation()
        {
            EnsureCategoryTextProgress();
            AnimationFrameHelper.ApplyFrameInterval(_categoryTextTimer, this);
            if (!_categoryTextTimer.Enabled)
                _categoryTextTimer.Start();
        }

        private void UpdateCategoryTextAnimation()
        {
            bool animating = false;
            foreach (var category in _categories)
            {
                float target = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase) ? 1F : 0F;
                float current = _categoryTextProgress.TryGetValue(category, out var value) ? value : target;
                float next = Ease(current, target, _categoryTextTimer);
                if (Math.Abs(next - target) <= 0.02F)
                    next = target;
                else
                    animating = true;

                _categoryTextProgress[category] = next;
            }

            if (!animating)
                _categoryTextTimer.Stop();

            Invalidate();
        }

        private void SetCategoryUnderlineTarget(RectangleF target)
        {
            if (!_categoryUnderlineInitialized)
            {
                _categoryUnderlineBounds = target;
                _categoryUnderlineTarget = target;
                _categoryUnderlineInitialized = true;
                return;
            }

            if (NearlySame(_categoryUnderlineTarget, target)) return;

            _categoryUnderlineTarget = target;
            AnimationFrameHelper.ApplyFrameInterval(_categoryUnderlineTimer, this);
            if (!_categoryUnderlineTimer.Enabled)
                _categoryUnderlineTimer.Start();
        }

        private void UpdateCategoryUnderline()
        {
            _categoryUnderlineBounds = new RectangleF(
                Ease(_categoryUnderlineBounds.X, _categoryUnderlineTarget.X, _categoryUnderlineTimer),
                _categoryUnderlineTarget.Y,
                Ease(_categoryUnderlineBounds.Width, _categoryUnderlineTarget.Width, _categoryUnderlineTimer),
                _categoryUnderlineTarget.Height);

            if (NearlySame(_categoryUnderlineBounds, _categoryUnderlineTarget, 0.5F))
            {
                _categoryUnderlineBounds = _categoryUnderlineTarget;
                _categoryUnderlineTimer.Stop();
            }

            Invalidate();
        }

        private static float Ease(float current, float target, System.Windows.Forms.Timer timer) =>
            current + (target - current) * AnimationFrameHelper.ScaleEase(0.28F, timer);

        private static bool NearlySame(RectangleF a, RectangleF b, float tolerance = 0.1F) =>
            Math.Abs(a.X - b.X) <= tolerance &&
            Math.Abs(a.Y - b.Y) <= tolerance &&
            Math.Abs(a.Width - b.Width) <= tolerance &&
            Math.Abs(a.Height - b.Height) <= tolerance;

        private static int ClampAlpha(int value) =>
            Math.Max(0, Math.Min(255, value));

        private static string NoticeTitle =>
            AntdUI.Localization.Get("App.Game.Notice", "公告");

        private void PaintScrollBar(Graphics g, int x, int y, int height)
        {
            int count = FilteredNotices.Count;
            if (count <= VisibleNoticeRows)
            {
                _scrollTrackRect = Rectangle.Empty;
                _scrollThumbRect = Rectangle.Empty;
                return;
            }

            _scrollTrackRect = new Rectangle(x, y, 4, height);
            int thumbHeight = Math.Max(22, height * VisibleNoticeRows / count);
            int maxScroll = MaxNoticeScroll;
            int travel = Math.Max(1, height - thumbHeight);
            int thumbTop = y + (maxScroll == 0 ? 0 : _noticeScroll * travel / maxScroll);
            _scrollThumbRect = new Rectangle(x - 2, thumbTop, 8, thumbHeight);

            using var track = new SolidBrush(Color.FromArgb(32, 255, 255, 255));
            using var thumb = new SolidBrush(_scrollDragging
                ? Color.FromArgb(188, 255, 255, 255)
                : Color.FromArgb(112, 255, 255, 255));
            using var trackPath = RoundedRect(_scrollTrackRect, 2);
            using var thumbPath = RoundedRect(_scrollThumbRect, 4);
            g.FillPath(track, trackPath);
            g.FillPath(thumb, thumbPath);
        }

        private const int VisibleNoticeRows = 3;

        private int MaxNoticeScroll => Math.Max(0, FilteredNotices.Count - VisibleNoticeRows);

        private void ClampNoticeScroll()
        {
            if (_noticeScroll < 0) _noticeScroll = 0;
            int max = MaxNoticeScroll;
            if (_noticeScroll > max) _noticeScroll = max;
        }

        private void SetScrollFromThumbTop(int thumbTop)
        {
            if (_scrollTrackRect.Height <= 0 || _scrollThumbRect.Height <= 0) return;

            int maxScroll = MaxNoticeScroll;
            if (maxScroll == 0)
            {
                _noticeScroll = 0;
                return;
            }

            int travel = Math.Max(1, _scrollTrackRect.Height - _scrollThumbRect.Height);
            int localTop = Math.Max(0, Math.Min(travel, thumbTop - _scrollTrackRect.Top));
            _noticeScroll = (int)Math.Round(localTop / (double)travel * maxScroll);
            ClampNoticeScroll();
        }

        private void RebuildCategories()
        {
            _categories = _notices
                .Select(x => string.IsNullOrWhiteSpace(x.Tag) ? NoticeTitle : x.Tag)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            if (_categories.Count == 0) _categories.Add(NoticeTitle);
            if (string.IsNullOrEmpty(_selectedCategory) || !_categories.Contains(_selectedCategory))
                _selectedCategory = _categories[0];
            _noticeScroll = 0;
            _categoryTextTimer?.Stop();
            _categoryTextProgress.Clear();
            foreach (var category in _categories)
                _categoryTextProgress[category] = string.Equals(category, _selectedCategory, StringComparison.OrdinalIgnoreCase) ? 1F : 0F;
            _categoryUnderlineInitialized = false;
        }

        private void PaintDots(Graphics g, Rectangle rect)
        {
            if (_banners.Count <= 1) return;

            int dotW = 18;
            int dotH = 4;
            int gap = 5;
            int totalW = _banners.Count * dotW + (_banners.Count - 1) * gap;
            int x = rect.Left + (rect.Width - totalW) / 2;
            int y = rect.Bottom - 14;

            for (int i = 0; i < _banners.Count; i++)
            {
                using var brush = new SolidBrush(i == _selectedBannerIndex
                    ? Color.FromArgb(235, 255, 255, 255)
                    : Color.FromArgb(95, 255, 255, 255));
                using var path = RoundedRect(new Rectangle(x + i * (dotW + gap), y, dotW, dotH), 2);
                g.FillPath(brush, path);
            }
        }

        private static void DrawCoverImage(Graphics g, Image image, Rectangle dst)
        {
            if (image == null)
            {
                using var empty = new LinearGradientBrush(dst, Color.FromArgb(40, 80, 120), Color.FromArgb(18, 20, 26), 45F);
                g.FillRectangle(empty, dst);
                return;
            }

            float srcRatio = (float)image.Width / image.Height;
            float dstRatio = (float)dst.Width / dst.Height;
            RectangleF src;
            if (srcRatio > dstRatio)
            {
                float srcW = image.Height * dstRatio;
                src = new RectangleF((image.Width - srcW) / 2f, 0, srcW, image.Height);
            }
            else
            {
                float srcH = image.Width / dstRatio;
                src = new RectangleF(0, (image.Height - srcH) / 2f, image.Width, srcH);
            }

            g.DrawImage(image, dst, src, GraphicsUnit.Pixel);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private bool IsInsidePanel(Point location)
        {
            if (Width <= 0 || Height <= 0) return false;
            using var path = RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), 16);
            return path.IsVisible(location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _slideTimer.Stop();
                _slideTimer.Dispose();
                _categoryUnderlineTimer.Stop();
                _categoryUnderlineTimer.Dispose();
                _categoryTextTimer.Stop();
                _categoryTextTimer.Dispose();
                foreach (var item in _banners)
                    if (item.OwnsImage) item.Image?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void DisposeReplacedBannerImages(List<NoticeBannerItem> replacement)
        {
            foreach (var current in _banners)
            {
                if (replacement.Any(x => ReferenceEquals(x.Image, current.Image))) continue;
                if (current.OwnsImage) current.Image?.Dispose();
            }
        }

        private readonly struct NoticeHit
        {
            public NoticeHit(Rectangle rect, NoticeItem item, int sourceIndex)
            {
                Rect = rect;
                Item = item;
                SourceIndex = sourceIndex;
            }

            public Rectangle Rect { get; }
            public NoticeItem Item { get; }
            public int SourceIndex { get; }
        }

        private readonly struct CategoryHit
        {
            public CategoryHit(Rectangle rect, string category)
            {
                Rect = rect;
                Category = category;
            }

            public Rectangle Rect { get; }
            public string Category { get; }
        }
    }
}
