using AntdUI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private readonly Action _onPathChanged;

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

            Font = new Font("Microsoft YaHei UI", 9F);
            var surfaceBack = AntdUI.Config.IsDark ? AppTheme.DarkBackground : Color.White;
            BackColor = surfaceBack;
            Size = LogicalSize(380, 520);
            MinimumSize = LogicalSize(340, 320);
            AutoScroll = false;

            // ── 游戏图标 ──
            var picIcon = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(48, 48),
                Location = new Point(20, 20),
            };
            try
            {
                var ico = LoadIcon(game.IconName);
                if (ico != null)
                {
                    var src = ico.ToBitmap();
                    var dst = new Bitmap(48, 48);
                    using var g = Graphics.FromImage(dst);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, 48, 48);
                    picIcon.Image = dst;
                }
            }
            catch { }

            // ── 游戏名 ──
            string displayName = game.IconName == "PlayEndfield"
                ? (AntdUI.Localization.CurrentLanguage.StartsWith("en")
                    ? "Endfield (Google)"
                    : "\u660e\u65e5\u65b9\u821f\uff1a\u7ec8\u672b\u5730(Google\u670d)")
                : game.GetLocalizedName();
            var lblName = new AntdUI.Label
            {
                Text = displayName,
                Location = new Point(80, 20),
                Size = new Size(260, 28),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            };

            // ── 游戏版本 ──
            string cachedVersion = latest?.LocalVersion ?? "";
            var lblVersion = new AntdUI.Label
            {
                Text = string.IsNullOrEmpty(cachedVersion)
                    ? AntdUI.Localization.Get("App.GameSetting.VersionChecking", "版本：N/A")
                    : AntdUI.Localization.Get("App.GameSetting.Version", "版本：") + cachedVersion,
                Location = new Point(80, 48),
                Size = new Size(260, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
            };
            HandleCreated += async (s, e) =>
            {
                try
                {
                    // Bili/Global/Play 服与官服共用游戏文件，用官服 Preset 读取本地版本
                    string svcIconName = game.IconName switch
                    {
                        "BiliEndfield" or "GlobalEndfield" or "PlayEndfield" => "Endfield",
                        "BiliArknights" => "Arknights",
                        _ => game.IconName
                    };
                    using var svc = new EndfieldService(svcIconName);
                    var status = await svc.CheckStatusAsync(currentPath).ConfigureAwait(false);
                    if (status == null || string.IsNullOrEmpty(status.LocalVersion)) return;
                    if (!lblVersion.IsDisposed)
                        BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                            lblVersion.Text = AntdUI.Localization.Get("App.GameSetting.Version", "版本：") + status.LocalVersion));
                    // 回写缓存
                    var cfgSave = ConfigHelper.Load();
                    var entry = cfgSave.Games.Find(g => g.IconName == game.IconName);
                    if (entry != null && entry.LocalVersion != status.LocalVersion)
                    {
                        entry.LocalVersion = status.LocalVersion;
                        ConfigHelper.Save(cfgSave);
                    }
                }
                catch { }
            };

            // ── 分割线 ──
            var divider1 = new AntdUI.Divider
            {
                Location = new Point(20, 82),
                Size = new Size(320, 1),
                Thickness = 1F,
            };

            // ── 游戏安装路径 标题 ──
            var lblPathSection = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.GameSetting.InstallPath", "游戏安装路径"),
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
                PlaceholderText = AntdUI.Localization.Get("App.GameSetting.PathPlaceholder", "未设置路径"),
            };
            _inputPath.TextChanged += (s, e) => AutoSave(_inputPath.Text.Trim());
            _inputPath.Leave += (s, e) => _onPathChanged?.Invoke();

            // ── 更改路径 ──
            var btnBrowse = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.GameSetting.ChangePath", "更改路径"),
                IconSvg = "FolderOpenOutlined",
                IconRatio = .58F,
                IconGap = .18F,
                Location = new Point(20, 172),
                Size = new Size(320, 36),
                Ghost = true,
            };
            btnBrowse.Click += (s, e) => BrowsePath();

            // ── 打开文件目录 ──
            var btnOpenDir = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.GameSetting.OpenDir", "打开文件目录"),
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
                var btnReplaceOfficial = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.ReplaceBili", "将文件替换为B服"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnReplaceOfficial.Click += async (s, e) =>
                {
                    string path = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    {
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetBiliPath", "请先设置B服路径"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplace", "确认替换"),
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplaceArkBili", "确定要将当前官服替换为B服吗？此操作会覆盖游戏文件"),
                        AntdUI.TType.Warn)
                    {
                        OkText = AntdUI.Localization.Get("OK", "确定"),
                        CancelText = AntdUI.Localization.Get("Cancel", "取消")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.GameSetting.Replacing", "替换中..."), async (cfg) =>
                    {
                        try
                        {
                            bool usedHardLink = false;
                            cfg.Text = AntdUI.Localization.Get("App.Switch.KillingProcess", "结束游戏进程...");
                            cfg.Refresh();
                            await GameLauncher.KillArknightsProcesses(false);
                            await GameLauncher.SwitchServerWithResult(path, "BiliArknights", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, false, r => usedHardLink = r);
                            var cfg2 = ConfigHelper.Load();
                            var BiliBili = cfg2.Games.Find(g => g.IconName == "Arknights");
                            if (BiliBili != null) BiliBili.RootPath = path;
                            ConfigHelper.Save(cfg2);
                            cfg.OK(AntdUI.Localization.Get("App.GameSetting.ReplaceSuccess", "替换成功，B服资源包已覆盖至当前目录"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                            if (!usedHardLink)
                                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.HardLinkTip", "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"));
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(AntdUI.Localization.Get("App.GameSetting.ReplaceFailed", "替换失败：") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplaceOfficial);

                var btnBili = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.BiliWebsite", "Arknights BiliBili官网"),
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
                    Text = AntdUI.Localization.Get("App.GameSetting.EndfieldWebsite", "Endfield 官网"),
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                // Website button hidden.

                var btnSync = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.SyncToAll", "同步路径到 BillBili服 / 国际服 / GooglePlay服"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnSync.Click += (s, e) =>
                {
                    string currentPath = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetOfficialPath", "请先设置官服路径"));
                        return;
                    }
                    var cfg = ConfigHelper.Load();
                    foreach (var icon in new[] { "BiliEndfield", "GlobalEndfield", "PlayEndfield" })
                    {
                        var other = cfg.Games.Find(g => g.IconName == icon);
                        if (other != null) other.RootPath = currentPath;
                    }
                    ConfigHelper.Save(cfg);
                    AntdUI.Message.success(_overview, AntdUI.Localization.Get("App.GameSetting.SyncSuccessAll", "路径已同步到 BillBili服 / 国际服 / GooglePlay服"));
                };
                Controls.Add(btnSync);
                Size = LogicalSize(360, 386);
            }
            else if (game.IconName == "BiliEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.ReplaceBili", "将文件替换为B服"),
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
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetBiliPath", "请先设置B服路径"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplace", "确认替换"),
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplaceEndBili", "确定要将当前目录替换为B服文件吗？此操作会覆盖游戏文件"),
                        AntdUI.TType.Warn)
                    {
                        OkText = AntdUI.Localization.Get("OK", "确定"),
                        CancelText = AntdUI.Localization.Get("Cancel", "取消")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.GameSetting.Replacing", "替换中..."), async (cfg) =>
                    {
                        try
                        {
                            bool usedHardLink = false;
                            cfg.Text = AntdUI.Localization.Get("App.Switch.KillingProcess", "结束游戏进程...");
                            cfg.Refresh();
                            await GameLauncher.KillArknightsProcesses(true);
                            await GameLauncher.SwitchServerWithResult(path, "BiliEndfield", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, r => usedHardLink = r);
                            cfg.OK(AntdUI.Localization.Get("App.GameSetting.ReplaceSuccess", "替换成功，B服资源包已覆盖至当前目录"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                            if (!usedHardLink)
                                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.HardLinkTip", "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"));
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(AntdUI.Localization.Get("App.GameSetting.ReplaceFailed", "替换失败：") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplace);
            }
            else if (game.IconName == "GlobalEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.ReplaceGlobal", "将文件替换为国际服"),
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
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetGlobalPath", "请先设置国际服路径"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplace", "确认替换"),
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplaceGlobal", "确定要将当前目录替换为国际服文件吗？此操作会覆盖游戏文件"),
                        AntdUI.TType.Warn)
                    {
                        OkText = AntdUI.Localization.Get("OK", "确定"),
                        CancelText = AntdUI.Localization.Get("Cancel", "取消")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.GameSetting.Replacing", "替换中..."), async (cfg) =>
                    {
                        try
                        {
                            bool usedHardLink = false;
                            cfg.Text = AntdUI.Localization.Get("App.Switch.KillingProcess", "结束游戏进程...");
                            cfg.Refresh();
                            await GameLauncher.KillArknightsProcesses(true);
                            await GameLauncher.SwitchServerWithResult(path, "GlobalEndfield", msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, r => usedHardLink = r);
                            cfg.OK(AntdUI.Localization.Get("App.GameSetting.ReplaceSuccess", "替换成功，国际服资源包已覆盖至当前目录"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                            if (!usedHardLink)
                                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.HardLinkTip", "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"));
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(AntdUI.Localization.Get("App.GameSetting.ReplaceFailed", "替换失败：") + ex.Message);
                        }
                    });
                };
                Controls.Add(btnReplace);
            }
            else if (game.IconName == "PlayEndfield")
            {
                var btnReplace = new AntdUI.Button
                {
                    Text = AntdUI.Localization.Get("App.GameSetting.ReplacePlay", "将文件替换为GooglePlay服"),
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
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetPlayPath", "请先设置GooglePlay服路径"));
                        return;
                    }
                    var result = AntdUI.Modal.open(new AntdUI.Modal.Config(
                        FindForm() as AntdUI.BaseForm ?? null,
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplace", "确认替换"),
                        AntdUI.Localization.Get("App.GameSetting.ConfirmReplacePlay", "确定要将当前目录替换为GooglePlay服文件吗？此操作会覆盖游戏文件"),
                        AntdUI.TType.Warn)
                    {
                        OkText = AntdUI.Localization.Get("OK", "确定"),
                        CancelText = AntdUI.Localization.Get("Cancel", "取消")
                    });
                    if (result != DialogResult.OK) return;

                    AntdUI.Message.loading(_overview, AntdUI.Localization.Get("App.GameSetting.Replacing", "替换中..."), async (cfg) =>
                    {
                        try
                        {
                            bool usedHardLink = false;
                            cfg.Text = AntdUI.Localization.Get("App.Switch.KillingProcess", "结束游戏进程...");
                            cfg.Refresh();
                            await GameLauncher.KillArknightsProcesses(true);
                            await GameLauncher.SwitchServerWithResult(path, game.IconName, msg =>
                            {
                                cfg.Text = msg;
                                cfg.Refresh();
                            }, true, r => usedHardLink = r);
                            cfg.OK(AntdUI.Localization.Get("App.GameSetting.ReplaceSuccess", "替换成功，GooglePlay服资源包已覆盖至当前目录"));
                            (FindForm() as AntdUI.BaseForm)?.Close();
                            if (!usedHardLink)
                                AntdUI.Message.info(_overview, AntdUI.Localization.Get("App.Game.HardLinkTip", "提示：将启动器安装到与游戏相同的磁盘分区可启用硬链接，切服速度更快"));
                        }
                        catch (Exception ex)
                        {
                            cfg.Error(AntdUI.Localization.Get("App.GameSetting.ReplaceFailed", "替换失败：") + ex.Message);
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
                    Text = AntdUI.Localization.Get("App.GameSetting.SessionToken", "Session Token"),
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
                    PlaceholderText = AntdUI.Localization.Get("App.GameSetting.TokenPlaceholder", "未设置 Token"),
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
                    Text = AntdUI.Localization.Get("App.GameSetting.AutoGetToken", "自动获取 Token"),
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
                                AntdUI.Localization.Get("App.GameSetting.TokenSuccess", "Token 获取成功"));
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
                                AntdUI.Localization.Get("App.GameSetting.TokenNotFound", "未找到 Token，请确认游戏已启动"),
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
                            AntdUI.Localization.Get("App.GameSetting.TokenNotFound", "未找到 Token，请确认游戏已启动"),
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
                    Text = AntdUI.Localization.Get("App.GameSetting.SyncToBili", "同步路径到 BillBili服"),
                    IconSvg = "CopyOutlined",
                    IconRatio = .58F,
                    IconGap = .18F,
                    Location = new Point(20, 268),
                    Size = new Size(320, 36),
                    Ghost = true,
                };
                btnSync.Click += (s, e) =>
                {
                    string currentPath = _inputPath.Text.Trim();
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        AntdUI.Message.warn(_overview, AntdUI.Localization.Get("App.GameSetting.WarnSetOfficialPath", "请先设置官服路径"));
                        return;
                    }
                    var cfg = ConfigHelper.Load();
                    var bili = cfg.Games.Find(g => g.IconName == "BiliArknights");
                    if (bili != null) bili.RootPath = currentPath;
                    ConfigHelper.Save(cfg);
                    AntdUI.Message.success(_overview, AntdUI.Localization.Get("App.GameSetting.SyncSuccess", "路径已同步到 BillBili服"));
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
                Text = AntdUI.Localization.Get("App.GameSetting.CustomSync", "自定义联动软件"),
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
                Text = AntdUI.Localization.Get("App.GameSetting.ManageSync", "管理联动软件"),
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
            bool showAccountSwitch = game.IconName != "BiliArknights" && game.IconName != "BiliEndfield";
            AntdUI.Divider divider3 = null;
            AntdUI.Switch swacmg = null;
            if (showAccountSwitch)
            {
                divider3 = new AntdUI.Divider
                {
                    Location = new Point(20, 530),
                    Size = new Size(264, 20),
                    Thickness = 1F,
                    Text = AntdUI.Localization.Get("App.GameSetting.AccountSwitch", "启用账号切换"),
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
                Text = AntdUI.Localization.Get("App.GameSetting.CustomLaunchArgs", "自定义启动参数"),
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
                PlaceholderText = AntdUI.Localization.Get("App.GameSetting.CustomLaunchArgsPlaceholder", "输入启动参数"),
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
            Controls.Add(picIcon);
            Controls.Add(lblName);
            Controls.Add(divider1);
            Controls.Add(lblVersion);
            Controls.Add(lblPathSection);
            Controls.Add(_inputPath);
            Controls.Add(btnBrowse);
            Controls.Add(btnOpenDir);
            Size = GetInitialDrawerSize();
            MinimumSize = new Size(Math.Min(320, Size.Width), Math.Min(300, Size.Height));

            int contentPanelTop = 94;
            var contentPanel = new System.Windows.Forms.Panel
            {
                Location = new Point(0, contentPanelTop),
                Size = new Size(Width, Math.Max(1, Height - contentPanelTop)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = surfaceBack,
                TabStop = false,
            };
            var contentControls = Controls
                .Cast<Control>()
                .Where(control => control != picIcon && control != lblName && control != lblVersion && control != divider1)
                .ToArray();
            foreach (var control in contentControls)
            {
                Controls.Remove(control);
                control.Top -= contentPanelTop;
                contentPanel.Controls.Add(control);
            }
            Controls.Add(contentPanel);

            ApplyGameAccentTheme();

            swExtra.CheckedChanged += (s, e) => ApplyResponsiveLayout();
            Resize += (s, e) => ApplyResponsiveLayout();
            contentPanel.Resize += (s, e) => ApplyResponsiveLayout();
            HandleCreated += (s, e) => ScheduleResponsiveLayout();
            Control drawerHost = null;
            EventHandler drawerHostResize = (s, e) =>
            {
                FitToDrawerHost();
                ScheduleResponsiveLayout();
            };
            ParentChanged += (s, e) =>
            {
                if (drawerHost != null)
                    drawerHost.Resize -= drawerHostResize;

                drawerHost = Parent;
                if (drawerHost != null)
                    drawerHost.Resize += drawerHostResize;

                FitToDrawerHost();
                ScheduleResponsiveLayout();
            };
            Disposed += (s, e) =>
            {
                if (drawerHost != null)
                    drawerHost.Resize -= drawerHostResize;
            };
            VisibleChanged += (s, e) => ScheduleResponsiveLayout();
            ApplyResponsiveLayout();

            void FitToDrawerHost()
            {
                if (IsDisposed || Parent == null) return;

                int hostHeight = Parent.ClientSize.Height;
                if (hostHeight > 0 && Height != hostHeight)
                    Height = hostHeight;
            }

            void ScheduleResponsiveLayout()
            {
                if (IsDisposed) return;
                if (IsHandleCreated) BeginInvoke((Action)(() =>
                {
                    FitToDrawerHost();
                    ApplyResponsiveLayout();
                    contentPanel.AutoScrollPosition = Point.Empty;
                }));
                else
                {
                    FitToDrawerHost();
                    ApplyResponsiveLayout();
                }
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

                picIcon.Size = new Size(56, 56);
                picIcon.Left = margin;
                picIcon.Top = 22;
                lblName.Left = picIcon.Right + 20;
                lblVersion.Left = lblName.Left;
                lblName.Top = picIcon.Top + 4;
                lblVersion.Top = lblName.Bottom + 6;
                lblName.Width = Math.Max(120, contentRight - lblName.Left);
                if (lblName.PreferredSize.Width > lblName.Width)
                    lblName.Font = new Font(lblName.Font.FontFamily, 12F, lblName.Font.Style);
                lblVersion.Width = lblName.Width;
                divider1.Left = margin;
                divider1.Top = Math.Max(picIcon.Bottom, lblVersion.Bottom) + gapMedium;
                divider1.Width = contentWidth;

                contentPanelTop = divider1.Bottom + gapSmall;
                contentPanel.Location = new Point(0, contentPanelTop);
                contentPanel.Size = new Size(ClientSize.Width, Math.Max(1, ClientSize.Height - contentPanelTop));

                lblPathSection.Left = margin;
                lblPathSection.Top = gapTiny;
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

        private void AutoSave(string path)
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.Name == _game.Name && g.IconName == _game.IconName);
            if (entry != null) { entry.RootPath = path; ConfigHelper.Save(cfg); }
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
                AntdUI.Localization.Get("App.Game.SelectDirTitle", "选择「{0}」游戏根目录").Replace("{0}", _game.GetLocalizedName()),
                _inputPath.Text);
            if (path == null) return;

            _inputPath.Text = path;
            AutoSave(path);
            _onPathChanged?.Invoke();
        }

        private static System.Drawing.Icon LoadIcon(string iconName)
        {
            try
            {
                string basePath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icon");
                string file = iconName switch
                {
                    "Arknights"      => "Arknights.ico",
                    "BiliArknights"  => "BiliArknights.ico",
                    "Endfield"       => "Endfield.ico",
                    "BiliEndfield"   => "BiliEndfield.ico",
                    "GlobalEndfield" => "GlobalEndfield.ico",
                    "PlayEndfield"   => "PlayEndfield.ico",
                    _ => null
                };
                if (file == null) return null;
                string full = Path.Combine(basePath, file);
                if (!File.Exists(full))
                    full = Path.Combine(AppContext.BaseDirectory, "Resources", file);
                return File.Exists(full) ? new System.Drawing.Icon(full, new Size(256, 256)) : null;
            }
            catch { return null; }
        }
    }
}
