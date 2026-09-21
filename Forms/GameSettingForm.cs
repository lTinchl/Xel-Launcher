using AntdUI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public class GameSettingForm : UserControl
    {
        private readonly GameEntry _game;
        private readonly Overview _overview;
        private readonly GamePage _gamePage;
        private AntdUI.Input _inputPath;
        private AntdUI.Button _btnBrowse;
        private AntdUI.Button _btnCreateLinked;
        private AntdUI.Button _btnDetachLinked;
        private AntdUI.Button _btnReplaceLegacy;
        private readonly Action _onPathChanged;
        private Action _applyResponsiveLayout;
        private bool _suppressPathAutoSave;

        private Size LogicalSize(int width, int height) => new Size(width, LogicalPixels(height));

        private int LogicalPixels(int value)
        {
            using var g = CreateGraphics();
            var scale = Math.Max(1F, g.DpiY / 96F);
            return Math.Max(1, (int)Math.Round(value / scale));
        }

        public GameSettingForm(GameEntry game, Overview overview, Action onAccountSwitchChanged = null, Action onPathChanged = null, GamePage gamePage = null)
        {
            _game = game;
            _overview = overview;
            _gamePage = gamePage;
            _onPathChanged = onPathChanged;
            var cfg = ConfigHelper.Load();
            var latest = cfg.Games.Find(g => g.Name == game.Name && g.IconName == game.IconName);
            string currentPath = latest?.RootPath ?? game.RootPath;
            bool linkedGroupActive = LinkedClientPolicy.IsSharedClient(
                game.IconName, currentPath);

            Font = new Font("Microsoft YaHei UI", 9F);
            var surfaceBack = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.White;
            BackColor = surfaceBack;
            Size = LogicalSize(380, 520);
            MinimumSize = LogicalSize(340, 320);
            AutoScroll = false;

            // ── 游戏安装路径 标题 ──
            var lblPathSection = new AntdUI.Label
            {
                Text = Localizer.GetRequiredString("App.GameSetting.InstallPath"),
                Location = new Point(20, 94),
                Size = new Size(320, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
            };

            // ── 安装路径输入框 ──
            _inputPath = new AntdUI.Input
            {
                Text = currentPath,
                Location = new Point(20, 124),
                Size = new Size(320, 36),
                PlaceholderText = Localizer.GetRequiredString("App.GameSetting.PathPlaceholder"),
                ReadOnly = linkedGroupActive,
            };
            _inputPath.TextChanged += (s, e) =>
            {
                if (!_suppressPathAutoSave)
                    AutoSave(_inputPath.Text.Trim());
            };
            _inputPath.Leave += (s, e) =>
            {
                _onPathChanged?.Invoke();
                ResetPathDisplay();
            };

            // ── 更改路径 ──
            var btnBrowse = _btnBrowse = new AntdUI.Button
            {
                Text = Localizer.GetRequiredString("App.GameSetting.ChangePath"),
                IconSvg = "FolderOpenOutlined",
                IconRatio = .58F,
                IconGap = .18F,
                Location = new Point(20, 172),
                Size = new Size(320, 36),
                Ghost = true,
                Enabled = !linkedGroupActive,
            };
            btnBrowse.Click += (s, e) => BrowsePath();

            // ── 打开文件目录 ──
            var btnOpenDir = new AntdUI.Button
            {
                Text = Localizer.GetRequiredString("App.GameSetting.OpenDir"),
                IconSvg = "FolderOutlined",
                IconRatio = .58F,
                IconGap = .18F,
                Location = new Point(20, 220),
                Size = new Size(320, 36),
                Ghost = true,
            };
            btnOpenDir.Click += (s, e) =>
            {
                string path = _inputPath.Text.Trim();
                if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            };

            AntdUI.Divider dividerToken = null;
            AntdUI.Label lblToken = null;
            AntdUI.Input inputToken = null;
            AntdUI.Button btnAutoToken = null;

            if (game.IconName == "BiliArknights")
            {
                var btnCreateLinked = _btnCreateLinked = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.LinkedClient.CreateBili"),
                    IconSvg = "LinkOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Type = AntdUI.TTypeMini.Primary,
                    // Linked Runtime is created transparently from the normal
                    // Start Game flow. Keep this legacy control unavailable for
                    // old builds/config compatibility; existing linked pairs can
                    // still be detached with the adjacent recovery action.
                    Enabled = false,
                    Visible = false,
                };
                btnCreateLinked.Click += (s, e) =>
                    CreateLinkedBilibiliClient();
                Controls.Add(btnCreateLinked);

                var btnDetachLinked = _btnDetachLinked = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.LinkedClient.Detach"),
                    IconSvg = "DisconnectOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                    Enabled = linkedGroupActive,
                    Visible = linkedGroupActive,
                };
                btnDetachLinked.Click += (s, e) =>
                    DetachLinkedBilibiliClient();
                Controls.Add(btnDetachLinked);

                var btnReplaceOfficial = _btnReplaceLegacy = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.ReplaceBili"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                    Enabled = !linkedGroupActive,
                };
                btnReplaceOfficial.Click += async (s, e) =>
                {
                    string path = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetBiliPath"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplace"),
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplaceArkBili"),
                        AntdUI.TType.Warn)
                    {
                        OkText = Localizer.GetRequiredString("OK"),
                        CancelText = Localizer.GetRequiredString("Cancel")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.GameSetting.Replacing"), async (cfg) =>
                    {
                        try
                        {
                            LinkedClientPolicy.ClearLegacyPairState(
                                ConfigHelper.Load());
                            GameLauncher.EnsureGameClientClosed(
                                path, isEndfield: false);
                            await GameLauncher.SwitchServerWithResult(path, "BiliArknights", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, false, _ => { });
                            var cfg2 = ConfigHelper.Load();
                            var BiliBili = cfg2.Games.Find(g => g.IconName == "Arknights");
                            LinkedClientPolicy.ClearLegacyPairState(cfg2);
                            if (BiliBili != null) BiliBili.RootPath = path;
                            ConfigHelper.Save(cfg2);
                            cfg.OK(Localizer.GetRequiredString("App.GameSetting.ReplaceSuccess"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(Localizer.GetRequiredString("App.GameSetting.ReplaceFailed") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplaceOfficial);

                var btnBili = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.BiliWebsite"),
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                // Website button hidden.
                Size = LogicalSize(360, 386);
            }
            else if (game.IconName == "Endfield")
            {
                var btn = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.EndfieldWebsite"),
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                // Website button hidden.

                var btnSync = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.SyncToAll"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 312),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnSync.Click += (s, e) =>
                {
                    string currentPath = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetOfficialPath"));
                        return;
                    }
                    var cfg = ConfigHelper.Load();
                    foreach (var icon in new[] { "BiliEndfield", "GlobalEndfield", "PlayEndfield" })
                    {
                        var other = cfg.Games.Find(g => g.IconName == icon);
                        if (other != null) other.RootPath = currentPath;
                    }
                    ConfigHelper.Save(cfg);
                    AntdUI.Message.success(_overview, Localizer.GetRequiredString("App.GameSetting.SyncSuccessAll"));
                };
                Controls.Add(btnSync);
                Size = LogicalSize(360, 386);
            }
            else if (game.IconName == "BiliEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.ReplaceBili"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnReplace.Click += async (s, e) =>
                {
                    string path = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetBiliPath"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplace"),
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplaceEndBili"),
                        AntdUI.TType.Warn)
                    {
                        OkText = Localizer.GetRequiredString("OK"),
                        CancelText = Localizer.GetRequiredString("Cancel")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.GameSetting.Replacing"), async (cfg) =>
                    {
                        try
                        {
                            GameLauncher.EnsureGameClientClosed(
                                path, isEndfield: true);
                            await GameLauncher.SwitchServerWithResult(path, "BiliEndfield", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, _ => { });
                            cfg.OK(Localizer.GetRequiredString("App.GameSetting.ReplaceSuccess"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(Localizer.GetRequiredString("App.GameSetting.ReplaceFailed") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplace);
            }
            else if (game.IconName == "GlobalEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.ReplaceGlobal"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnReplace.Click += async (s, e) =>
                {
                    string path = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetGlobalPath"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplace"),
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplaceGlobal"),
                        AntdUI.TType.Warn)
                    {
                        OkText = Localizer.GetRequiredString("OK"),
                        CancelText = Localizer.GetRequiredString("Cancel")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.GameSetting.Replacing"), async (cfg) =>
                    {
                        try
                        {
                            GameLauncher.EnsureGameClientClosed(
                                path, isEndfield: true);
                            await GameLauncher.SwitchServerWithResult(path, "GlobalEndfield", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, _ => { });
                            cfg.OK(Localizer.GetRequiredString("App.GameSetting.ReplaceSuccess"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(Localizer.GetRequiredString("App.GameSetting.ReplaceFailed") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplace);
            }
            else if (game.IconName == "PlayEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.ReplacePlay"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnReplace.Click += async (s, e) =>
                {
                    string path = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetPlayPath"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplace"),
                        Localizer.GetRequiredString("App.GameSetting.ConfirmReplacePlay"),
                        AntdUI.TType.Warn)
                    {
                        OkText = Localizer.GetRequiredString("OK"),
                        CancelText = Localizer.GetRequiredString("Cancel")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, Localizer.GetRequiredString("App.GameSetting.Replacing"), async (cfg) =>
                    {
                        try
                        {
                            GameLauncher.EnsureGameClientClosed(
                                path, isEndfield: true);
                            await GameLauncher.SwitchServerWithResult(path, game.IconName, msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, _ => { });
                            cfg.OK(Localizer.GetRequiredString("App.GameSetting.ReplaceSuccess"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(Localizer.GetRequiredString("App.GameSetting.ReplaceFailed") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplace);

                // ── Token 分割线 ──
                dividerToken = new AntdUI.Divider
                {
                    Location = new Point(20, 316),
                    Size = new Size(320, 1),
                    Thickness = 1F,
                };

                // ── Token 标题 ──
                lblToken = new AntdUI.Label
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.SessionToken"),
                    Location = new Point(20, 330),
                    Size = new Size(320, 24),
                    Font = new Font("Microsoft YaHei UI", 9F),
                };

                // ── Token 输入框 ──
                var cfgToken = ConfigHelper.Load();
                var tokenEntry = cfgToken.Games.Find(g => g.IconName == game.IconName);
                string savedToken = tokenEntry?.SessionToken ?? "";
                inputToken = new AntdUI.Input
                {
                    Text = savedToken,
                    Location = new Point(20, 358),
                    Size = new Size(320, 36),
                    PlaceholderText = Localizer.GetRequiredString("App.GameSetting.TokenPlaceholder"),
                };
                inputToken.TextChanged += (s, e) =>
                {
                    var cfgT = ConfigHelper.Load();
                    var entryT = cfgT.Games.Find(g => g.IconName == game.IconName);
                    if (entryT != null)
                    {
                        entryT.SessionToken = inputToken.Text.Trim();
                        ConfigHelper.Save(cfgT);
                    }
                };

                // ── 自动获取 Token ──
                btnAutoToken = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.AutoGetToken"),
                    Location = new Point(20, 406),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnAutoToken.Click += (s, e) =>
                {
                    try
                    {
                        string rawCommand = "(Get-CimInstance Win32_Process -Filter \"Name = 'Games.exe'\").CommandLine";
                        byte[] commandBytes = System.Text.Encoding.Unicode.GetBytes(rawCommand);
                        string encodedCommand = Convert.ToBase64String(commandBytes);

                        var psi = new ProcessStartInfo("powershell")
                        {
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = System.Text.Encoding.UTF8
                        };

                        var proc = Process.Start(psi);
                        string output = proc.StandardOutput.ReadToEnd();
                        string error = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();

                        var match = System.Text.RegularExpressions.Regex.Match(
                            output, @"--g_session_token=(\S+)");
                        if (match.Success)
                        {
                            string token = match.Groups[1].Value;
                            inputToken.Text = token;
                            AntdUI.Message.success(_overview,
                                Localizer.GetRequiredString("App.GameSetting.TokenSuccess"));
                        }
                        else
                        {
                            string detail = "";
                            if (!string.IsNullOrWhiteSpace(error))
                                detail = error.Trim();
                            else if (!string.IsNullOrWhiteSpace(output))
                                detail = output.Trim();
                            AntdUI.Modal.open(new AntdUI.Modal.Config(
                                FindForm() as AntdUI.BaseForm ?? null,
                                Localizer.GetRequiredString("App.GameSetting.TokenNotFound"),
                                string.IsNullOrEmpty(detail) ? "No output from PowerShell" : detail,
                                AntdUI.TType.Warn)
                            {
                                CancelText = null,
                                Width = 560,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        AntdUI.Modal.open(new AntdUI.Modal.Config(
                            FindForm() as AntdUI.BaseForm ?? null,
                            Localizer.GetRequiredString("App.GameSetting.TokenNotFound"),
                            ex.ToString(),
                            AntdUI.TType.Error)
                        {
                            CancelText = null,
                            Width = 560,
                        });
                    }
                };

                Controls.Add(dividerToken);
                Controls.Add(lblToken);
                Controls.Add(inputToken);
                Controls.Add(btnAutoToken);
            }
            else
            {
                var btnSync = new AntdUI.Button
                {
                    Text = Localizer.GetRequiredString("App.GameSetting.SyncToBili"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                    Enabled = !linkedGroupActive,
                };
                btnSync.Click += (s, e) =>
                {
                    string currentPath = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.GameSetting.WarnSetOfficialPath"));
                        return;
                    }
                    try
                    {
                        var cfg = ConfigHelper.Load();
                        LinkedClientPolicy.ClearLegacyPairState(cfg);
                        var bili = cfg.Games.Find(g => g.IconName == "BiliArknights");
                        if (bili != null) bili.RootPath = currentPath;
                        ConfigHelper.Save(cfg);
                        AntdUI.Message.success(_overview, Localizer.GetRequiredString("App.GameSetting.SyncSuccess"));
                    }
                    catch (InvalidOperationException ex)
                    {
                        AntdUI.Message.warn(_overview, ex.Message);
                    }
                };
                Controls.Add(btnSync);
                Size = LogicalSize(360, 386);
            }

            // ── 读取持久化的 Switch 状态 ──
            var cfgNow = ConfigHelper.Load();
            var entryNow = cfgNow.Games.Find(g => g.IconName == game.IconName);
            bool syncEnabled = entryNow?.SyncLaunchEnabled ?? false;
            bool accountSwitchEnabled = entryNow?.AccountSwitchEnabled ?? false;
            bool launchArgsEnabled = entryNow?.CustomLaunchArgsEnabled ?? false;

            var divider2 = new AntdUI.Divider
            {
                Location = new Point(20, 630),
                Size = new Size(264, 20),
                Thickness = 1F,
                Text = Localizer.GetRequiredString("App.GameSetting.CustomSync"),
                Orientation = AntdUI.TOrientation.Left,
                OrientationMargin = 0
            };

            var swExtra = new AntdUI.Switch
            {
                Location = new Point(304, 630),
                Size = new Size(36, 20),
                Checked = syncEnabled,
            };

            // ── 管理按钮（Switch 开启时才显示）──
            var btnManage = new AntdUI.Button
            {
                Text = Localizer.GetRequiredString("App.GameSetting.ManageSync"),
                Location = new Point(20, 660),
                Size = new Size(320, 36),
                Ghost = true,
                Visible = syncEnabled,
            };
            btnManage.Click += (s, e) =>
            {
                var syncForm = new SyncAppManagerForm(entryNow ?? game, _overview);

                AntdUI.Drawer.open(_overview, syncForm, AntdUI.TAlignMini.Right);
            };
            swExtra.CheckedChanged += (s, e) =>
            {
                bool on = swExtra.Checked;
                btnManage.Visible = on;

                // 持久化 Switch 状态
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == game.IconName);
                if (entry != null)
                {
                    entry.SyncLaunchEnabled = on;
                    ConfigHelper.Save(cfg);
                }
            };

            Size = LogicalSize(380, syncEnabled ? 560 : 520);

            // ── 启用账号切换 ──
            bool showAccountSwitch =
                GameChannelCatalog.Get(game.IconName)?.SupportsAccountSwitch == true;
            AntdUI.Divider divider3 = null;
            AntdUI.Switch swacmg = null;
            if (showAccountSwitch)
            {
                divider3 = new AntdUI.Divider
                {
                    Location = new Point(20, 530),
                    Size = new Size(264, 20),
                    Thickness = 1F,
                    Text = Localizer.GetRequiredString("App.GameSetting.AccountSwitch"),
                    Orientation = AntdUI.TOrientation.Left,
                    OrientationMargin = 0
                };

                swacmg = new AntdUI.Switch
                {
                    Location = new Point(304, 530),
                    Size = new Size(36, 20),
                    Checked = accountSwitchEnabled,
                };
                swacmg.CheckedChanged += (s, e) =>
                {
                    var cfg = ConfigHelper.Load();
                    var entry = cfg.Games.Find(g => g.IconName == game.IconName);
                    if (entry != null)
                    {
                        entry.AccountSwitchEnabled = swacmg.Checked;
                        ConfigHelper.Save(cfg);
                    }
                    onAccountSwitchChanged?.Invoke();
                };
            }

            // ── 自定义启动参数 ──
            var dividerArgs = new AntdUI.Divider
            {
                Location = new Point(20, 560),
                Size = new Size(270, 20),
                Thickness = 1F,
                Text = Localizer.GetRequiredString("App.GameSetting.CustomLaunchArgs"),
                Orientation = AntdUI.TOrientation.Left,
                OrientationMargin = 0
            };

            var swArgs = new AntdUI.Switch
            {
                Location = new Point(304, 560),
                Size = new Size(36, 20),
                Checked = launchArgsEnabled,
            };

            var inputArgs = new AntdUI.Input
            {
                Location = new Point(18, 584),
                Size = new Size(320, 36),
                Text = entryNow?.CustomLaunchArgs ?? "",
                ReadOnly = !launchArgsEnabled,
                PlaceholderText = Localizer.GetRequiredString("App.GameSetting.CustomLaunchArgsPlaceholder"),
            };
            inputArgs.TextChanged += (s, e) =>
            {
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == game.IconName);
                if (entry != null)
                {
                    entry.CustomLaunchArgs = inputArgs.Text;
                    ConfigHelper.Save(cfg);
                }
            };
            swArgs.CheckedChanged += (s, e) =>
            {
                bool on = swArgs.Checked;
                inputArgs.ReadOnly = !on;
                var cfg = ConfigHelper.Load();
                var entry = cfg.Games.Find(g => g.IconName == game.IconName);
                if (entry != null)
                {
                    entry.CustomLaunchArgsEnabled = on;
                    ConfigHelper.Save(cfg);
                }
            };

            if (showAccountSwitch)
            {
                Controls.Add(divider3);
                Controls.Add(swacmg);
            }
            Controls.Add(dividerArgs);
            Controls.Add(swArgs);
            Controls.Add(inputArgs);
            Controls.Add(divider2);
            Controls.Add(swExtra);
            Controls.Add(btnManage);
            Controls.Add(lblPathSection);
            Controls.Add(_inputPath);
            Controls.Add(btnBrowse);
            Controls.Add(btnOpenDir);
            Size = GetInitialDrawerSize();
            MinimumSize = new Size(Math.Min(320, Size.Width), Math.Min(300, Size.Height));

            var contentPanel = new System.Windows.Forms.Panel
            {
                Location = Point.Empty,
                Size = new Size(Width, Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = surfaceBack,
                TabStop = false,
            };
            var contentControls = Controls
                .Cast<Control>()
                .ToArray();
            foreach (var control in contentControls)
            {
                Controls.Remove(control);
                contentPanel.Controls.Add(control);
            }
            Controls.Add(contentPanel);

            ApplyGameAccentTheme();

            swExtra.CheckedChanged += (s, e) => ApplyResponsiveLayout();
            Resize += (s, e) => ApplyResponsiveLayout();
            HandleCreated += (s, e) =>
            {
                FitToDrawerHost();
                ApplyResponsiveLayout();
                ResetPathDisplay();
            };
            Control drawerHost = null;
            EventHandler drawerHostResize = (s, e) =>
            {
                FitToDrawerHost();
                ApplyResponsiveLayout();
            };
            ParentChanged += (s, e) =>
            {
                if (drawerHost != null)
                    drawerHost.Resize -= drawerHostResize;

                drawerHost = Parent;
                if (drawerHost != null)
                    drawerHost.Resize += drawerHostResize;

                FitToDrawerHost();
                ApplyResponsiveLayout();
            };
            Disposed += (s, e) =>
            {
                if (drawerHost != null)
                    drawerHost.Resize -= drawerHostResize;

                _applyResponsiveLayout = null;
            };
            VisibleChanged += (s, e) =>
            {
                if (!Visible) return;

                FitToDrawerHost();
                ApplyResponsiveLayout();
            };
            _applyResponsiveLayout = ApplyResponsiveLayout;
            ApplyResponsiveLayout();

            void FitToDrawerHost()
            {
                if (IsDisposed || Parent == null) return;

                int hostHeight = Parent.ClientSize.Height;
                if (hostHeight > 0 && Height != hostHeight)
                    Height = hostHeight;
            }

            void ApplyResponsiveLayout()
            {
                if (IsDisposed) return;

                int gapTiny = Math.Max(6, lblPathSection.Font.Height / 3);
                int gapSmall = Math.Max(10, lblPathSection.Font.Height / 2);
                int gapMedium = Math.Max(14, lblPathSection.Font.Height * 2 / 3);
                int inputHeight = Math.Max(36, _inputPath.Font.Height + gapSmall * 2);
                int buttonHeight = Math.Max(36, btnBrowse.Font.Height + gapSmall * 2);

                int reservedScrollWidth = game.IconName == "PlayEndfield" ? 0 : SystemInformation.VerticalScrollBarWidth + 24;
                int viewportWidth = Math.Max(0, contentPanel.ClientSize.Width - reservedScrollWidth);
                int sidePadding = viewportWidth >= 420 ? 32 : 24;
                int contentWidth = Math.Min(560, Math.Max(260, viewportWidth - sidePadding * 2));
                int margin = Math.Max(sidePadding, (viewportWidth - contentWidth) / 2);
                int switchX = margin + contentWidth - 36;
                int contentRight = margin + contentWidth;

                lblPathSection.Left = margin;
                lblPathSection.Top = gapMedium;
                lblPathSection.Width = contentWidth;
                _inputPath.Left = margin;
                _inputPath.Top = lblPathSection.Bottom + gapSmall;
                _inputPath.Width = contentWidth;
                _inputPath.Height = inputHeight;
                btnBrowse.Left = margin;
                btnBrowse.Top = _inputPath.Bottom + gapSmall;
                btnBrowse.Width = contentWidth;
                btnBrowse.Height = buttonHeight;
                btnOpenDir.Left = margin;
                btnOpenDir.Top = btnBrowse.Bottom + gapSmall;
                btnOpenDir.Width = contentWidth;
                btnOpenDir.Height = buttonHeight;

                Control[] lowerControls = showAccountSwitch
                    ? new Control[] { divider3, swacmg, dividerArgs, swArgs, inputArgs, divider2, swExtra, btnManage }
                    : new Control[] { dividerArgs, swArgs, inputArgs, divider2, swExtra, btnManage };

                int actionTop = btnOpenDir.Bottom + gapSmall;
                foreach (Control control in contentPanel.Controls)
                {
                    if (control == lblPathSection ||
                        control == _inputPath ||
                        control == btnBrowse ||
                        control == btnOpenDir ||
                        control == dividerToken ||
                        control == lblToken ||
                        control == inputToken ||
                        control == btnAutoToken ||
                        Array.IndexOf(lowerControls, control) >= 0)
                    {
                        continue;
                    }

                    if (!control.Visible) continue;

                    if (control is AntdUI.Button button && control.Width > 80)
                    {
                        control.Left = margin;
                        control.Top = actionTop;
                        control.Width = contentWidth;
                        control.Height = buttonHeight;
                        button.TextAlign = ContentAlignment.MiddleCenter;
                        actionTop = control.Bottom + gapSmall;
                    }
                    else if (control is AntdUI.Input && control.Width > 80)
                    {
                        control.Left = margin;
                        control.Top = actionTop;
                        control.Width = contentWidth;
                        control.Height = inputHeight;
                        actionTop = control.Bottom + gapSmall;
                    }
                }
                btnBrowse.TextAlign = ContentAlignment.MiddleCenter;
                btnOpenDir.TextAlign = ContentAlignment.MiddleCenter;

                if (dividerToken != null)
                {
                    int tokenTop = actionTop + gapTiny;

                    dividerToken.Left = margin;
                    dividerToken.Top = tokenTop;
                    dividerToken.Width = contentWidth;

                    lblToken.Left = margin;
                    lblToken.Top = dividerToken.Bottom + gapSmall;
                    lblToken.Width = contentWidth;

                    inputToken.Left = margin;
                    inputToken.Top = lblToken.Bottom + gapSmall;
                    inputToken.Width = contentWidth;
                    inputToken.Height = inputHeight;

                    btnAutoToken.Left = margin;
                    btnAutoToken.Top = inputToken.Bottom + gapSmall;
                    btnAutoToken.Width = contentWidth;
                    btnAutoToken.Height = buttonHeight;
                    btnAutoToken.TextAlign = ContentAlignment.MiddleCenter;

                    actionTop = btnAutoToken.Bottom + gapSmall;
                }

                int lowerTop = Math.Max(btnOpenDir.Bottom + gapMedium, actionTop + gapMedium);

                if (showAccountSwitch)
                {
                    divider3.Location = new Point(margin, lowerTop);
                    divider3.Width = Math.Max(160, contentWidth - 56);
                    swacmg.Location = new Point(switchX, lowerTop);
                    lowerTop += Math.Max(34, divider3.Height + gapSmall);
                }

                dividerArgs.Location = new Point(margin, lowerTop);
                dividerArgs.Width = Math.Max(160, contentWidth - 56);
                swArgs.Location = new Point(switchX, lowerTop);

                inputArgs.Location = new Point(margin, lowerTop + Math.Max(30, dividerArgs.Height + gapSmall));
                inputArgs.Width = contentWidth;
                inputArgs.Height = inputHeight;

                int syncTop = inputArgs.Bottom + gapMedium;
                divider2.Location = new Point(margin, syncTop);
                divider2.Width = Math.Max(160, contentWidth - 56);
                swExtra.Location = new Point(switchX, syncTop);

                btnManage.Location = new Point(margin, syncTop + Math.Max(42, divider2.Height + gapMedium));
                btnManage.Width = contentWidth;
                btnManage.Height = buttonHeight;
                btnManage.TextAlign = ContentAlignment.MiddleCenter;

                foreach (Control control in contentPanel.Controls)
                {
                    if (!control.Visible) continue;
                    if (control is AntdUI.Switch)
                    {
                        control.Left = Math.Min(control.Left, contentRight - control.Width);
                    }
                    else if (control.Right > contentRight && control.Width > 16)
                    {
                        control.Width = Math.Max(16, contentRight - control.Left);
                    }
                }

                int contentBottom = btnManage.Visible
                    ? btnManage.Bottom
                    : Math.Max(divider2.Bottom, swExtra.Bottom);
                contentPanel.AutoScrollMinSize = new Size(0, contentBottom + 40);
                contentPanel.HorizontalScroll.Enabled = false;
                contentPanel.HorizontalScroll.Maximum = 0;
            }

        }

        private Size GetInitialDrawerSize()
        {
            var ownerSize = _overview?.ClientSize ?? Size.Empty;
            if (ownerSize.Width <= 0 || ownerSize.Height <= 0) return Size;

            int width = Math.Min(Math.Max(340, Width), Math.Max(300, ownerSize.Width - 48));
            int height = Math.Max(320, ownerSize.Height);
            return new Size(width, height);
        }

        private void CreateLinkedBilibiliClient()
        {
            var currentConfig = ConfigHelper.Load();
            var official = currentConfig.Games.Find(g => g.IconName == "Arknights");
            var bilibili = currentConfig.Games.Find(g => g.IconName == "BiliArknights");
            var officialPath = official?.RootPath?.Trim();
            if (string.IsNullOrWhiteSpace(officialPath) || !Directory.Exists(officialPath))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.LinkedClient.Error.SourceMissing"));
                return;
            }

            if (LinkedClientPolicy.IsSharedClient(
                    "Arknights", official?.RootPath) ||
                LinkedClientPolicy.IsSharedClient(
                    "BiliArknights", bilibili?.RootPath))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.LinkedClient.Error.MutationBlocked"));
                return;
            }

            if (GameUpdateManager.Find(officialPath) != null ||
                GameRepairManager.IsRepairing(officialPath))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                return;
            }

            var owner = FindForm();
            var targetPath = DialogHelper.BrowseFolder(
                owner?.IsHandleCreated == true ? owner.Handle : IntPtr.Zero,
                Localizer.GetRequiredString("App.LinkedClient.SelectTarget"),
                _inputPath.Text);
            if (targetPath == null) return;

            var confirm = AntdUI.Modal.open(new AntdUI.Modal.Config(
                owner as AntdUI.BaseForm ?? null,
                Localizer.GetRequiredString("App.LinkedClient.ConfirmTitle"),
                Localizer.GetRequiredString("App.LinkedClient.ConfirmMessage") +
                Environment.NewLine + Environment.NewLine +
                Localizer.GetRequiredString("App.LinkedRuntime.FirstCreationTip") +
                Environment.NewLine + Environment.NewLine + targetPath,
                AntdUI.TType.Warn)
            {
                OkText = Localizer.GetRequiredString("OK"),
                CancelText = Localizer.GetRequiredString("Cancel"),
                Width = 580,
            });
            if (confirm != DialogResult.OK) return;

            SetLinkedClientControlsBusy(true);
            AntdUI.Message.loading(
                _overview,
                Localizer.GetRequiredString("App.LinkedClient.Creating"),
                async loading =>
                {
                    ArknightsLinkedClientResult result;
                    try
                    {
                        var progress = new Progress<ArknightsLinkedClientProgress>(value =>
                        {
                            loading.Text = FormatLinkedClientProgress(value, detaching: false);
                            loading.Refresh();
                        });
                        result = await ArknightsLinkedClientService
                            .CreateBilibiliClientAsync(
                                officialPath, targetPath, progress);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Create linked Arknights Bilibili client");
                        loading.Error(string.Format(
                            Localizer.GetRequiredString("App.LinkedClient.Failed"),
                            ex.Message));
                        RefreshLinkedClientControls();
                        return;
                    }

                    try
                    {
                        loading.OK(string.Format(
                            Localizer.GetRequiredString("App.LinkedClient.Success"),
                            result.LinkedFileCount,
                            result.TargetVersion));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Report linked Arknights Bilibili client success");
                    }

                    _game.RootPath = result.TargetPath;
                    _game.LocalVersion = result.TargetVersion;
                    _game.IndependentChannelClient = true;
                    _game.LinkedClientGroupId = result.LinkedClientGroupId;

                    try
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            _suppressPathAutoSave = true;
                            try
                            {
                                _inputPath.Text = result.TargetPath;
                                _inputPath.ReadOnly = true;
                                _btnBrowse.Enabled = false;
                                _btnCreateLinked.Enabled = false;
                                _btnCreateLinked.Visible = false;
                                _btnDetachLinked.Enabled = true;
                                _btnDetachLinked.Visible = true;
                                _btnReplaceLegacy.Enabled = false;
                            }
                            finally
                            {
                                _suppressPathAutoSave = false;
                            }

                            ReapplyResponsiveLayout();
                            ResetPathDisplay();
                        }
                    }
                    catch (Exception ex)
                    {
                        _suppressPathAutoSave = false;
                        LogHelper.LogError(ex, "Refresh linked Arknights Bilibili client controls");
                    }

                    try
                    {
                        _onPathChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Refresh game page after linked client creation");
                    }
                });
        }

        private void DetachLinkedBilibiliClient()
        {
            var currentConfig = ConfigHelper.Load();
            var bilibili = currentConfig.Games.Find(g =>
                g.IconName == "BiliArknights");
            var pendingDetach = currentConfig.PendingLinkedClientDetach;
            var clientPath = !string.IsNullOrWhiteSpace(
                    pendingDetach?.TargetPath)
                ? pendingDetach.TargetPath
                : bilibili?.RootPath;
            var groupId = !string.IsNullOrWhiteSpace(
                    pendingDetach?.GroupId)
                ? pendingDetach.GroupId
                : bilibili?.LinkedClientGroupId;
            if (string.IsNullOrWhiteSpace(groupId) ||
                string.IsNullOrWhiteSpace(clientPath) ||
                !Directory.Exists(clientPath))
            {
                AntdUI.Message.info(_overview, Localizer.GetRequiredString("App.LinkedClient.DetachSuccess"));
                return;
            }

            if (GameUpdateManager.Find(clientPath) != null ||
                GameRepairManager.IsRepairing(clientPath))
            {
                AntdUI.Message.warn(_overview, Localizer.GetRequiredString("App.LinkedClient.Error.GroupBusy"));
                return;
            }

            var owner = FindForm();
            var confirm = AntdUI.Modal.open(new AntdUI.Modal.Config(
                owner as AntdUI.BaseForm ?? null,
                Localizer.GetRequiredString("App.LinkedClient.DetachConfirmTitle"),
                Localizer.GetRequiredString("App.LinkedClient.DetachConfirmMessage"),
                AntdUI.TType.Warn)
            {
                OkText = Localizer.GetRequiredString("OK"),
                CancelText = Localizer.GetRequiredString("Cancel"),
                Width = 580,
            });
            if (confirm != DialogResult.OK) return;

            SetLinkedClientControlsBusy(true);
            AntdUI.Message.loading(
                _overview,
                Localizer.GetRequiredString("App.LinkedClient.Detaching"),
                async loading =>
                {
                    try
                    {
                        var progress = new Progress<ArknightsLinkedClientProgress>(value =>
                        {
                            loading.Text = FormatLinkedClientProgress(value, detaching: true);
                            loading.Refresh();
                        });
                        await ArknightsLinkedClientService.DetachSharedFilesAsync(
                            clientPath, progress);

                        ConfigHelper.Update(
                            latestConfig => LinkedClientPolicy.CompleteDetach(
                                latestConfig, groupId),
                            allowLinkedClientStateChange: true);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Detach linked Arknights client");
                        loading.Error(string.Format(
                            Localizer.GetRequiredString("App.LinkedClient.DetachFailed"),
                            ex.Message));
                        RefreshLinkedClientControls();
                        return;
                    }

                    try
                    {
                        loading.OK(Localizer.GetRequiredString("App.LinkedClient.DetachSuccess"));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Report linked Arknights client detach success");
                    }

                    _game.LinkedClientGroupId = "";

                    try
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            _inputPath.ReadOnly = false;
                            _btnBrowse.Enabled = true;
                            _btnCreateLinked.Enabled = false;
                            _btnCreateLinked.Visible = false;
                            _btnDetachLinked.Enabled = false;
                            _btnDetachLinked.Visible = false;
                            _btnReplaceLegacy.Enabled = true;
                            ReapplyResponsiveLayout();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Refresh detached Arknights client controls");
                    }

                    try
                    {
                        _onPathChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "Refresh game page after linked client detach");
                    }
                });
        }

        private void SetLinkedClientControlsBusy(bool busy)
        {
            if (IsDisposed || Disposing) return;
            if (!busy)
            {
                RefreshLinkedClientControls();
                return;
            }

            _inputPath.ReadOnly = true;
            if (_btnBrowse != null) _btnBrowse.Enabled = false;
            if (_btnCreateLinked != null) _btnCreateLinked.Enabled = false;
            if (_btnDetachLinked != null) _btnDetachLinked.Enabled = false;
            if (_btnReplaceLegacy != null) _btnReplaceLegacy.Enabled = false;
        }

        private void RefreshLinkedClientControls()
        {
            if (IsDisposed || Disposing) return;
            var config = ConfigHelper.Load();
            var bilibili = config.Games.Find(g =>
                string.Equals(g.IconName, "BiliArknights",
                    StringComparison.OrdinalIgnoreCase));
            var path = bilibili?.RootPath;
            var linked = LinkedClientPolicy.IsSharedClient(
                             "BiliArknights", path) ||
                         ArknightsLinkedClientService.HasLinkedClientMarker(
                             _inputPath.Text);
            var detachableGroupId = !string.IsNullOrWhiteSpace(
                    bilibili?.LinkedClientGroupId)
                ? bilibili.LinkedClientGroupId
                : config.PendingLinkedClientDetach?.GroupId;

            _inputPath.ReadOnly = linked;
            if (_btnBrowse != null) _btnBrowse.Enabled = !linked;
            if (_btnCreateLinked != null)
            {
                _btnCreateLinked.Enabled = false;
                _btnCreateLinked.Visible = false;
            }
            if (_btnDetachLinked != null)
            {
                _btnDetachLinked.Enabled = linked &&
                    !string.IsNullOrWhiteSpace(detachableGroupId);
                _btnDetachLinked.Visible = linked;
            }
            if (_btnReplaceLegacy != null)
                _btnReplaceLegacy.Enabled = !linked;

            ReapplyResponsiveLayout();
        }

        private void ReapplyResponsiveLayout()
        {
            if (IsDisposed || Disposing) return;
            _applyResponsiveLayout?.Invoke();
        }

        private static string FormatLinkedClientProgress(
            ArknightsLinkedClientProgress progress,
            bool detaching)
        {
            var text = detaching
                ? Localizer.GetRequiredString("App.LinkedClient.Detaching")
                : progress.Stage switch
                {
                    ArknightsLinkedClientStage.Validating => Localizer.GetRequiredString("App.LinkedClient.Stage.Validating"),
                    ArknightsLinkedClientStage.FetchingManifests => Localizer.GetRequiredString("App.LinkedClient.Stage.FetchingManifests"),
                    ArknightsLinkedClientStage.VerifyingSource => Localizer.GetRequiredString("App.LinkedClient.Stage.VerifyingSource"),
                    ArknightsLinkedClientStage.LinkingFiles => Localizer.GetRequiredString("App.LinkedClient.Stage.LinkingFiles"),
                    ArknightsLinkedClientStage.RepairingTarget => Localizer.GetRequiredString("App.LinkedClient.Stage.RepairingTarget"),
                    ArknightsLinkedClientStage.VerifyingTarget => Localizer.GetRequiredString("App.LinkedClient.Stage.VerifyingTarget"),
                    ArknightsLinkedClientStage.Finalizing => Localizer.GetRequiredString("App.LinkedClient.Stage.Finalizing"),
                    _ => Localizer.GetRequiredString("App.LinkedClient.Stage.Completed"),
                };

            if (progress.FileCount > 0)
                text += $" ({progress.FileIndex}/{progress.FileCount})";
            if (!string.IsNullOrWhiteSpace(progress.CurrentFile))
                text += Environment.NewLine + progress.CurrentFile;
            return text;
        }

        private void AutoSave(string path)
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.Name == _game.Name && g.IconName == _game.IconName);
            if (entry != null)
            {
                LinkedClientPolicy.UpdatePath(cfg, entry, path);
                ConfigHelper.Save(cfg);
            }
        }

        private void ResetPathDisplay()
        {
            if (_inputPath == null || _inputPath.IsDisposed || IsDisposed) return;

            _inputPath.SelectionLength = 0;
            _inputPath.SelectionStart = 0;
            _inputPath.Invalidate();
        }

        private void ApplyGameAccentTheme()
        {
            var palette = _gamePage?.GetCoverAccentPalette();
            var primary = palette?.Primary ?? GameTheme.GetAccent(_game.IconName);
            var hover = palette?.PrimaryHover ?? GameTheme.GetAccentHover(_game.IconName);
            var active = palette?.PrimaryActive ?? GameTheme.GetAccentActive(_game.IconName);

            AntdUI.Style.SetPrimary(primary);
            AntdUI.Style.Set(AntdUI.Colour.Primary.ToString(), primary, nameof(AntdUI.Button));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryHover.ToString(), hover, nameof(AntdUI.Button));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryActive.ToString(), active, nameof(AntdUI.Button));

            foreach (Control control in Controls)
                ApplyGameAccentTheme(control, primary, hover, active);
        }

        private static void ApplyGameAccentTheme(Control control, Color primary, Color hover, Color active)
        {
            switch (control)
            {
                case AntdUI.Button button:
                    button.BackColor = Color.Transparent;
                    button.DefaultBorderColor = Color.FromArgb(185, primary);
                    button.BackHover = hover;
                    button.BackActive = active;
                    button.ForeHover = hover;
                    button.ForeActive = active;
                    break;
                case AntdUI.Input input:
                    input.BorderHover = Color.FromArgb(165, hover);
                    input.BorderActive = hover;
                    input.SelectionColor = Color.FromArgb(70, hover);
                    break;
                case AntdUI.Switch sw:
                    sw.Fill = primary;
                    sw.FillHover = hover;
                    break;
            }

            foreach (Control child in control.Controls)
                ApplyGameAccentTheme(child, primary, hover, active);
        }

        private void BrowsePath()
        {
            var form = FindForm();
            IntPtr ownerHandle = form?.IsHandleCreated == true ? form.Handle : IntPtr.Zero;
            string path = Helpers.DialogHelper.BrowseFolder(
                ownerHandle,
                Localizer.GetRequiredString("App.Game.SelectDirTitle").Replace("{0}", _game.GetLocalizedName()),
                _inputPath.Text);
            if (path == null) return;

            _inputPath.Text = path;
            AutoSave(path);
            _onPathChanged?.Invoke();
            ResetPathDisplay();
        }

    }
}
