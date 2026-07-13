using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    internal enum UpdateReminderAction
    {
        None,
        UpdateNow,
        SuppressVersion
    }

    internal sealed class UpdateReminderDialog : UserControl
    {
        private readonly RoundedPanel _notesCard;
        private readonly ReleaseNotesView _notes;
        private readonly ThinScrollBar _scrollBar;
        private readonly Color _cardBack;
        private readonly Color _normalText;
        private readonly Color _subtleText;
        private readonly Color _accent;

        public UpdateReminderAction SelectedAction { get; private set; }

        private static string L(string key, string fallback) =>
            AntdUI.Localization.Get(key, fallback);

        public UpdateReminderDialog(UpdateInfo info, string currentVersion)
        {
            Size = new Size(540, 370);
            MinimumSize = Size;
            BackColor = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkBackground : Color.White;

            _normalText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            _subtleText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForegroundSecondary : Color.FromArgb(104, 112, 124);
            _accent = AntdUI.Style.Db.Primary;
            _cardBack = AntdUI.Config.IsDark ? Color.FromArgb(26, 31, 38) : Color.FromArgb(247, 249, 252);
            var border = AntdUI.Config.IsDark ? Color.FromArgb(60, 68, 80) : Color.FromArgb(220, 226, 235);

            var glyph = new UpdateGlyph
            {
                Location = new Point(20, 13),
                Size = new Size(50, 50),
                AccentColor = _accent,
            };

            var title = new AntdUI.Label
            {
                Text = string.Format(L("App.Update.ReminderTitle", "发现新版本 v{0}"), info.LatestVersion),
                AutoSize = false,
                Location = new Point(86, 11),
                Size = new Size(430, 32),
                Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Regular),
                ForeColor = _normalText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var version = new AntdUI.Label
            {
                Text = string.Format(L("App.Update.ReminderSubtitle", "当前版本 v{0}，可更新到 v{1}"), currentVersion, info.LatestVersion),
                AutoSize = false,
                Location = new Point(87, 43),
                Size = new Size(429, 22),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = _subtleText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _notesCard = new RoundedPanel
            {
                Location = new Point(20, 84),
                Size = new Size(500, 190),
                FillColor = _cardBack,
                BorderColor = border,
                Radius = 10,
            };

            var notesTitle = new AntdUI.Label
            {
                Text = L("App.Update.ReleaseNotes", "更新内容"),
                AutoSize = false,
                Location = new Point(16, 12),
                Size = new Size(454, 24),
                Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Regular),
                ForeColor = _normalText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _notes = new ReleaseNotesView(
                GetSummaryLines(info.Changelog),
                _normalText,
                _accent,
                _cardBack)
            {
                Location = new Point(15, 44),
                Size = new Size(456, 128),
                BackColor = _cardBack,
                TabStop = false,
                Cursor = Cursors.Arrow,
            };
            _notes.ScrollChanged += UpdateScrollBar;

            _scrollBar = new ThinScrollBar(
                AntdUI.Config.IsDark ? Color.FromArgb(46, 53, 63) : Color.FromArgb(230, 235, 242),
                AntdUI.Config.IsDark ? Color.FromArgb(116, 128, 146) : Color.FromArgb(150, 160, 176))
            {
                Location = new Point(480, 47),
                Size = new Size(8, 122),
                BackColor = _cardBack,
                Visible = false,
            };
            _scrollBar.ScrollRequested += ScrollNotesToTrackTop;
            _scrollBar.MouseWheel += (s, e) => _notes.ScrollWheel(e.Delta);

            var closeHint = new AntdUI.Label
            {
                Text = L("App.Update.RemindLaterHint", "直接关闭窗口，下次启动时仍会提醒"),
                AutoSize = false,
                Location = new Point(20, 282),
                Size = new Size(500, 20),
                Font = new Font("Microsoft YaHei UI", 8.75F, FontStyle.Regular),
                ForeColor = _subtleText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
            };

            var btnUpdate = CreateButton(
                L("App.Update.DownloadNow", "立即更新"),
                AntdUI.TTypeMini.Primary,
                UpdateReminderAction.UpdateNow,
                112);
            var btnSuppress = CreateButton(
                L("App.Update.SuppressVersion", "当前版本不再提醒"),
                AntdUI.TTypeMini.Default,
                UpdateReminderAction.SuppressVersion,
                160);

            btnUpdate.Location = new Point(Width - 20 - btnUpdate.Width, 320);
            btnSuppress.Location = new Point(btnUpdate.Left - 10 - btnSuppress.Width, 320);

            _notesCard.Controls.Add(notesTitle);
            _notesCard.Controls.Add(_notes);
            _notesCard.Controls.Add(_scrollBar);
            Controls.Add(glyph);
            Controls.Add(title);
            Controls.Add(version);
            Controls.Add(_notesCard);
            Controls.Add(closeHint);
            Controls.Add(btnSuppress);
            Controls.Add(btnUpdate);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SyncHostBackground();
            BeginInvoke(new Action(() =>
            {
                _notes.RecalculateLayout();
                UpdateScrollBar();
            }));
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            SyncHostBackground();
        }

        private void SyncHostBackground()
        {
            var hostBack = Parent?.BackColor ?? BackColor;
            if (hostBack == Color.Empty || hostBack == Color.Transparent) return;
            BackColor = hostBack;
        }

        private AntdUI.Button CreateButton(
            string text,
            AntdUI.TTypeMini type,
            UpdateReminderAction action,
            int minimumWidth)
        {
            using var font = new Font("Microsoft YaHei UI", 9F);
            var width = Math.Max(minimumWidth, TextRenderer.MeasureText(
                text,
                font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width + 36);

            var button = new AntdUI.Button
            {
                Text = text,
                Type = type,
                Radius = 7,
                Size = new Size(width, 36),
                Font = new Font("Microsoft YaHei UI", 9F),
                TextAlign = ContentAlignment.MiddleCenter,
                TabStop = false,
                WaveSize = 0,
            };

            if (type == AntdUI.TTypeMini.Default)
            {
                button.Ghost = true;
                button.BorderWidth = 1F;
                button.DefaultBorderColor = AntdUI.Config.IsDark
                    ? Color.FromArgb(76, 86, 102)
                    : Color.FromArgb(210, 218, 230);
                button.BackHover = AntdUI.Config.IsDark
                    ? Color.FromArgb(46, 53, 64)
                    : Color.FromArgb(240, 244, 249);
                button.BackActive = AntdUI.Config.IsDark
                    ? Color.FromArgb(54, 62, 74)
                    : Color.FromArgb(232, 238, 246);
            }

            button.Click += (s, e) => Complete(action);
            return button;
        }

        private void Complete(UpdateReminderAction action)
        {
            SelectedAction = action;
            if (FindForm() is Form form)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        }

        private static List<string> GetSummaryLines(string changelog)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(changelog))
            {
                result.Add(L("App.Update.NoChangelog", "暂无更新内容。"));
                return result;
            }

            foreach (var raw in changelog.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("---", StringComparison.Ordinal)) continue;

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    var heading = line.TrimStart('#').Trim();
                    if (heading.Equals("What's Changed", StringComparison.OrdinalIgnoreCase) ||
                        heading.Equals("更新内容", StringComparison.OrdinalIgnoreCase))
                        continue;
                    line = heading;
                }

                line = line.TrimStart('-', '*', '•').Trim();
                line = line.Replace("**", "").Replace("__", "").Trim();
                if (line.Length == 0 || line.StartsWith("Full Changelog", StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(line);
                if (result.Count >= 8) break;
            }

            if (result.Count == 0)
                result.Add(L("App.Update.NoChangelog", "暂无更新内容。"));
            return result;
        }

        private void UpdateScrollBar()
        {
            if (_scrollBar == null || _scrollBar.IsDisposed) return;
            if (_notes.MaxScroll <= 0 || _notes.ContentHeight <= 0)
            {
                _scrollBar.Visible = false;
                return;
            }

            var thumbHeight = Math.Max(22, _scrollBar.Height * _notes.ClientSize.Height / _notes.ContentHeight);
            var thumbTop = (_scrollBar.Height - thumbHeight) * _notes.ScrollOffset / _notes.MaxScroll;
            _scrollBar.SetThumb(thumbTop, thumbHeight);
            _scrollBar.Visible = true;
            _scrollBar.BringToFront();
        }

        private void ScrollNotesToTrackTop(int requestedTop)
        {
            var maxThumbTop = Math.Max(1, _scrollBar.Height - _scrollBar.ThumbHeight);
            var clampedTop = Math.Max(0, Math.Min(maxThumbTop, requestedTop));
            _notes.SetScrollOffset(clampedTop * _notes.MaxScroll / maxThumbTop);
        }

        private sealed class ReleaseNotesView : Control
        {
            private readonly IReadOnlyList<string> _items;
            private readonly List<int> _itemHeights = new List<int>();
            private readonly Color _textColor;
            private readonly Color _accentColor;
            private readonly Font _bodyFont;
            private int _scrollOffset;

            public event Action ScrollChanged;
            public int ContentHeight { get; private set; }
            public int ScrollOffset => _scrollOffset;
            public int MaxScroll => Math.Max(0, ContentHeight - ClientSize.Height);

            public ReleaseNotesView(
                IReadOnlyList<string> items,
                Color textColor,
                Color accentColor,
                Color backgroundColor)
            {
                _items = items;
                _textColor = textColor;
                _accentColor = accentColor;
                _bodyFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
                BackColor = backgroundColor;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            public void RecalculateLayout()
            {
                if (!IsHandleCreated || ClientSize.Width <= 20) return;

                using var graphics = CreateGraphics();
                ConfigureTextRendering(graphics);
                using var format = CreateTextFormat();
                _itemHeights.Clear();

                var textWidth = Math.Max(1, ClientSize.Width - 22);
                var total = 2;
                foreach (var item in _items)
                {
                    var measured = graphics.MeasureString(item, _bodyFont, textWidth, format);
                    var height = Math.Max(_bodyFont.Height, (int)Math.Ceiling(measured.Height)) + 6;
                    _itemHeights.Add(height);
                    total += height;
                }

                ContentHeight = Math.Max(0, total);
                _scrollOffset = Math.Min(_scrollOffset, MaxScroll);
                ScrollChanged?.Invoke();
                Invalidate();
            }

            public void SetScrollOffset(int offset)
            {
                var next = Math.Max(0, Math.Min(MaxScroll, offset));
                if (next == _scrollOffset) return;
                _scrollOffset = next;
                Invalidate();
                ScrollChanged?.Invoke();
            }

            public void ScrollWheel(int delta)
            {
                var notches = delta / 120;
                if (notches == 0) return;
                SetScrollOffset(_scrollOffset - notches * Math.Max(24, _bodyFont.Height * 2));
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (IsHandleCreated) RecalculateLayout();
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                ScrollWheel(e.Delta);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                ConfigureTextRendering(e.Graphics);
                e.Graphics.SetClip(ClientRectangle);

                using var textBrush = new SolidBrush(_textColor);
                using var bulletBrush = new SolidBrush(_accentColor);
                using var format = CreateTextFormat();

                var y = 2 - _scrollOffset;
                for (var i = 0; i < _items.Count; i++)
                {
                    var height = i < _itemHeights.Count ? _itemHeights[i] : _bodyFont.Height + 6;
                    if (y + height >= 0 && y <= ClientSize.Height)
                    {
                        var bulletY = y + Math.Max(5F, (_bodyFont.Height - 3F) / 2F);
                        e.Graphics.FillEllipse(bulletBrush, 2F, bulletY, 3F, 3F);
                        var textRect = new RectangleF(14F, y, Math.Max(1F, ClientSize.Width - 22F), height);
                        e.Graphics.DrawString(_items[i], _bodyFont, textBrush, textRect, format);
                    }

                    y += height;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _bodyFont.Dispose();
                base.Dispose(disposing);
            }

            private static void ConfigureTextRendering(Graphics graphics)
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
            }

            private static StringFormat CreateTextFormat()
            {
                return new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.None,
                    FormatFlags = StringFormatFlags.LineLimit,
                };
            }
        }

        private sealed class UpdateGlyph : Control
        {
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color AccentColor { get; set; } = Color.FromArgb(22, 119, 255);

            public UpdateGlyph()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var halo = new SolidBrush(Color.FromArgb(34, AccentColor));
                e.Graphics.FillEllipse(halo, 1, 1, Width - 3, Height - 3);
                using var core = new SolidBrush(Color.FromArgb(58, AccentColor));
                e.Graphics.FillEllipse(core, 7, 7, Width - 15, Height - 15);

                using var pen = new Pen(AccentColor, 2.4F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round,
                };
                var centerX = Width / 2F;
                e.Graphics.DrawLine(pen, centerX, 30, centerX, 16);
                e.Graphics.DrawLine(pen, centerX, 16, centerX - 5, 21);
                e.Graphics.DrawLine(pen, centerX, 16, centerX + 5, 21);
                e.Graphics.DrawLine(pen, centerX - 7, 32, centerX + 7, 32);
            }
        }

        private sealed class ThinScrollBar : Control
        {
            private readonly Color _trackColor;
            private readonly Color _thumbColor;
            private int _thumbTop;
            private int _thumbHeight = 36;
            private int _dragStartY;
            private int _dragStartTop;

            public event Action<int> ScrollRequested;
            public bool IsDragging { get; private set; }
            public int ThumbHeight => _thumbHeight;

            public ThinScrollBar(Color trackColor, Color thumbColor)
            {
                _trackColor = trackColor;
                _thumbColor = thumbColor;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            public void SetThumb(int top, int height)
            {
                _thumbHeight = Math.Max(12, Math.Min(Height, height));
                _thumbTop = Math.Max(0, Math.Min(Math.Max(0, Height - _thumbHeight), top));
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var track = new SolidBrush(_trackColor);
                using var thumb = new SolidBrush(_thumbColor);
                e.Graphics.FillRectangle(track, 2, 0, 4, Height);
                e.Graphics.FillRectangle(thumb, 2, _thumbTop, 4, _thumbHeight);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;

                var thumbRect = new Rectangle(0, _thumbTop, Width, _thumbHeight);
                if (!thumbRect.Contains(e.Location))
                    ScrollRequested?.Invoke(e.Y - _thumbHeight / 2);

                IsDragging = true;
                _dragStartY = Cursor.Position.Y;
                _dragStartTop = _thumbTop;
                Capture = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (!IsDragging || (e.Button & MouseButtons.Left) == 0) return;
                ScrollRequested?.Invoke(_dragStartTop + Cursor.Position.Y - _dragStartY);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                IsDragging = false;
                Capture = false;
            }

            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                base.OnMouseCaptureChanged(e);
                if (!Capture) IsDragging = false;
            }
        }

        private sealed class RoundedPanel : Panel
        {
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public int Radius { get; set; } = 8;
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color FillColor { get; set; } = Color.White;
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public Color BorderColor { get; set; } = Color.FromArgb(214, 221, 232);

            public RoundedPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = CreateRoundRectPath(new Rectangle(1, 1, Width - 3, Height - 3), Radius);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(BorderColor, 1F);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            private static GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                var diameter = radius * 2;
                var arc = new Rectangle(rect.Location, new Size(diameter, diameter));
                path.AddArc(arc, 180, 90);
                arc.X = rect.Right - diameter;
                path.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - diameter;
                path.AddArc(arc, 0, 90);
                arc.X = rect.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
