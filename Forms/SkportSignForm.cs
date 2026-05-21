using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public class SkportSignForm : UserControl
    {
        private readonly SkportService _service = new();
        private readonly AntdUI.Switch _switchEnabled;
        private readonly AntdUI.Switch _switchStartupSign;
        private readonly AntdUI.Input _inputToken;
        private readonly AntdUI.Button _btnToggleToken;
        private readonly NoCaretRichTextBox _logBox;
        private readonly LogScrollBar _logScrollBar;
        private readonly Panel _logPanel;
        private readonly AntdUI.Button _btnPassword;
        private readonly AntdUI.Button _btnSign;
        private readonly System.Windows.Forms.Timer _autoSaveTimer;
        private readonly System.Windows.Forms.Timer _logScrollHideTimer;
        private CancellationTokenSource _cts;
        private bool _tokenVisible;
        private bool _suppressAutoSave;

        private const int EmLineScroll = 0x00B6;
        private const int EmGetLineCount = 0x00BA;
        private const int EmGetFirstVisibleLine = 0x00CE;
        private const int WmVScroll = 0x0115;
        private const int SbBottom = 7;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool HideCaret(IntPtr hWnd);

        public SkportSignForm(Overview overview, bool embedded = false)
        {
            const int formWidth = 1040;
            const int formHeight = 720;
            const int headerHeight = 58;
            const int margin = 28;
            const int contentWidth = formWidth - margin * 2;
            var controlHeight = embedded ? formHeight - headerHeight : formHeight;
            var contentTop = embedded ? 0 : headerHeight;
            var accent = Color.FromArgb(22, 119, 255);
            var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.FromArgb(250, 252, 255);
            var logBack = AntdUI.Config.IsDark ? Color.FromArgb(32, 34, 38) : Color.White;
            var logBorder = AntdUI.Config.IsDark ? Color.FromArgb(66, 72, 82) : Color.FromArgb(214, 221, 232);

            _autoSaveTimer = new System.Windows.Forms.Timer { Interval = 600 };
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                SaveConfig(showMessage: false);
            };

            Font = new Font("Microsoft YaHei UI", 10F);
            Size = new Size(formWidth, controlHeight);
            MinimumSize = Size;
            BackColor = surface;

            if (!embedded)
            {
                var header = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(formWidth, headerHeight),
                    BackColor = surface,
                };
                Controls.Add(header);

                var formTitle = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skport.Title", "SKPORT Sign"),
                    Location = new Point(margin, 8),
                    Size = new Size(760, 38),
                    Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                var btnClose = new AntdUI.Button
                {
                    IconSvg = "CloseOutlined",
                    Location = new Point(formWidth - margin - 36, 10),
                    Size = new Size(36, 36),
                    Ghost = true,
                    Radius = 6,
                    BorderWidth = 0,
                    WaveSize = 0,
                };
                btnClose.Click += (s, e) => FindForm()?.Close();
                header.Controls.Add(formTitle);
                header.Controls.Add(btnClose);
            }

            var content = new Panel
            {
                Location = new Point(0, contentTop),
                Size = new Size(formWidth, formHeight - headerHeight),
                BackColor = surface,
            };
            Controls.Add(content);

            var titleMark = new Panel
            {
                Location = new Point(margin, 18),
                Size = new Size(4, 22),
                BackColor = accent,
            };
            var title = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.ConfigTitle", "SKPORT 签到配置"),
                Location = new Point(margin + 12, 14),
                Size = new Size(200, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };

            var divider = new AntdUI.Divider
            {
                Location = new Point(margin, 54),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var lblSign = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.AutoSign", "启动器运行时签到"),
                Location = new Point(margin + 2, 294),
                Size = new Size(150, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };

            _switchEnabled = new AntdUI.Switch
            {
                Location = new Point(220, 298),
                Size = new Size(44, 22),
            };
            var lblSwitchHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.AutoSignHint", "开启后启动器运行期间每日自动签到一次，并在右下角提示结果"),
                Location = new Point(284, 294),
                Size = new Size(700, 28),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            var lblStartupSign = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.StartupSign", "开机自动签到"),
                Location = new Point(margin + 2, 340),
                Size = new Size(150, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };

            _switchStartupSign = new AntdUI.Switch
            {
                Location = new Point(220, 344),
                Size = new Size(44, 22),
            };
            var lblStartupHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.StartupSignHint", "开机后后台执行一次签到，不显示主窗口，完成后用右下角通知提示"),
                Location = new Point(284, 340),
                Size = new Size(700, 28),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            var lblToken = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.Token", "SKPORT Token"),
                Location = new Point(margin + 2, 82),
                Size = new Size(140, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };
            var lblTokenHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.TokenHint", "使用英文分号 ; 分隔，支持多账号签到"),
                Location = new Point(184, 82),
                Size = new Size(360, 28),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            _inputToken = new AntdUI.Input
            {
                Location = new Point(margin, 116),
                Size = new Size(contentWidth - 48, 48),
                Radius = 6,
                AllowClear = true,
                Multiline = false,
                UseSystemPasswordChar = true,
                WordWrap = false,
                PlaceholderText = AntdUI.Localization.Get("App.Skport.TokenPlaceholder", "请输入 SKPORT Token，多个 Token 用 ; 分隔"),
            };
            _btnToggleToken = new AntdUI.Button
            {
                IconSvg = "EyeInvisibleOutlined",
                Location = new Point(formWidth - margin - 38, 121),
                Size = new Size(38, 38),
                Radius = 6,
                Ghost = true,
                BorderWidth = 0,
                WaveSize = 0,
            };
            _btnToggleToken.Click += (s, e) => ToggleTokenVisible();

            var actions = new FlowLayoutPanel
            {
                Location = new Point(margin, 184),
                Size = new Size(contentWidth, 40),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };
            
            _btnPassword = CreateActionButton(AntdUI.Localization.Get("App.Skport.PasswordLogin", "账号密码登录"), "KeyOutlined", AntdUI.TTypeMini.Default, 132);
            _btnSign = CreateActionButton(AntdUI.Localization.Get("App.Skport.SignNow", "立即签到"), "CheckCircleOutlined", AntdUI.TTypeMini.Success);
            var btnClear = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.Skport.ClearLog", "清空日志"),
                IconSvg = "DeleteOutlined",
                Location = new Point(formWidth - margin - 102, 394),
                Size = new Size(102, 34),
                Radius = 6,
                Type = AntdUI.TTypeMini.Error,
            };

            _btnPassword.Click += async (s, e) => await PasswordTokenAsync();
            _btnSign.Click += async (s, e) => await SignAsync();
            _inputToken.TextChanged += (s, e) => ScheduleAutoSave();
            _switchEnabled.CheckedChanged += (s, e) => ScheduleAutoSave();
            _switchStartupSign.CheckedChanged += (s, e) => ScheduleAutoSave();
            btnClear.Click += (s, e) =>
            {
                SkportLogStore.Clear();
                _logBox.Clear();
            };

            actions.Controls.Add(_btnPassword);
            actions.Controls.Add(_btnSign);

            var autoMark = new Panel
            {
                Location = new Point(margin, 244),
                Size = new Size(4, 22),
                BackColor = accent,
            };
            var lblAuto = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.AutoConfigTitle", "自动签到配置"),
                Location = new Point(margin + 12, 240),
                Size = new Size(180, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };
            var dividerAuto = new AntdUI.Divider
            {
                Location = new Point(margin, 280),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var logMark = new Panel
            {
                Location = new Point(margin, 400),
                Size = new Size(4, 22),
                BackColor = accent,
            };
            var lblLog = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.Log", "日志"),
                Location = new Point(margin + 12, 396),
                Size = new Size(120, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };
            var dividerLog = new AntdUI.Divider
            {
                Location = new Point(margin, 438),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var logPanel = new Panel
            {
                Location = new Point(margin, 458),
                Size = new Size(contentWidth, 176),
                BackColor = Color.Transparent,
            };
            _logPanel = logPanel;
            logPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = CreateRoundRectPath(new Rectangle(0, 0, logPanel.Width - 1, logPanel.Height - 1), 8);
                using var fill = new SolidBrush(logBack);
                using var pen = new Pen(logBorder, 1F);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            };

            _logBox = new NoCaretRichTextBox
            {
                Location = new Point(12, 10),
                Size = new Size(contentWidth - 42, 156),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = logBack,
                ForeColor = AntdUI.Config.IsDark ? Color.WhiteSmoke : Color.FromArgb(36, 36, 36),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ScrollBars = RichTextBoxScrollBars.None,
                DetectUrls = false,
                TabStop = false,
                Cursor = Cursors.Default,
            };
            _logBox.MouseWheel += HandleLogMouseWheel;
            _logBox.VScroll += (s, e) => UpdateLogScrollBar();
            _logBox.TextChanged += (s, e) => UpdateLogScrollBar();
            _logBox.GotFocus += (s, e) => HideLogCaret();
            _logBox.MouseDown += (s, e) => BeginInvoke(new Action(HideLogCaret));
            _logBox.MouseUp += (s, e) => HideLogCaret();
            _logBox.SelectionChanged += (s, e) => HideLogCaret();
            logPanel.MouseEnter += (s, e) =>
            {
                _logBox.Focus();
                RevealLogScrollBar(autoHide: false);
            };
            logPanel.MouseLeave += (s, e) => ScheduleLogScrollBarHide(logPanel);
            _logBox.MouseEnter += (s, e) => RevealLogScrollBar(autoHide: false);
            _logBox.MouseLeave += (s, e) => ScheduleLogScrollBarHide(logPanel);
            logPanel.MouseWheel += HandleLogMouseWheel;
            logPanel.Controls.Add(_logBox);

            var scrollTrackColor = AntdUI.Config.IsDark ? Color.FromArgb(48, 52, 60) : Color.FromArgb(236, 240, 246);
            var scrollThumbColor = AntdUI.Config.IsDark ? Color.FromArgb(118, 128, 146) : Color.FromArgb(156, 166, 182);
            _logScrollBar = new LogScrollBar(scrollTrackColor, scrollThumbColor)
            {
                Location = new Point(contentWidth - 22, 14),
                Size = new Size(8, 148),
                Visible = false,
            };
            _logScrollBar.ScrollRequested += ScrollLogToTrackTop;
            _logScrollBar.MouseWheel += HandleLogMouseWheel;
            _logScrollBar.MouseEnter += (s, e) => RevealLogScrollBar(autoHide: false);
            _logScrollBar.MouseLeave += (s, e) => ScheduleLogScrollBarHide(logPanel);
            _logScrollBar.DragEnded += () =>
            {
                UpdateLogScrollBar();
                ScheduleLogScrollBarHide(logPanel);
            };
            logPanel.Controls.Add(_logScrollBar);
            _logScrollHideTimer = new System.Windows.Forms.Timer { Interval = 900 };
            _logScrollHideTimer.Tick += (s, e) =>
            {
                if (_logScrollBar.IsDisposed) return;
                if (_logScrollBar.IsDragging || IsMouseInside(logPanel)) return;
                _logScrollHideTimer.Stop();
                _logScrollBar.Visible = false;
            };

            content.Controls.Add(titleMark);
            content.Controls.Add(title);
            content.Controls.Add(divider);
            content.Controls.Add(lblToken);
            content.Controls.Add(lblTokenHint);
            content.Controls.Add(_inputToken);
            content.Controls.Add(_btnToggleToken);
            content.Controls.Add(actions);
            content.Controls.Add(autoMark);
            content.Controls.Add(lblAuto);
            content.Controls.Add(dividerAuto);
            content.Controls.Add(lblSign);
            content.Controls.Add(_switchEnabled);
            content.Controls.Add(lblSwitchHint);
            content.Controls.Add(lblStartupSign);
            content.Controls.Add(_switchStartupSign);
            content.Controls.Add(lblStartupHint);
            content.Controls.Add(logMark);
            content.Controls.Add(lblLog);
            content.Controls.Add(btnClear);
            content.Controls.Add(dividerLog);
            content.Controls.Add(logPanel);

            LoadConfig();
            LoadLogHistory();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                if (_autoSaveTimer?.Enabled == true)
                    SaveConfig(showMessage: false);
                _autoSaveTimer?.Stop();
                _autoSaveTimer?.Dispose();
                _logScrollHideTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private static AntdUI.Button CreateActionButton(string text, string iconSvg, AntdUI.TTypeMini type, int width = 112)
        {
            return new AntdUI.Button
            {
                Text = text,
                IconSvg = iconSvg,
                Type = type,
                Size = new Size(width, 36),
                Radius = 6,
                Margin = new Padding(0, 0, 10, 0),
            };
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
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

        private void LoadConfig()
        {
            var cfg = ConfigHelper.Load();
            _suppressAutoSave = true;
            try
            {
                _switchEnabled.Checked = cfg.SkportSignEnabled;
                _switchStartupSign.Checked = cfg.SkportStartupSignEnabled;
                _inputToken.Text = string.Join(";", SkportTokenStorage.GetTokens(cfg));
            }
            finally
            {
                _suppressAutoSave = false;
            }
        }

        private void ScheduleAutoSave()
        {
            if (_suppressAutoSave) return;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void SaveConfig(bool showMessage)
        {
            var cfg = ConfigHelper.Load();
            cfg.SkportSignEnabled = _switchEnabled.Checked;
            cfg.SkportStartupSignEnabled = _switchStartupSign.Checked;
            SkportTokenStorage.SetTokens(cfg, SkportService.SplitTokens(_inputToken.Text));
            ConfigHelper.Save(cfg);
            SkportAutoSignHelper.ApplyStartupRegistration(cfg.SkportStartupSignEnabled);

            if (showMessage)
                AntdUI.Message.success(FindForm(), "SKPORT 配置已保存");
        }

        private void ToggleTokenVisible()
        {
            _tokenVisible = !_tokenVisible;
            _inputToken.UseSystemPasswordChar = !_tokenVisible;
            _btnToggleToken.IconSvg = _tokenVisible ? "EyeOutlined" : "EyeInvisibleOutlined";
        }

        private Task PasswordTokenAsync()
        {
            using var dialog = new SkportPasswordTokenDialog(_service, AppendLog);
            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), dialog)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
            });
            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Token))
                return Task.CompletedTask;

            ApplyToken(dialog.Token);
            AppendLog(AntdUI.Localization.Get("App.Skport.PasswordSuccess", "账号密码登录成功，Token 已保存。"));
            AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skport.TokenSaved", "Token 已保存"));
            return Task.CompletedTask;
        }

        private void ApplyToken(string token)
        {
            AddToken(token);
        }

        private void AddToken(string token)
        {
            var tokens = SkportService.SplitTokens(_inputToken.Text);
            if (!tokens.Contains(token))
                tokens.Add(token);
            _inputToken.Text = string.Join(";", tokens);
            SaveConfig(showMessage: false);
        }

        private async Task SignAsync()
        {
            var tokens = SkportService.SplitTokens(_inputToken.Text);
            if (tokens.Count == 0)
            {
                AntdUI.Message.warn(FindForm(), AntdUI.Localization.Get("App.Skport.TokenRequired", "请先填写 SKPORT Token"));
                return;
            }

            SaveConfig(showMessage: false);
            SetBusy(true);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                AppendLog(AntdUI.Localization.Get("App.Skport.SignStart", "========== SKPORT 签到开始 =========="));
                var progress = new Progress<string>(AppendLog);
                await _service.SignAllAsync(tokens, progress, _cts.Token);
                AppendLog(AntdUI.Localization.Get("App.Skport.SignComplete", "========== SKPORT 签到结束 =========="));
            }
            catch (OperationCanceledException)
            {
                AppendLog(AntdUI.Localization.Get("App.Skport.OperationCanceled", "操作已取消。"));
            }
            catch (Exception ex)
            {
                AppendLog(AntdUI.Localization.Get("App.Skport.SignFailedPrefix", "签到过程发生异常：") + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void AppendLog(string message)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            var timestampMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            _logBox.AppendText(timestampMessage + Environment.NewLine);
            SkportLogStore.Append(message);

            if (!_logScrollBar.IsDragging)
                _logBox.ScrollToCaret();
            UpdateLogScrollBar();
        }

        private void LoadLogHistory()
        {
            var lines = SkportLogStore.LoadRecent();
            if (lines.Length == 0) return;

            _logBox.AppendText(string.Join(Environment.NewLine, lines) + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
            UpdateLogScrollBar();
        }

        private void SetBusy(bool busy)
        {
            _inputToken.Enabled = !busy;
            _btnPassword.Enabled = !busy;
            _btnSign.Enabled = !busy;
            _btnSign.Loading = busy;
            if (busy) _btnSign.Text = AntdUI.Localization.Get("App.Skport.Signing", "正在签到");
            else _btnSign.Text = AntdUI.Localization.Get("App.Skport.SignNow", "立即签到");
        }

        private void HideLogCaret() => HideCaret(_logBox.Handle);

        private void RevealLogScrollBar(bool autoHide)
        {
            UpdateLogScrollBar();
            if (!_logScrollBar.Enabled) return;
            _logScrollBar.Visible = true;
            _logScrollHideTimer.Stop();
            if (autoHide && !IsMouseInside(_logPanel) && !_logScrollBar.IsDragging)
                _logScrollHideTimer.Start();
        }

        private void ScheduleLogScrollBarHide(Panel panel)
        {
            if (_logScrollBar.IsDragging || IsMouseInside(panel)) return;
            _logScrollHideTimer.Stop();
            _logScrollHideTimer.Start();
        }

        private bool IsMouseInside(Control control)
        {
            var p = control.PointToClient(MousePosition);
            return control.ClientRectangle.Contains(p);
        }

        private void HandleLogMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                int lines = -(e.Delta / 120) * 3;
                SendMessage(_logBox.Handle, EmLineScroll, IntPtr.Zero, (IntPtr)lines);
                UpdateLogScrollBar();
                RevealLogScrollBar(autoHide: true);
                HideLogCaret();
            }
        }

        private void ScrollLogToTrackTop(int trackTop)
        {
            int totalLines = (int)SendMessage(_logBox.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero);
            if (totalLines <= 1) return;

            int firstLine = (int)SendMessage(_logBox.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero);
            int trackAreaHeight = _logScrollBar.Height - _logScrollBar.ThumbHeight;
            if (trackAreaHeight <= 0) return;

            float ratio = Math.Max(0, Math.Min(1, (float)trackTop / trackAreaHeight));
            int targetLine = (int)(ratio * (totalLines - 1));

            SendMessage(_logBox.Handle, EmLineScroll, IntPtr.Zero, (IntPtr)(targetLine - firstLine));
            HideLogCaret();
        }

        private void UpdateLogScrollBar()
        {
            if (_logScrollBar.IsDragging || _logBox.IsDisposed) return;

            int totalLines = (int)SendMessage(_logBox.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero);
            int firstLine = (int)SendMessage(_logBox.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero);

            int visibleLines = 1;
            using (var g = _logBox.CreateGraphics())
            {
                var fontHeight = _logBox.Font.GetHeight(g);
                if (fontHeight > 0)
                    visibleLines = (int)(_logBox.ClientSize.Height / fontHeight);
            }

            if (totalLines <= visibleLines)
            {
                _logScrollBar.Enabled = false;
                _logScrollBar.Visible = false;
                return;
            }

            _logScrollBar.Enabled = true;
            int maxFirstLine = totalLines - visibleLines;
            if (maxFirstLine < 0) maxFirstLine = 0;

            float progress = maxFirstLine == 0 ? 0 : (float)firstLine / maxFirstLine;
            float viewRatio = (float)visibleLines / totalLines;

            int thumbHeight = Math.Max(30, (int)(_logScrollBar.Height * viewRatio));
            _logScrollBar.UpdateThumb(thumbHeight, progress);
        }

        private sealed class LogScrollBar : Control
        {
            private readonly Color _trackColor;
            private readonly Color _thumbColor;
            private int _thumbTop;
            private int _thumbHeight;
            private int _dragStartY;
            private int _dragStartTop;

            public event Action<int> ScrollRequested;
            public event Action DragEnded;

            public bool IsDragging { get; private set; }
            public int ThumbHeight => _thumbHeight;

            public LogScrollBar(Color trackColor, Color thumbColor)
            {
                _trackColor = trackColor;
                _thumbColor = thumbColor;
                _thumbHeight = 42;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            public void UpdateThumb(int height, float progress)
            {
                int maxTop = Height - height;
                int top = (int)(maxTop * progress);
                SetThumb(top, height);
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

                using (var trackPath = CreateRoundRectPath(new Rectangle(2, 0, 4, Height), 2))
                using (var trackFill = new SolidBrush(_trackColor))
                    e.Graphics.FillPath(trackFill, trackPath);

                using var thumbPath = CreateRoundRectPath(new Rectangle(2, _thumbTop, 4, _thumbHeight), 2);
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

        private sealed class NoCaretRichTextBox : RichTextBox
        {
            private const int WmSetFocus = 0x0007;
            private const int WmLButtonDown = 0x0201;
            private const int WmLButtonUp = 0x0202;

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WmSetFocus || m.Msg == WmLButtonDown || m.Msg == WmLButtonUp)
                    HideCaret(Handle);
            }

            protected override void OnGotFocus(EventArgs e)
            {
                base.OnGotFocus(e);
                HideCaret(Handle);
            }

            protected override void OnSelectionChanged(EventArgs e)
            {
                base.OnSelectionChanged(e);
                if (IsHandleCreated) HideCaret(Handle);
            }
        }
    }

    public class SkportPasswordTokenDialog : UserControl
    {
        private readonly SkportService _service;
        private readonly Action<string> _logAction;
        private readonly AntdUI.Input _inputAccount;
        private readonly AntdUI.Input _inputPassword;
        private readonly AntdUI.Button _btnLogin;
        private bool _loggingIn;

        public string Token { get; private set; } = "";

        public SkportPasswordTokenDialog(SkportService service, Action<string> logAction)
        {
            _service = service;
            _logAction = logAction;
            const int left = 28;
            const int contentWidth = 424;
            var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
            var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.White;
            var accent = Color.FromArgb(22, 119, 255);

            Size = new Size(480, 300);
            BackColor = surface;
            Font = new Font("Microsoft YaHei UI", 10F);

            var title = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.PasswordLogin", "账号密码登录"),
                Location = new Point(left, 12),
                Size = new Size(320, 30),
                Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = normalText,
            };

            var subtitle = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.PasswordSubtitle", "使用鹰角网络账号登录，成功后会自动保存 Token。"),
                Location = new Point(left, 42),
                Size = new Size(390, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };
            var btnClose = new AntdUI.Button
            {
                IconSvg = "CloseOutlined",
                Location = new Point(left + contentWidth - 34, 10),
                Size = new Size(34, 34),
                Ghost = true,
                Radius = 6,
                BorderWidth = 0,
                WaveSize = 0,
            };
            btnClose.Click += (s, e) => FindForm()?.Close();

            var topLine = new Panel
            {
                Location = new Point(left, 78),
                Size = new Size(contentWidth, 1),
                BackColor = AntdUI.Config.IsDark ? Color.FromArgb(52, 56, 64) : Color.FromArgb(232, 236, 242),
            };
            var accountMark = new Panel
            {
                Location = new Point(left, 100),
                Size = new Size(3, 18),
                BackColor = accent,
            };
            var lblAccount = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.Account", "账号"),
                Location = new Point(left + 12, 94),
                Size = new Size(120, 28),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = normalText,
            };

            _inputAccount = new AntdUI.Input
            {
                PlaceholderText = AntdUI.Localization.Get("App.Skport.AccountPlaceholder", "请输入鹰角网络手机号 / 邮箱"),
                Location = new Point(left, 130),
                Size = new Size(contentWidth, 42),
                Radius = 6,
                AllowClear = true,
            };

            var passwordMark = new Panel
            {
                Location = new Point(left, 194),
                Size = new Size(3, 18),
                BackColor = accent,
            };
            var lblPassword = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skport.Password", "密码"),
                Location = new Point(left + 12, 188),
                Size = new Size(120, 28),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = normalText,
            };

            _inputPassword = new AntdUI.Input
            {
                PlaceholderText = AntdUI.Localization.Get("App.Skport.PasswordPlaceholder", "请输入密码"),
                Location = new Point(left, 224),
                Size = new Size(288, 42),
                Radius = 6,
                AllowClear = true,
                UseSystemPasswordChar = true,
            };

            _btnLogin = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.Skport.LoginAndGetToken", "登录并获取 Token"),
                IconSvg = "LoginOutlined",
                Type = AntdUI.TTypeMini.Success,
                Location = new Point(left + 304, 224),
                Size = new Size(120, 42),
                Radius = 6,
            };
            _btnLogin.Click += async (s, e) => await LoginAsync();
            _inputPassword.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await LoginAsync();
            };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(btnClose);
            Controls.Add(topLine);
            Controls.Add(accountMark);
            Controls.Add(lblAccount);
            Controls.Add(_inputAccount);
            Controls.Add(passwordMark);
            Controls.Add(lblPassword);
            Controls.Add(_inputPassword);
            Controls.Add(_btnLogin);
        }

        private async Task LoginAsync()
        {
            if (_loggingIn) return;

            var account = _inputAccount.Text.Trim();
            var pwd = _inputPassword.Text;

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(pwd))
            {
                AntdUI.Message.warn(FindForm(), AntdUI.Localization.Get("App.Skport.InputRequired", "请填写账号和密码"));
                return;
            }

            _loggingIn = true;
            _btnLogin.Loading = true;
            _btnLogin.Text = AntdUI.Localization.Get("App.Skport.LoggingIn", "登录中...");
            _btnLogin.Enabled = false;
            _inputAccount.ReadOnly = true;
            _inputPassword.ReadOnly = true;

            try
            {
                _logAction(AntdUI.Localization.Get("App.Skport.PasswordLoginStart", "开始使用账号密码登录..."));
                Token = await _service.LoginByPasswordAsync(account, pwd);
                var form = FindForm();
                if (form != null)
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                _logAction(AntdUI.Localization.Get("App.Skport.PasswordFailedPrefix", "账号密码登录获取 Token 失败：") + ex.Message);
                AntdUI.Message.error(FindForm(), ex.Message);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _loggingIn = false;
                    _btnLogin.Loading = false;
                    _btnLogin.Text = AntdUI.Localization.Get("App.Skport.LoginAndGetToken", "登录并获取 Token");
                    _btnLogin.Enabled = true;
                    _inputAccount.ReadOnly = false;
                    _inputPassword.ReadOnly = false;
                }
            }
        }
    }
}
