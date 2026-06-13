using System;
using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    internal sealed class StartupAnnouncementDialog : UserControl
    {
        private const int EmLineScroll = 0x00B6;
        private const int EmGetLineCount = 0x00BA;
        private const int EmGetFirstVisibleLine = 0x00CE;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly RoundedPanel _notesFrame;
        private readonly RichTextBox _notes;
        private readonly ThinScrollBar _scrollBar;
        private readonly AntdUI.Button _okButton;
        private readonly Color _normalText;
        private readonly Color _subtleText;
        private readonly Color _accentText;
        private readonly Timer _scrollHideTimer;
        private bool _allowClose;
        private Form _hostForm;

        public event EventHandler ReadCompleted;

        private static string L(string key, string fallback) =>
            AntdUI.Localization.Get(key, fallback);

        public StartupAnnouncementDialog(string version)
        {
            Size = new Size(600, 500);
            MinimumSize = Size;
            BackColor = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkSurface : Color.White;

            _normalText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            _subtleText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForegroundSecondary : Color.FromArgb(108, 116, 128);
            _accentText = AntdUI.Style.Db.Primary;

            var bodyBack = BackColor;
            var border = AntdUI.Config.IsDark ? Color.FromArgb(66, 72, 82) : Color.FromArgb(214, 221, 232);

            _notesFrame = new RoundedPanel
            {
                Location = new Point(20, 40),
                Size = new Size(560, 384),
                Padding = new Padding(12, 12, 22, 10),
                FillColor = bodyBack,
                BorderColor = border,
                Radius = 7,
            };

            _notes = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.None,
                DetectUrls = false,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                BackColor = bodyBack,
                ForeColor = _normalText,
                TabStop = true,
            };
            RenderAnnouncement(version);
            _notes.MouseWheel += HandleNotesMouseWheel;
            _notes.MouseEnter += (s, e) => _notes.Focus();
            _notes.KeyUp += (s, e) => RevealScrollBar(autoHide: true);

            _scrollBar = new ThinScrollBar(
                AntdUI.Config.IsDark ? Color.FromArgb(52, 58, 68) : Color.FromArgb(236, 240, 246),
                AntdUI.Config.IsDark ? Color.FromArgb(128, 138, 154) : Color.FromArgb(156, 166, 182))
            {
                Visible = false,
                BackColor = bodyBack,
            };
            _scrollBar.ScrollRequested += ScrollNotesToTrackTop;
            _scrollBar.MouseWheel += HandleNotesMouseWheel;
            _scrollBar.DragEnded += () => RevealScrollBar(autoHide: true);
            _notesFrame.Resize += (s, e) => LayoutScrollBar();
            _notesFrame.MouseWheel += HandleNotesMouseWheel;
            _notesFrame.MouseEnter += (s, e) => _notes.Focus();

            _scrollHideTimer = new Timer { Interval = 900 };
            _scrollHideTimer.Tick += (s, e) =>
            {
                if (_scrollBar.IsDragging) return;
                _scrollHideTimer.Stop();
                _scrollBar.Visible = false;
            };

            _okButton = new AntdUI.Button
            {
                Text = L("App.StartupAnnouncement.Button", "已阅读"),
                Type = AntdUI.TTypeMini.Primary,
                Radius = 6,
                Size = new Size(118, 36),
                Location = new Point(462, 434),
                Enabled = false,
                TabStop = false,
                WaveSize = 0,
            };
            _okButton.Click += (s, e) => Complete();

            _notesFrame.Controls.Add(_notes);
            _notesFrame.Controls.Add(_scrollBar);
            LayoutScrollBar();
            Controls.Add(_notesFrame);
            Controls.Add(_okButton);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SyncHostBackground();
            AttachHostCloseGuard();
            BeginInvoke(new Action(UpdateReadState));
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            SyncHostBackground();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _hostForm != null)
            {
                _hostForm.FormClosing -= HostForm_FormClosing;
                _hostForm = null;
            }
            if (disposing)
                _scrollHideTimer?.Dispose();

            base.Dispose(disposing);
        }

        private void AttachHostCloseGuard()
        {
            _hostForm = FindForm();
            if (_hostForm != null)
                _hostForm.FormClosing += HostForm_FormClosing;
        }

        private void SyncHostBackground()
        {
            var hostBack = Parent?.BackColor ?? BackColor;
            if (hostBack == Color.Empty || hostBack == Color.Transparent) return;

            BackColor = hostBack;
            _notesFrame.FillColor = hostBack;
            _notes.BackColor = hostBack;
            if (_scrollBar != null)
                _scrollBar.BackColor = hostBack;
            ResetTextBackground(hostBack);
            _notesFrame.Invalidate();
        }

        private void ResetTextBackground(Color backColor)
        {
            if (_notes == null || _notes.IsDisposed || _notes.TextLength == 0) return;

            var selectionStart = _notes.SelectionStart;
            var selectionLength = _notes.SelectionLength;
            _notes.SelectAll();
            _notes.SelectionBackColor = backColor;
            _notes.Select(Math.Min(selectionStart, _notes.TextLength), Math.Min(selectionLength, _notes.TextLength - Math.Min(selectionStart, _notes.TextLength)));
            if (_notes.SelectionLength > 0)
                _notes.SelectionLength = 0;
        }

        private void HostForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose) return;

            UpdateReadState();
            if (!_okButton.Enabled)
                e.Cancel = true;
        }

        private void Complete()
        {
            UpdateReadState();
            if (!_okButton.Enabled) return;

            _allowClose = true;
            ReadCompleted?.Invoke(this, EventArgs.Empty);
            if (FindForm() is Form form)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        }

        private void UpdateReadState()
        {
            var canClose = HasReadToBottom();
            _okButton.Enabled = canClose;
        }

        private void RevealScrollBar(bool autoHide)
        {
            UpdateReadState();
            UpdateScrollBar();
            if (!NotesNeedScroll()) return;

            _scrollBar.Visible = true;
            _scrollBar.BringToFront();
            if (!autoHide) return;

            _scrollHideTimer.Stop();
            _scrollHideTimer.Start();
        }

        private void LayoutScrollBar()
        {
            if (_scrollBar == null) return;

            _scrollBar.Location = new Point(Math.Max(0, _notesFrame.ClientSize.Width - 18), 14);
            _scrollBar.Size = new Size(8, Math.Max(24, _notesFrame.ClientSize.Height - 28));
            _scrollBar.BringToFront();
            UpdateScrollBar();
        }

        private void UpdateScrollBar()
        {
            if (_scrollBar == null || _scrollBar.IsDisposed || _scrollBar.IsDragging) return;
            if (!TryGetScrollMetrics(out var totalLines, out var firstVisibleLine, out var visibleLines))
            {
                _scrollBar.Visible = false;
                return;
            }

            var trackHeight = _scrollBar.Height;
            var thumbHeight = Math.Max(18, trackHeight * visibleLines / totalLines);
            var maxFirstLine = Math.Max(1, totalLines - visibleLines);
            var thumbTop = (trackHeight - thumbHeight) * Math.Min(firstVisibleLine, maxFirstLine) / maxFirstLine;
            _scrollBar.SetThumb(thumbTop, thumbHeight);
        }

        private bool NotesNeedScroll() =>
            TryGetScrollMetrics(out _, out _, out _);

        private bool TryGetScrollMetrics(out int totalLines, out int firstVisibleLine, out int visibleLines)
        {
            totalLines = (int)SendMessage(_notes.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero);
            firstVisibleLine = (int)SendMessage(_notes.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero);
            visibleLines = Math.Max(1, _notes.ClientSize.Height / Math.Max(1, _notes.Font.Height));
            return totalLines > visibleLines;
        }

        private void ScrollNotesToTrackTop(int requestedTop)
        {
            if (!TryGetScrollMetrics(out var totalLines, out var firstVisibleLine, out var visibleLines))
            {
                UpdateScrollBar();
                return;
            }

            var maxThumbTop = Math.Max(1, _scrollBar.Height - _scrollBar.ThumbHeight);
            var maxFirstLine = Math.Max(1, totalLines - visibleLines);
            var targetFirstLine = Math.Max(0, Math.Min(maxFirstLine, requestedTop * maxFirstLine / maxThumbTop));
            SendMessage(_notes.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(targetFirstLine - firstVisibleLine));
            RevealScrollBar(autoHide: false);
        }

        private void HandleNotesMouseWheel(object sender, MouseEventArgs e)
        {
            var notches = e.Delta / 120;
            if (notches == 0) return;

            SendMessage(_notes.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(-notches * 3));
            RevealScrollBar(autoHide: true);
        }

        private bool HasReadToBottom()
        {
            if (_notes.TextLength == 0) return true;
            if (_notes.ClientSize.Height <= 0) return false;

            var lastCharIndex = Math.Max(0, _notes.TextLength - 1);
            var lastCharBottom = _notes.GetPositionFromCharIndex(lastCharIndex).Y + _notes.Font.Height;
            return lastCharBottom <= _notes.ClientSize.Height + _notes.Font.Height;
        }

        private void RenderAnnouncement(string version)
        {
            _notes.Clear();
            _notes.SelectionIndent = 10;
            _notes.SelectionRightIndent = 10;

            AppendLine(StartupAnnouncementContent.GetVersionTitle(version), 18F, FontStyle.Bold, _normalText);
            AppendBlank();
            AppendLine("Highlights", 14F, FontStyle.Bold, _normalText);
            AppendBlank(4);
            foreach (var section in StartupAnnouncementContent.Highlights)
                AppendSection(section.Title, section.Body);

            AppendSeparator();
            AppendLine(StartupAnnouncementContent.DetailIntro, 10.5F, FontStyle.Regular, _normalText);
            AppendDetailHeader(StartupAnnouncementContent.GetDetailHeader(version));
            AppendLine(StartupAnnouncementContent.FullChangelogTitle, 10.5F, FontStyle.Bold, _normalText);
            foreach (var item in StartupAnnouncementContent.ChangelogItems)
                AppendLine("• " + item, 10F, FontStyle.Regular, _normalText);
            AppendBlank();
            AppendLine(StartupAnnouncementContent.Tip, 10F, FontStyle.Regular, _accentText);
            _notes.Select(0, 0);
        }

        private void PopulateAnnouncement(string version)
        {
            RenderAnnouncement(version);
        }

#if false
            _notes.Clear();
            _notes.SelectionIndent = 10;
            _notes.SelectionRightIndent = 10;

            AppendLine($"v{NormalizeVersion(version)}", 18F, FontStyle.Bold, _normalText);
            AppendBlank();
            AppendLine("Highlights", 14F, FontStyle.Bold, _normalText);
            AppendBlank(4);
            AppendSection("终末地更新器升级",
                "终末地更新插件切换到 Hi3Helper.Plugin.Hypergryph 1.0.3，适配新版增量更新流程，减少异常增量包反复更新的风险。");
            AppendSection("游戏更新检查开关",
                "设置页新增“检查游戏更新”开关。关闭后启动器不会主动检查游戏更新，也不会因为游戏版本状态反复提示更新。");
            AppendSection("插件依赖与构建修正",
                "修正 Hypergryph.Core、Endfield、Arknights、SevenZipExtractor 和 SharpHDiffPatch.Core 的项目引用与输出，确保调试构建可以生成所需 DLL。");
            AppendSection("启动公告",
                "重要变更会在启动时以版本信息弹窗展示。用户需要滚动阅读到末尾后，才可以关闭公告。");

            AppendSeparator();
            AppendLine("以下是详细内容：", 10.5F, FontStyle.Regular, _normalText);
            AppendDetailHeader($"v{NormalizeVersion(version)}  (2026-06-05)");
            AppendLine("Full Changelog:", 10.5F, FontStyle.Bold, _normalText);
            AppendLine("• Hi3Helper.Plugin.Hypergryph 更新到 1.0.3", 10F, FontStyle.Regular, _normalText);
            AppendLine("• 新增设置项：检1111查游戏更新", 10F, FontStyle.Regular, _normalText);
            AppendLine("• 启动公告支持强制阅读完毕后关闭", 10F, FontStyle.Regular, _normalText);
            AppendLine("• 修复插件项目引用后默认 Debug 输出缺失的问题", 10F, FontStyle.Regular, _normalText);
            AppendBlank();
            AppendLine("提示：如果不想让启动器主动检查游戏更新，可以到 设置 > 软件 > 检查游戏更新 关闭该开关。", 10F, FontStyle.Regular, _accentText);
            _notes.Select(0, 0);
        }

#endif

        private void AppendSection(string title, string body)
        {
            AppendLine(title, 11.5F, FontStyle.Bold, _normalText);
            AppendLine(body, 10F, FontStyle.Regular, _normalText);
            AppendBlank(3);
        }

        private void AppendDetailHeader(string text)
        {
            var back = AntdUI.Config.IsDark ? Color.FromArgb(50, 51, 56) : Color.FromArgb(238, 241, 246);
            AppendLine(text, 10.5F, FontStyle.Bold, _normalText, back);
        }

        private void AppendSeparator()
        {
            AppendBlank(2);
            AppendLine(new string('─', 70), 8F, FontStyle.Regular, _subtleText);
            AppendBlank(2);
        }

        private void AppendBlank(int pixels = 8)
        {
            _notes.SelectionFont = new Font("Microsoft YaHei UI", Math.Max(1F, pixels / 2F), FontStyle.Regular);
            _notes.SelectionColor = _subtleText;
            _notes.AppendText(Environment.NewLine);
        }

        private void AppendLine(string text, float size, FontStyle style, Color color, Color? backColor = null)
        {
            _notes.SelectionFont = new Font("Microsoft YaHei UI", size, style);
            _notes.SelectionColor = color;
            _notes.SelectionBackColor = backColor ?? _notes.BackColor;
            _notes.AppendText(text + Environment.NewLine);
            _notes.SelectionBackColor = _notes.BackColor;
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            return version.Trim().TrimStart('v', 'V');
        }

        private sealed class ThinScrollBar : Control
        {
            private readonly Color _trackColor;
            private readonly Color _thumbColor;
            private int _thumbTop;
            private int _thumbHeight = 42;
            private int _dragStartY;
            private int _dragStartTop;

            public event Action<int> ScrollRequested;
            public event Action DragEnded;
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
                var clampedHeight = Math.Max(12, Math.Min(Height, height));
                var clampedTop = Math.Max(0, Math.Min(Math.Max(0, Height - clampedHeight), top));
                if (_thumbTop == clampedTop && _thumbHeight == clampedHeight) return;

                _thumbTop = clampedTop;
                _thumbHeight = clampedHeight;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var trackPath = RoundedPanel.CreateRoundRectPath(new Rectangle(2, 0, 4, Height), 2))
                using (var trackFill = new SolidBrush(_trackColor))
                    e.Graphics.FillPath(trackFill, trackPath);

                using var thumbPath = RoundedPanel.CreateRoundRectPath(new Rectangle(2, _thumbTop, 4, _thumbHeight), 2);
                using var thumbFill = new SolidBrush(_thumbColor);
                e.Graphics.FillPath(thumbFill, thumbPath);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
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
                if (!IsDragging) return;
                ScrollRequested?.Invoke(_dragStartTop + Cursor.Position.Y - _dragStartY);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                IsDragging = false;
                Capture = false;
                DragEnded?.Invoke();
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
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = CreateRoundRectPath(new Rectangle(1, 1, Width - 3, Height - 3), Radius);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(BorderColor, 1F);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            public static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
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
