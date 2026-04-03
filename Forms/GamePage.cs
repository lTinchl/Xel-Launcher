using System;
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

        private AntdUI.Button btnAccountManage;
        private AntdUI.Select accountSelect;
        private AntdUI.Button GameStart;
        private AntdUI.Dropdown floatMenu;
        private AntdUI.Panel panelLaunch;

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
            bottomBar.Controls.Add(panelLaunch);
            Controls.Add(pb);
            Controls.Add(bottomBar);
            HandleCreated += (s, e) => { PositionLaunchInBar(bottomBar); UpdateLaunchPanelColor(); };
            bottomBar.SizeChanged += (s, e) => PositionLaunchInBar(bottomBar);
        }

        private void PositionLaunchInBar(System.Windows.Forms.Panel bar)
        {
            panelLaunch.Location = new Point(bar.Width - panelLaunch.Width - 16, (bar.Height - panelLaunch.Height) / 2);
        }

        private void UpdateAccountControlsVisibility()
        {
            bool hideAccounts = _game?.IconName != "Arknights";
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
            var ordered = cfg.AccountOrder.Where(id => cfg.Accounts.ContainsKey(id)).ToList();
            foreach (var id in cfg.Accounts.Keys)
                if (!ordered.Contains(id)) ordered.Add(id);
            foreach (var id in ordered)
                if (!cfg.DisabledAccounts.Contains(id))
                    accountSelect.Items.Add(new AntdUI.SelectItem("  " + cfg.Accounts[id], id));
            if (!string.IsNullOrEmpty(cfg.DefaultAccount) && !cfg.DisabledAccounts.Contains(cfg.DefaultAccount))
                accountSelect.SelectedValue = cfg.DefaultAccount;
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

        private void btnAccountManage_Click(object sender, EventArgs e)
        {
            var form = new AccountManagerForm(_overview, this);
            AntdUI.Modal.open(new AntdUI.Modal.Config(_overview, "账号管理", form)
            {
                OkText = null,
                CancelText = null,
                BtnHeight = 0,
                MaskClosable = true,
            });
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
                    GameLauncher.StartArknights(path, isEndfield);
                    config.OK("游戏启动成功");
                    if (ConfigHelper.Load().CloseAfterLaunch)
                        Invoke(new Action(() => Application.Exit()));
                }
                catch (Exception ex)
                {
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
