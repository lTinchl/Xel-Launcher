using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public class SkylandSignForm : UserControl
    {
        private readonly SkylandService _service = new();
        private readonly AntdUI.Switch _switchEnabled;
        private readonly AntdUI.Switch _switchStartupSign;
        private readonly AntdUI.Input _inputToken;
        private readonly AntdUI.Button _btnToggleToken;
        private readonly NoCaretRichTextBox _logBox;
        private readonly LogScrollBar _logScrollBar;
        private readonly Panel _logPanel;
        private readonly AntdUI.Button _btnScan;
        private readonly AntdUI.Button _btnSms;
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

        public SkylandSignForm(Overview overview, bool embedded = false)
        {
            const int formWidth = 920;
            const int formHeight = 640;
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
                    Text = AntdUI.Localization.Get("App.Skyland.Title", "Skyland Sign"),
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
                Text = AntdUI.Localization.Get("App.Skyland.ConfigTitle", "森空岛配置"),
                Location = new Point(margin + 12, 14),
                Size = new Size(200, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };
            var btnDoc = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.Skyland.Document", "文档"),
                IconSvg = "BookOutlined",
                Location = new Point(formWidth - margin - 76, 13),
                Size = new Size(76, 32),
                Ghost = true,
                Radius = 6,
            };
            btnDoc.Click += (s, e) => TabHeaderForm.Open("https://github.com/lTinchl/skyland-auto-sign");

            var divider = new AntdUI.Divider
            {
                Location = new Point(margin, 54),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var lblSign = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.AutoSign", "启动器运行时签到"),
                Location = new Point(margin + 2, 276),
                Size = new Size(150, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };

            _switchEnabled = new AntdUI.Switch
            {
                Location = new Point(220, 280),
                Size = new Size(44, 22),
            };
            var lblSwitchHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.AutoSignHint", "开启后启动器运行期间每日自动签到一次，并在右下角提示结果"),
                Location = new Point(284, 276),
                Size = new Size(contentWidth - 256, 28),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            var lblStartupSign = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.StartupSign", "开机自动签到"),
                Location = new Point(margin + 2, 318),
                Size = new Size(150, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };

            _switchStartupSign = new AntdUI.Switch
            {
                Location = new Point(220, 322),
                Size = new Size(44, 22),
            };
            var lblStartupHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.StartupSignHint", "开机后后台执行一次签到，不显示主窗口，完成后用右下角通知提示"),
                Location = new Point(284, 318),
                Size = new Size(contentWidth - 256, 28),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
            };

            var lblToken = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.Token", "森空岛Token"),
                Location = new Point(margin + 2, 82),
                Size = new Size(140, 28),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = normalText,
            };
            var lblTokenHint = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.TokenHint", "使用英文分号 ; 分隔，支持多账号签到"),
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
                PlaceholderText = AntdUI.Localization.Get("App.Skyland.TokenPlaceholder", "请输入森空岛 Token，多个 Token 用 ; 分隔"),
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
                Location = new Point(margin, 178),
                Size = new Size(contentWidth, 40),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };
            _btnScan = CreateActionButton(AntdUI.Localization.Get("App.Skyland.ScanLogin", "扫码登录"), "QrcodeOutlined", AntdUI.TTypeMini.Primary);
            _btnSms = CreateActionButton(AntdUI.Localization.Get("App.Skyland.SmsLogin", "手机验证码登录"), "MessageOutlined", AntdUI.TTypeMini.Default, 138);
            _btnPassword = CreateActionButton(AntdUI.Localization.Get("App.Skyland.PasswordLogin", "账号密码登录"), "KeyOutlined", AntdUI.TTypeMini.Default, 132);
            _btnSign = CreateActionButton(AntdUI.Localization.Get("App.Skyland.SignNow", "立即签到"), "CheckCircleOutlined", AntdUI.TTypeMini.Success);
            var btnClear = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.Skyland.ClearLog", "清空日志"),
                IconSvg = "DeleteOutlined",
                Location = new Point(formWidth - margin - 102, 360),
                Size = new Size(102, 34),
                Radius = 6,
                Type = AntdUI.TTypeMini.Error,
            };

            _btnScan.Click += async (s, e) => await ScanTokenAsync();
            _btnSms.Click += async (s, e) => await SmsTokenAsync();
            _btnPassword.Click += async (s, e) => await PasswordTokenAsync();
            _btnSign.Click += async (s, e) => await SignAsync();
            _inputToken.TextChanged += (s, e) => ScheduleAutoSave();
            _switchEnabled.CheckedChanged += (s, e) => ScheduleAutoSave();
            _switchStartupSign.CheckedChanged += (s, e) => ScheduleAutoSave();
            btnClear.Click += (s, e) =>
            {
                SkylandLogStore.Clear();
                _logBox.Clear();
            };

            actions.Controls.Add(_btnScan);
            actions.Controls.Add(_btnSms);
            actions.Controls.Add(_btnPassword);
            actions.Controls.Add(_btnSign);

            var autoMark = new Panel
            {
                Location = new Point(margin, 226),
                Size = new Size(4, 22),
                BackColor = accent,
            };
            var lblAuto = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.AutoConfigTitle", "自动签到配置"),
                Location = new Point(margin + 12, 222),
                Size = new Size(180, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };
            var dividerAuto = new AntdUI.Divider
            {
                Location = new Point(margin, 262),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var logMark = new Panel
            {
                Location = new Point(margin, 366),
                Size = new Size(4, 22),
                BackColor = accent,
            };
            var lblLog = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.Log", "日志"),
                Location = new Point(margin + 12, 362),
                Size = new Size(120, 32),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = normalText,
            };
            var dividerLog = new AntdUI.Divider
            {
                Location = new Point(margin, 398),
                Size = new Size(contentWidth, 1),
                Thickness = 1F,
            };

            var logPanel = new Panel
            {
                Location = new Point(margin, 416),
                Size = new Size(contentWidth, 148),
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
                Size = new Size(contentWidth - 42, 128),
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
                Size = new Size(8, 120),
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
            content.Controls.Add(btnDoc);
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
                _switchEnabled.Checked = cfg.SkylandSignEnabled;
                _switchStartupSign.Checked = cfg.SkylandStartupSignEnabled;
                _inputToken.Text = string.Join(";", SkylandTokenStorage.GetTokens(cfg));
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
            cfg.SkylandSignEnabled = _switchEnabled.Checked;
            cfg.SkylandStartupSignEnabled = _switchStartupSign.Checked;
            SkylandTokenStorage.SetTokens(cfg, SkylandService.SplitTokens(_inputToken.Text));
            ConfigHelper.Save(cfg);
            SkylandAutoSignHelper.ApplyStartupRegistration(cfg.SkylandStartupSignEnabled);

            if (showMessage)
                AntdUI.Message.success(FindForm(), "森空岛配置已保存");
        }

        private void ToggleTokenVisible()
        {
            _tokenVisible = !_tokenVisible;
            _inputToken.UseSystemPasswordChar = !_tokenVisible;
            _btnToggleToken.IconSvg = _tokenVisible ? "EyeOutlined" : "EyeInvisibleOutlined";
        }

        private async Task ScanTokenAsync()
        {
            SetBusy(true);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.ScanCreating", "创建森空岛扫码登录..."));
                var scan = await _service.CreateScanLoginAsync(_cts.Token);
                if (!ShowQrDialog(scan.ScanUrl)) return;

                AppendLog(AntdUI.Localization.Get("App.Skyland.ScanChecking", "开始检测扫码状态..."));
                var progress = new Progress<string>(AppendLog);
                var token = await _service.WaitForScanTokenAsync(scan.ScanId, progress, TimeSpan.FromMinutes(3), _cts.Token);

                AddToken(token);
                AppendLog(AntdUI.Localization.Get("App.Skyland.ScanSuccess", "扫码登录成功，Token 已保存。"));
                AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skyland.ScanSuccessToast", "扫码登录成功，Token 已保存"));
            }
            catch (OperationCanceledException)
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.OperationCanceled", "操作已取消。"));
            }
            catch (Exception ex)
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.ScanFailedPrefix", "扫码获取 Token 失败：") + ex.Message);
                AntdUI.Message.error(FindForm(), ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SmsTokenAsync()
        {
            using var dialog = new SkylandSmsTokenDialog(_service, AppendLog);
            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), dialog)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
            });
            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Token)) return;

            ApplyToken(dialog.Token);
            AppendLog(AntdUI.Localization.Get("App.Skyland.SmsSuccess", "手机号验证码登录成功，Token 已保存。"));
            AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skyland.TokenSaved", "Token 已保存"));
        }

        private Task PasswordTokenAsync()
        {
            using var dialog = new SkylandPasswordTokenDialog(_service, AppendLog);
            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), dialog)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
            });
            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Token))
                return Task.CompletedTask;

            ApplyToken(dialog.Token);
            AppendLog(AntdUI.Localization.Get("App.Skyland.PasswordSuccess", "账号密码登录成功，Token 已保存。"));
            AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skyland.TokenSaved", "Token 已保存"));
            return Task.CompletedTask;
        }

        private void ApplyToken(string token)
        {
            AddToken(token);
        }

        private void AddToken(string token)
        {
            var tokens = SkylandService.SplitTokens(_inputToken.Text);
            if (!tokens.Contains(token))
                tokens.Add(token);
            _inputToken.Text = string.Join(";", tokens);
            SaveConfig(showMessage: false);
        }

        private async Task SignAsync()
        {
            var tokens = SkylandService.SplitTokens(_inputToken.Text);
            if (tokens.Count == 0)
            {
                AntdUI.Message.warn(FindForm(), AntdUI.Localization.Get("App.Skyland.TokenRequired", "请先填写森空岛 Token"));
                return;
            }

            SaveConfig(showMessage: false);
            SetBusy(true);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.SignStart", "========== 森空岛签到开始 =========="));
                var progress = new Progress<string>(AppendLog);
                await _service.SignAllAsync(tokens, progress, _cts.Token);
                AppendLog(AntdUI.Localization.Get("App.Skyland.SignEnd", "========== 森空岛签到结束 =========="));
                AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skyland.SignComplete", "森空岛签到完成"));
            }
            catch (OperationCanceledException)
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.SignCanceled", "签到已取消。"));
            }
            catch (Exception ex)
            {
                AppendLog(AntdUI.Localization.Get("App.Skyland.SignFailedPrefix", "签到失败：") + ex.Message);
                AntdUI.Message.error(FindForm(), ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private bool ShowQrDialog(string scanUrl)
        {
            var qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=" + Uri.EscapeDataString(scanUrl);

            var panel = new Panel { Size = new Size(360, 420), Padding = new Padding(16) };
            var picture = new PictureBox
            {
                Size = new Size(320, 320),
                Location = new Point(20, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
            };
            var hint = new Label
            {
                Text = AntdUI.Localization.Get("App.Skyland.QrHint", "请使用森空岛 App 扫码，并在 App 内确认登录后点击确定。"),
                Location = new Point(16, 344),
                Size = new Size(328, 46),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AntdUI.Config.IsDark ? Color.WhiteSmoke : Color.FromArgb(36, 36, 36),
            };
            var raw = new TextBox
            {
                Text = scanUrl,
                Location = new Point(16, 392),
                Size = new Size(328, 24),
                ReadOnly = true,
            };

            panel.Controls.Add(picture);
            panel.Controls.Add(hint);
            panel.Controls.Add(raw);
            picture.LoadAsync(qrUrl);

            return AntdUI.Modal.open(new AntdUI.Modal.Config(FindForm(), "森空岛扫码登录", panel)
            {
                OkText = "确定",
                CancelText = "取消",
            }) == DialogResult.OK;
        }

        private void SetBusy(bool busy)
        {
            if (IsDisposed || Disposing) return;

            _btnScan.Enabled = !busy;
            _btnSms.Enabled = !busy;
            _btnPassword.Enabled = !busy;
            _btnSign.Enabled = !busy;
            _inputToken.ReadOnly = busy;
            _btnToggleToken.Enabled = !busy;
            _switchEnabled.Enabled = !busy;
            _switchStartupSign.Enabled = !busy;
        }

        private void AppendLog(string message)
        {
            if (_logBox.IsDisposed) return;
            if (_logBox.InvokeRequired)
            {
                _logBox.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            var line = SkylandLogStore.Append(message);
            AppendLogLine(line);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
            UpdateLogScrollBar();
            HideLogCaret();
        }

        private void LoadLogHistory()
        {
            var lines = SkylandLogStore.LoadRecent();
            if (lines.Length == 0) return;

            _logBox.Text = string.Join(Environment.NewLine, lines);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
            UpdateLogScrollBar();
            HideLogCaret();
        }

        private void AppendLogLine(string line)
        {
            if (_logBox.TextLength > 0)
                _logBox.AppendText(Environment.NewLine);
            _logBox.AppendText(line);
        }

        private void UpdateLogScrollBar()
        {
            if (_logBox.IsDisposed || _logScrollBar.IsDisposed || _logScrollBar.IsDragging) return;

            if (!TryGetScrollMetrics(_logBox, _logScrollBar.Height, out var totalLines,
                    out var visibleLines, out var firstVisible))
            {
                if (_logBox.TextLength == 0)
                {
                    _logScrollBar.Visible = false;
                    return;
                }

                _logScrollBar.SetThumb(0, _logScrollBar.Height);
                return;
            }

            var trackHeight = _logScrollBar.Height;
            var thumbHeight = Math.Max(28, trackHeight * visibleLines / totalLines);
            var maxThumbTop = Math.Max(1, trackHeight - thumbHeight);
            var maxFirstVisible = Math.Max(1, totalLines - visibleLines);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, firstVisible * maxThumbTop / maxFirstVisible));

            _logScrollBar.SetThumb(thumbTop, thumbHeight);
        }

        private void HideLogCaret()
        {
            if (_logBox.IsDisposed || !_logBox.IsHandleCreated) return;
            HideCaret(_logBox.Handle);
        }

        private void RevealLogScrollBar(bool autoHide)
        {
            if (_logScrollBar.IsDisposed) return;

            UpdateLogScrollBar();
            if (_logBox.TextLength == 0) return;

            _logScrollBar.Visible = true;
            _logScrollBar.BringToFront();
            _logScrollHideTimer?.Stop();
            if (autoHide) _logScrollHideTimer?.Start();
        }

        private void ScheduleLogScrollBarHide(Control hoverScope)
        {
            if (_logScrollBar.IsDisposed || _logScrollBar.IsDragging) return;

            _logScrollHideTimer?.Stop();
            _logScrollHideTimer?.Start();
        }

        private bool LogNeedsScroll()
        {
            return !_logScrollBar.IsDisposed &&
                   TryGetScrollMetrics(_logBox, _logScrollBar.Height, out _, out _, out _);
        }

        private void ScrollLogToTrackTop(int requestedTop)
        {
            if (_logBox.IsDisposed || _logScrollBar.IsDisposed) return;

            if (!TryGetScrollMetrics(_logBox, _logScrollBar.Height, out var totalLines,
                    out var visibleLines, out var firstVisible))
            {
                UpdateLogScrollBar();
                return;
            }

            var maxThumbTop = Math.Max(1, _logScrollBar.Height - _logScrollBar.ThumbHeight);
            var thumbTop = Math.Min(maxThumbTop, Math.Max(0, requestedTop));
            if (thumbTop >= maxThumbTop - 1)
            {
                ScrollRichTextBoxToEnd(_logBox);
                _logScrollBar.SetThumb(maxThumbTop, _logScrollBar.ThumbHeight);
                return;
            }

            var targetFirstLine = thumbTop * Math.Max(1, totalLines - visibleLines) / maxThumbTop;
            var delta = targetFirstLine - firstVisible;
            ScrollLogLines(delta);

            _logScrollBar.SetThumb(thumbTop, _logScrollBar.ThumbHeight);
        }

        private void HandleLogMouseWheel(object sender, MouseEventArgs e)
        {
            var notches = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;
            var lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            ScrollLogLines(-notches * lines);
            UpdateLogScrollBar();
            RevealLogScrollBar(autoHide: true);
        }

        private void ScrollLogLines(int lineDelta)
        {
            if (lineDelta == 0 || _logBox.IsDisposed) return;
            SendMessage(_logBox.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(lineDelta));
        }

        private static void ScrollRichTextBoxToEnd(RichTextBox textBox)
        {
            if (textBox.IsDisposed) return;
            SendMessage(textBox.Handle, WmVScroll, new IntPtr(SbBottom), IntPtr.Zero);
        }

        private static bool TryGetScrollMetrics(RichTextBox textBox, int trackHeight,
            out int totalLines, out int visibleLines, out int firstVisible)
        {
            if (trackHeight <= 0)
            {
                totalLines = 1;
                visibleLines = 1;
                firstVisible = 0;
                return false;
            }

            totalLines = textBox.IsHandleCreated
                ? Math.Max(1, SendMessage(textBox.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero).ToInt32())
                : Math.Max(1, textBox.GetLineFromCharIndex(textBox.TextLength) + 1);
            firstVisible = textBox.IsHandleCreated
                ? Math.Max(0, SendMessage(textBox.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero).ToInt32())
                : textBox.GetLineFromCharIndex(textBox.GetCharIndexFromPosition(new Point(1, 1)));
            var bottomLine = textBox.GetLineFromCharIndex(
                textBox.GetCharIndexFromPosition(new Point(1, Math.Max(1, textBox.ClientSize.Height - 2))));
            visibleLines = Math.Max(1, bottomLine - firstVisible + 1);
            firstVisible = Math.Min(Math.Max(0, totalLines - visibleLines), firstVisible);
            return totalLines > visibleLines;
        }

        private static bool IsMouseInside(Control control)
        {
            return control != null &&
                   !control.IsDisposed &&
                   control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position);
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

        private sealed class SkylandPasswordTokenDialog : UserControl
        {
            private readonly SkylandService _service;
            private readonly Action<string> _log;
            private readonly AntdUI.Input _inputPhone;
            private readonly AntdUI.Input _inputPassword;
            private readonly AntdUI.Button _btnLogin;
            private bool _loggingIn;

            public string Token { get; private set; } = "";

            public SkylandPasswordTokenDialog(SkylandService service, Action<string> log)
            {
                _service = service;
                _log = log;
                const int left = 28;
                const int contentWidth = 424;
                var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
                var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
                var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.White;
                var accent = Color.FromArgb(22, 119, 255);

                Size = new Size(480, 300);
                BackColor = surface;

                var title = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.PasswordTitle", "账号密码获取 Token"),
                    Location = new Point(left, 12),
                    Size = new Size(320, 30),
                    Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                var subtitle = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.PasswordSubtitle", "使用森空岛账号登录，成功后会自动保存 Token。"),
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
                    Text = AntdUI.Localization.Get("App.Skyland.Account", "账号"),
                    Location = new Point(left + 12, 94),
                    Size = new Size(120, 28),
                    Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                _inputPhone = new AntdUI.Input
                {
                    Location = new Point(left, 130),
                    Size = new Size(contentWidth, 42),
                    PlaceholderText = AntdUI.Localization.Get("App.Skyland.AccountPlaceholder", "请输入手机号 / 账号"),
                    Radius = 6,
                };

                var passwordMark = new Panel
                {
                    Location = new Point(left, 194),
                    Size = new Size(3, 18),
                    BackColor = accent,
                };
                var lblPassword = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.Password", "密码"),
                    Location = new Point(left + 12, 188),
                    Size = new Size(120, 28),
                    Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                _inputPassword = new AntdUI.Input
                {
                    Location = new Point(left, 224),
                    Size = new Size(288, 42),
                    PlaceholderText = AntdUI.Localization.Get("App.Skyland.PasswordPlaceholder", "请输入密码"),
                    Radius = 6,
                    UseSystemPasswordChar = true,
                };
                _btnLogin = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.Skyland.LoginGet", "登录获取"),
                    IconSvg = "LoginOutlined",
                    Location = new Point(left + 304, 224),
                    Size = new Size(120, 42),
                    Radius = 6,
                    Type = AntdUI.TTypeMini.Success,
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
                Controls.Add(_inputPhone);
                Controls.Add(passwordMark);
                Controls.Add(lblPassword);
                Controls.Add(_inputPassword);
                Controls.Add(_btnLogin);
            }

            private async Task LoginAsync()
            {
                if (_loggingIn) return;

                _loggingIn = true;
                _btnLogin.Text = AntdUI.Localization.Get("App.Skyland.LoggingIn", "登录中...");
                _btnLogin.Enabled = false;
                _inputPhone.ReadOnly = true;
                _inputPassword.ReadOnly = true;

                try
                {
                    _log(AntdUI.Localization.Get("App.Skyland.PasswordLogging", "正在使用账号密码登录..."));
                    Token = await _service.LoginByPasswordAsync(_inputPhone.Text, _inputPassword.Text);
                    FindForm().DialogResult = DialogResult.OK;
                    FindForm().Close();
                }
                catch (Exception ex)
                {
                    _log(AntdUI.Localization.Get("App.Skyland.PasswordFailedPrefix", "账号密码获取 Token 失败：") + ex.Message);
                    AntdUI.Message.error(FindForm(), ex.Message);
                }
                finally
                {
                    _loggingIn = false;
                    _btnLogin.Text = AntdUI.Localization.Get("App.Skyland.LoginGet", "登录获取");
                    _btnLogin.Enabled = true;
                    _inputPhone.ReadOnly = false;
                    _inputPassword.ReadOnly = false;
                }
            }
        }

        private sealed class SkylandSmsTokenDialog : UserControl
        {
            private readonly SkylandService _service;
            private readonly Action<string> _log;
            private readonly AntdUI.Input _inputPhone;
            private readonly VerificationCodeInput _inputCode;
            private readonly AntdUI.Button _btnSend;
            private readonly AntdUI.Button _btnLogin;
            private readonly System.Windows.Forms.Timer _sendCooldownTimer;
            private static readonly TimeSpan SendCodeCooldown = TimeSpan.FromSeconds(60);
            private static DateTime _nextSendCodeTime = DateTime.MinValue;
            private bool _sendingCode;
            private bool _loggingIn;

            public string Token { get; private set; } = "";

            public SkylandSmsTokenDialog(SkylandService service, Action<string> log)
            {
                _service = service;
                _log = log;
                const int left = 28;
                const int contentWidth = 464;
                var subtleText = AntdUI.Config.IsDark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(112, 118, 128);
                var normalText = AntdUI.Config.IsDark ? AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
                var surface = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.White;
                var accent = Color.FromArgb(22, 119, 255);

                Size = new Size(520, 292);
                BackColor = surface;

                var title = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.SmsTitle", "手机号验证码获取 Token"),
                    Location = new Point(left, 12),
                    Size = new Size(360, 30),
                    Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                var subtitle = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.SmsSubtitle", "输入森空岛绑定手机号，获取验证码后自动登录。"),
                    Location = new Point(left, 42),
                    Size = new Size(420, 24),
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

                var phoneMark = new Panel
                {
                    Location = new Point(left, 100),
                    Size = new Size(3, 18),
                    BackColor = accent,
                };
                var lblPhone = new AntdUI.Label
                {
                    Text = AntdUI.Localization.Get("App.Skyland.Phone", "手机号"),
                    Location = new Point(left + 12, 94),
                    Size = new Size(120, 28),
                    Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                _inputPhone = new AntdUI.Input
                {
                    Location = new Point(left, 130),
                    Size = new Size(296, 40),
                    PlaceholderText = AntdUI.Localization.Get("App.Skyland.PhonePlaceholder", "请输入手机号"),
                    Radius = 6,
                };
                _btnSend = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.Skyland.SendCode", "发送验证码"),
                    Location = new Point(left + 312, 130),
                    Size = new Size(152, 40),
                    Radius = 6,
                    Type = AntdUI.TTypeMini.Primary,
                };
                var codeMark = new Panel
                {
                    Location = new Point(left, 194),
                    Size = new Size(3, 18),
                    BackColor = accent,
                };
                var lblCode = new AntdUI.Label
                {
                    Location = new Point(left + 12, 188),
                    Size = new Size(160, 28),
                    Text = AntdUI.Localization.Get("App.Skyland.Code", "验证码"),
                    Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    ForeColor = normalText,
                };
                _inputCode = new VerificationCodeInput(6)
                {
                    Location = new Point(left, 222),
                    Size = new Size(328, 44),
                };
                _btnLogin = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.Skyland.LoginGet", "登录获取"),
                    IconSvg = "LoginOutlined",
                    Location = new Point(left + 344, 222),
                    Size = new Size(120, 44),
                    Radius = 6,
                    Type = AntdUI.TTypeMini.Success,
                };

                _sendCooldownTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _sendCooldownTimer.Tick += (s, e) => UpdateSendButtonState();

                _btnSend.Click += async (s, e) => await SendCodeAsync();
                _btnLogin.Click += async (s, e) => await LoginAsync();
                _inputCode.Completed += async (s, e) => await LoginAsync();
                Controls.Add(title);
                Controls.Add(subtitle);
                Controls.Add(btnClose);
                Controls.Add(topLine);
                Controls.Add(phoneMark);
                Controls.Add(lblPhone);
                Controls.Add(_inputPhone);
                Controls.Add(_btnSend);
                Controls.Add(codeMark);
                Controls.Add(lblCode);
                Controls.Add(_inputCode);
                Controls.Add(_btnLogin);
                UpdateSendButtonState();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sendCooldownTimer?.Stop();
                    _sendCooldownTimer?.Dispose();
                }
                base.Dispose(disposing);
            }

            protected override async void OnParentChanged(EventArgs e)
            {
                base.OnParentChanged(e);
                var form = FindForm();
                if (form == null) return;
                form.FormClosing -= Form_FormClosing;
                form.FormClosing += Form_FormClosing;
            }

            private async void Form_FormClosing(object sender, FormClosingEventArgs e)
            {
                if (FindForm()?.DialogResult != DialogResult.OK) return;
                if (!string.IsNullOrWhiteSpace(Token)) return;

                e.Cancel = true;
                await LoginAsync();
            }

            private async Task LoginAsync()
            {
                if (_loggingIn || _inputCode.Text.Length < 6) return;

                _loggingIn = true;
                try
                {
                    _btnSend.Enabled = false;
                    _btnLogin.Text = AntdUI.Localization.Get("App.Skyland.LoggingIn", "登录中...");
                    _btnLogin.Enabled = false;
                    _log(AntdUI.Localization.Get("App.Skyland.SmsLogging", "正在使用手机号验证码登录..."));
                    Token = await _service.LoginByPhoneCodeAsync(_inputPhone.Text, _inputCode.Text);
                    FindForm().DialogResult = DialogResult.OK;
                    FindForm().Close();
                }
                catch (Exception ex)
                {
                    _log(AntdUI.Localization.Get("App.Skyland.SmsFailedPrefix", "手机号验证码获取 Token 失败：") + ex.Message);
                    AntdUI.Message.error(FindForm(), ex.Message);
                }
                finally
                {
                    _loggingIn = false;
                    _btnLogin.Text = AntdUI.Localization.Get("App.Skyland.LoginGet", "登录获取");
                    UpdateSendButtonState();
                }
            }

            private async Task SendCodeAsync()
            {
                if (GetSendCooldownSeconds() > 0 || _sendingCode) return;

                _sendingCode = true;
                UpdateSendButtonState();
                try
                {
                    await _service.SendPhoneCodeAsync(_inputPhone.Text);
                    _nextSendCodeTime = DateTime.Now.Add(SendCodeCooldown);
                    _log(AntdUI.Localization.Get("App.Skyland.CodeSent", "验证码已发送。"));
                    AntdUI.Message.success(FindForm(), AntdUI.Localization.Get("App.Skyland.CodeSentToast", "验证码已发送"));
                    _inputCode.FocusFirst();
                }
                catch (Exception ex)
                {
                    _log(AntdUI.Localization.Get("App.Skyland.SendCodeFailedPrefix", "发送验证码失败：") + ex.Message);
                    AntdUI.Message.error(FindForm(), ex.Message);
                }
                finally
                {
                    _sendingCode = false;
                    UpdateSendButtonState();
                }
            }

            private void UpdateSendButtonState()
            {
                var seconds = GetSendCooldownSeconds();
                if (seconds > 0)
                {
                    _btnSend.Text = string.Format(AntdUI.Localization.Get("App.Skyland.ResendInSeconds", "{0}秒后重发"), seconds);
                    _btnSend.Enabled = false;
                    if (!_sendCooldownTimer.Enabled) _sendCooldownTimer.Start();
                    return;
                }

                if (_sendCooldownTimer.Enabled) _sendCooldownTimer.Stop();
                _btnSend.Text = _sendingCode
                    ? AntdUI.Localization.Get("App.Skyland.Sending", "发送中...")
                    : AntdUI.Localization.Get("App.Skyland.SendCode", "发送验证码");
                _btnSend.Enabled = !_sendingCode && !_loggingIn;
                _btnLogin.Enabled = !_loggingIn;
            }

            private static int GetSendCooldownSeconds()
            {
                var seconds = (int)Math.Ceiling((_nextSendCodeTime - DateTime.Now).TotalSeconds);
                return Math.Max(0, seconds);
            }
        }

        private sealed class VerificationCodeInput : UserControl
        {
            private readonly AntdUI.Input[] _inputs;
            private bool _suppressChange;

            public event EventHandler Completed;

            public VerificationCodeInput(int length)
            {
                if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

                _inputs = new AntdUI.Input[length];
                Size = new Size(344, 44);

                var panel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = length,
                    RowCount = 1,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                };
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                for (var i = 0; i < length; i++)
                    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / length));

                for (var i = 0; i < length; i++)
                {
                    var input = new AntdUI.Input
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(4, 0, 4, 0),
                        MaxLength = 1,
                        Radius = 6,
                        TabIndex = i,
                        TextAlign = HorizontalAlignment.Center,
                        ImeMode = ImeMode.Disable,
                    };
                    input.VerifyKeyboard += VerifyKeyboard;
                    input.TextChanged += CodeTextChanged;
                    input.GotFocus += CodeGotFocus;
                    input.KeyPress += CodeKeyPress;

                    _inputs[i] = input;
                    panel.Controls.Add(input, i, 0);
                }

                Controls.Add(panel);
            }

            public override string Text
            {
                get => _inputs == null ? base.Text : string.Concat(_inputs.Select(input => input.Text));
                set
                {
                    if (_inputs == null) base.Text = value;
                    else SetCode(value ?? "");
                }
            }

            public void FocusFirst()
            {
                if (_inputs.Length > 0) _inputs[0].Focus();
            }

            private void SetCode(string value)
            {
                _suppressChange = true;
                try
                {
                    var digits = value.Where(char.IsDigit).Take(_inputs.Length).ToArray();
                    for (var i = 0; i < _inputs.Length; i++)
                        _inputs[i].Text = i < digits.Length ? digits[i].ToString() : "";

                    var focusIndex = Math.Min(digits.Length, _inputs.Length - 1);
                    FocusInput(focusIndex);
                }
                finally
                {
                    _suppressChange = false;
                }
                RaiseCompletedIfReady();
            }

            private void CodeGotFocus(object sender, EventArgs e)
            {
                var firstEmpty = Array.Find(_inputs, input => string.IsNullOrWhiteSpace(input.Text));
                if (firstEmpty != null && !ReferenceEquals(firstEmpty, sender))
                    FocusInput(Array.IndexOf(_inputs, firstEmpty));
            }

            private void FocusInput(int index)
            {
                if (index < 0 || index >= _inputs.Length) return;
                if (IsHandleCreated) BeginInvoke(new Action(() => _inputs[index].Focus()));
                else _inputs[index].Focus();
            }

            private void CodeTextChanged(object sender, EventArgs e)
            {
                if (_suppressChange || sender is not AntdUI.Input input) return;

                var index = Array.IndexOf(_inputs, input);
                if (index < 0) return;

                if (input.Text.Length > 1)
                {
                    SetCode(input.Text);
                    return;
                }

                if (input.Text.Length == 1)
                {
                    var value = input.Text[0];
                    if (!char.IsDigit(value))
                    {
                        input.Clear();
                        return;
                    }

                    if (index + 1 < _inputs.Length)
                    {
                        _inputs[index + 1].Clear();
                        _inputs[index + 1].Focus();
                    }
                    else
                    {
                        RaiseCompletedIfReady();
                    }
                }
            }

            private void VerifyKeyboard(object sender, AntdUI.InputVerifyKeyboardEventArgs e)
            {
                if (sender is not AntdUI.Input input || e.KeyData != (Keys.Control | Keys.V)) return;

                e.Result = false;
                var text = AntdUI.Helper.ClipboardGetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var start = Math.Max(Array.IndexOf(_inputs, input), 0);
                var digits = text.Where(char.IsDigit).ToArray();
                if (digits.Length == 0) return;

                _suppressChange = true;
                try
                {
                    for (var i = 0; i < digits.Length && start + i < _inputs.Length; i++)
                        _inputs[start + i].Text = digits[i].ToString();
                }
                finally
                {
                    _suppressChange = false;
                }

                var next = Math.Min(start + digits.Length, _inputs.Length - 1);
                _inputs[next].Focus();
                RaiseCompletedIfReady();
            }

            private void CodeKeyPress(object sender, KeyPressEventArgs e)
            {
                if (sender is not AntdUI.Input input) return;

                if (e.KeyChar == 8)
                {
                    var index = Array.IndexOf(_inputs, input);
                    input.Clear();
                    input.ClearUndo();
                    if (index > 0)
                    {
                        _inputs[index - 1].Clear();
                        _inputs[index - 1].Focus();
                    }
                    return;
                }

                if (!char.IsDigit(e.KeyChar))
                    e.Handled = true;
            }

            private void RaiseCompletedIfReady()
            {
                if (_inputs.All(input => input.Text.Length == 1 && char.IsDigit(input.Text[0])))
                    Completed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
