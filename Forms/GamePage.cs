using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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

        private bool _accountExpanded = false;
        private readonly List<AntdUI.Button> _subBtns = new();

        public GamePage(GameEntry game, Overview overview)
        {
            _game = game;
            _overview = overview;
            Dock = DockStyle.Fill;

            BuildLaunchPanel();
            BuildCoverImage();

            LoadAccountSelect();
            UpdateAccountControlsVisibility();
        }

        private void BuildLaunchPanel()
        {
            btnArknightsWiki = new AntdUI.Button
            {
                IconSvg = "PlusOutlined",
                Type = AntdUI.TTypeMini.Default,
                BackColor = Color.White,
                Size = new Size(52, 52),
                Location = new Point(0, 0),
                BorderWidth = 0,
                Radius = 26,
                WaveSize = 4,
            };

            var tooltip = new AntdUI.TooltipComponent();
            tooltip.SetTip(btnArknightsWiki, "小工具");
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
                PlaceholderText = "  选择账号",
                Font = new Font("Microsoft YaHei UI", 11F),
                DropDownRadius = 8,
                Placement = AntdUI.TAlignFrom.TL,
            };

            GameStart = new AntdUI.Button
            {
                BackExtend = "135, #6253E1, #04BEFE",
                IconSvg = "PoweroffOutlined",
                Text = "开始游戏",
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
            floatMenu.Items.Add(new AntdUI.SelectItem("游戏设置", "setting").SetIcon("SettingOutlined"));
            floatMenu.SelectedValueChanged += (s, e) =>
            {
                if (e.Value is string v && v == "setting")
                {
                    BeginInvoke(() =>
                    {
                        floatMenu.SelectedValue = null;
                        AntdUI.Drawer.open(_overview, new GameSettingForm(_game, _overview), AntdUI.TAlignMini.Right);
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

            Controls.Add(panelLaunch);
        }

        private void BuildCoverImage()
        {
            string imgFile = _game.IconName switch
            {
                "Endfield" or "BiliEndfield" or "GlobalEndfield" => "End.jpg",
                _ => "Arknights.jpg",
            };
            string imgPath = Path.Combine(AppContext.BaseDirectory, "Resources", imgFile);
            if (!File.Exists(imgPath)) return;

            var img = Image.FromFile(imgPath);
            var pb = new CoverPictureBox
            {
                Dock = DockStyle.Fill,
                Image = img,
            };
            // 底部留出按钮行高度
            var bottomBar = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
            };

            // 将按钮控件从 panelLaunch 移到 bottomBar
            // 子按钮紧靠 btnArknightsWiki 右侧展开，放在 bottomBar 里
            switch (_game.IconName)
            {
                case "Arknights":
                case "BiliArknights":
                    _subBtns.Add(CreateSubButton(Properties.Resources.Arknights_Toolbox, btnArkntools_Click));
                    _subBtns.Add(CreateSubButton(Properties.Resources.PRTS_WIKI, btnPrtsWiki_Click));
                    _subBtns.Add(CreateSubButton(Properties.Resources.Arknights_Yituliu, btnYituliu_Click));
                    break;
                case "Endfield":
                case "BiliEndfield":
                    _subBtns.Add(CreateSubButton(Properties.Resources.End_Yituliu, btnEndYituliu_Click));
                    _subBtns.Add(CreateSubButton(Properties.Resources.warfarin, btnWarfarin_Click));
                    break;
                case "GlobalEndfield":
                    _subBtns.Add(CreateSubButton(Properties.Resources.endfieldtools, btnEndfieldtools_Click));
                    break;
                default:
                    btnArknightsWiki.Visible = false;
                    break;
            }

            bottomBar.Controls.Add(panelLaunch);
            bottomBar.Controls.Add(btnArknightsWiki);
            foreach (var btn in _subBtns)
                bottomBar.Controls.Add(btn);
            Controls.Add(pb);
            Controls.Add(bottomBar);
            HandleCreated += (s, e) => {
                PositionLaunchInBar(bottomBar);
                PositionAddBtnInBar(bottomBar);
                PositionSubButtons();
                UpdateLaunchPanelColor();
                };
            bottomBar.SizeChanged += (s, e) =>
            {
                PositionLaunchInBar(bottomBar);
                PositionAddBtnInBar(bottomBar);
                PositionSubButtons();
            };
        }

        private void PositionLaunchInBar(System.Windows.Forms.Panel bar)
        {
            panelLaunch.Location = new Point(bar.Width - panelLaunch.Width - 16, (bar.Height - panelLaunch.Height) / 2);
        }

        private bool IsAccountGame => _game?.IconName == "Arknights" || _game?.IconName == "Endfield";

        private void UpdateAccountControlsVisibility()
        {
            bool hideAccounts = !IsAccountGame;
            btnAccountManage.Visible = !hideAccounts;
            accountSelect.Visible = !hideAccounts;

            int targetWidth = hideAccounts ? 224 : 448;
            int targetGS = hideAccounts ? 0 : 224;
            int targetFM = hideAccounts ? 168 : 392;
            panelLaunch.Width = targetWidth;
            GameStart.Location = new Point(targetGS, 0);
            floatMenu.Location = new Point(targetFM, 0);
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
            if (AntdUI.Config.IsDark)
            {
                panelLaunch.BackExtend = "#3C3C48";
                panelLaunch.Back = null;
                return;
            }
            panelLaunch.BackExtend = "";
            var bg = _overview.BackColor;
            float brightness = (bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f) / 255f;
            if (brightness < 0.5f)
                panelLaunch.Back = Color.FromArgb(40, 255, 255, 255);
            else
            {
                int r = Math.Max(0, bg.R - 20);
                int g = Math.Max(0, bg.G - 20);
                int b = Math.Max(0, bg.B - 20);
                panelLaunch.Back = Color.FromArgb(80, r, g, b);
            }
        }
        //账号管理按钮调用逻辑
        private void btnAccountManage_Click(object sender, EventArgs e)
        {
            var form = new AccountManagerForm(_overview, this, _game.IconName);
            AntdUI.Modal.open(new AntdUI.Modal.Config(_overview, "账号管理", form)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
                MaskClosable = true,
            });
        }

        private void btnArkntools_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://arkntools.app").Show();
        }

        private void btnPrtsWiki_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://prts.wiki").Show();
        }

        private void btnYituliu_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://ark.yituliu.cn").Show();
        }

        private void btnEndYituliu_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://ef.yituliu.cn/").Show();
        }

        private void btnWarfarin_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://warfarin.wiki").Show();
        }

        private void btnEndfieldtools_Click(object sender, EventArgs e)
        {
            new TabHeaderForm("https://endfieldtools.dev/").Show();
        }

        private AntdUI.Button CreateSubButton(System.Drawing.Icon icon, EventHandler clickHandler)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon.ToBitmap(), 0, 0, 32, 32);
            }
            var btn = new AntdUI.Button
            {
                Icon = bmp,
                IconSize = new Size(32, 32),
                Type = AntdUI.TTypeMini.Default,
                BackColor = Color.Transparent,
                BorderWidth = 0,
                Size = new Size(32, 32),
                Radius = 22,
                WaveSize = 4,
                Visible = false,
            };
            btn.Click += clickHandler;
            return btn;
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
        //+号按钮展开/收起子按钮
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
                btnArknightsWiki.BackColor = Color.White;
                foreach (var btn in _subBtns) btn.Visible = false;
            }
        }

        //wiki悬浮按钮定位
        private void PositionAddBtnInBar(System.Windows.Forms.Panel bar)
        {
            btnArknightsWiki.Location = new Point(16, (bar.Height - btnArknightsWiki.Height) / 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _floatBtn?.Close();
                _floatBtn = null;
            }
            base.Dispose(disposing);
        }

        private void GameStart_Click(object sender, EventArgs e)
        {
            if (GameStart.Loading) return;
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

            // Endfield 三服：只要当前游戏与任意另一个 Endfield 服路径相同就执行替换
            bool isEndfield = _game.IconName == "Endfield" || _game.IconName == "BiliEndfield" || _game.IconName == "GlobalEndfield";
            bool endfieldSameRoot = false;
            if (isEndfield && !string.IsNullOrEmpty(path))
            {
                var endfieldIcons = new[] { "Endfield", "BiliEndfield", "GlobalEndfield" };
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

            string zipPath = isEndfield
                ? (endfieldSameRoot ? GameLauncher.GetPayloadZipPath(_game.IconName) : null)
                : (sameRoot ? GameLauncher.GetPayloadZipPath(_game.IconName) : null);

            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                AntdUI.Message.warn(_overview, "请先选择游戏根目录");
                Helpers.DialogHelper.InjectIcon(Properties.Resources.icon);
                using var dlg = new FolderBrowserDialog { Description = $"选择「{_game.Name}」游戏根目录", UseDescriptionForTitle = true };
                if (dlg.ShowDialog(_overview) != DialogResult.OK) return;
                path = dlg.SelectedPath;
                string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
                if (!File.Exists(Path.Combine(path, exeName)))
                {
                    AntdUI.Message.error(_overview, $"所选目录中未找到 {exeName}");
                    return;
                }
                var cfg2 = ConfigHelper.Load();
                var e2 = cfg2.Games.Find(g => g.IconName == _game.IconName);
                if (e2 != null) { e2.RootPath = path; ConfigHelper.Save(cfg2); }

                // 路径刚被更新，重新计算 sameRoot / endfieldSameRoot / zipPath
                var cfg3 = ConfigHelper.Load();
                official = cfg3.Games.Find(g => g.IconName == "Arknights");
                bilibili = cfg3.Games.Find(g => g.IconName == "BiliArknights");
                sameRoot = official != null && bilibili != null &&
                    !string.IsNullOrEmpty(official.RootPath) && !string.IsNullOrEmpty(bilibili.RootPath) &&
                    Path.GetFullPath(official.RootPath).Equals(Path.GetFullPath(bilibili.RootPath), StringComparison.OrdinalIgnoreCase);

                endfieldSameRoot = false;
                if (isEndfield)
                {
                    var endfieldIcons2 = new[] { "Endfield", "BiliEndfield", "GlobalEndfield" };
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

                zipPath = isEndfield
                    ? (endfieldSameRoot ? GameLauncher.GetPayloadZipPath(_game.IconName) : null)
                    : (sameRoot ? GameLauncher.GetPayloadZipPath(_game.IconName) : null);
            }

            GameStart.LoadingWaveValue = 0;
            GameStart.Loading = true;
            AntdUI.Message.loading(_overview, "加载中...", async (config) =>
            {
                try
                {
                    if (_game.IconName == "Arknights")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = "切换账号中...";
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreAccount(selectedAccountId);
                        }
                    }
                    else if (_game.IconName == "Endfield")
                    {
                        string selectedAccountId = accountSelect.SelectedValue as string;
                        if (!string.IsNullOrEmpty(selectedAccountId))
                        {
                            config.Text = "切换账号中...";
                            config.Refresh();
                            await Helpers.GameLauncher.RestoreEndfieldAccount(selectedAccountId);
                        }
                    }
                    if ((sameRoot || endfieldSameRoot) && zipPath != null)
                    {
                        await GameLauncher.ExtractAndReplace(path, zipPath, msg =>
                        {
                            config.Text = msg;
                            config.Refresh();
                        }, isEndfield);
                    }
                    for (int i = 0; i <= 100; i++)
                    {
                        GameStart.LoadingWaveValue = i / 100F;
                        System.Threading.Thread.Sleep(30);
                    }
                    GameLauncher.StartArknights(path, _game.IconName);
                    config.OK("游戏启动成功");
                    var latestCfg = ConfigHelper.Load();
                    if (latestCfg.CloseAfterLaunch)
                        Invoke(new Action(() => Application.Exit()));
                    else if (latestCfg.HideToTrayOnLaunch)
                    {
                        // 先隐藏到托盘，再后台等进程退出后恢复
                        if (IsHandleCreated)
                            Invoke(new Action(() => _overview.HideToTray()));
                        var overviewRef = _overview;
                        var iconName = _game.IconName;
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                bool isEndfield = iconName == "Endfield" || iconName == "BiliEndfield" || iconName == "GlobalEndfield";
                                string procName = isEndfield ? "Endfield" : "Arknights";
                                // 等待游戏进程出现（最多 30 秒）
                                System.Diagnostics.Process gameProc = null;
                                for (int i = 0; i < 30 && gameProc == null; i++)
                                {
                                    var procs = System.Diagnostics.Process.GetProcessesByName(procName);
                                    if (procs.Length > 0) gameProc = procs[0];
                                    else System.Threading.Thread.Sleep(1000);
                                }
                                if (gameProc != null)
                                {
                                    try
                                    {
                                        gameProc.EnableRaisingEvents = true;
                                        gameProc.WaitForExit();
                                    }
                                    catch
                                    {
                                        // 无权监听时，改为轮询进程是否还存在
                                        while (!gameProc.HasExited)
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
                Invoke(new Action(() =>
                {
                    if (!GameStart.IsDisposed) GameStart.Loading = false;
                }));
            });
        }
    }

    class CoverPictureBox : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image Image { get; set; }

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

            // cover 模式：铺满控件，居中裁剪
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
