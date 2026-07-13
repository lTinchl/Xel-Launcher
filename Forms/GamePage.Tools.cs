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
        private void btnArkntools_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://arkntools.app");
        }

        private void btnPrtsWiki_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://prts.wiki");
        }

        private void btnYituliu_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://ark.yituliu.cn");
        }

        private void btnEndYituliu_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://ef.yituliu.cn/");
        }

        private void btnWarfarin_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://warfarin.wiki");
        }

        private void btnEndfieldtools_Click(object sender, EventArgs e)
        {
            TabHeaderForm.Open("https://endfieldtools.dev/");
        }

        private AntdUI.Avatar CreateSubButton(System.Drawing.Icon icon, EventHandler clickHandler, string tip)
        {
            var bmp = new Bitmap(42, 42);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon.ToBitmap(), 5, 5, 32, 32);
            }
            return CreateSubButton(bmp, clickHandler, tip);
        }

        private AntdUI.Avatar CreateSubButton(Image image, EventHandler clickHandler, string tip)
        {
            var btn = new AntdUI.Avatar
            {
                Image = image,
                ImageFit = AntdUI.TFit.Cover,
                BackColor = Color.Transparent,
                BorderWidth = 0,
                Size = new Size(42, 42),
                Radius = 21,
                Round = true,
                Cursor = Cursors.Hand,
                Visible = true,
            };
            btn.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    clickHandler(s, e);
            };
            LeftTooltip().SetTip(btn, tip);
            return btn;
        }

        private AntdUI.Avatar CreateCustomToolButton(CustomToolLink link)
        {
            var image = LoadCustomToolImage(link.IconPath) ?? CreateCustomToolImage(link.Name);
            var btn = CreateSubButton(image, (s, e) => TabHeaderForm.Open(link.Url), link.Name);
            btn.Tag = link;
            btn.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowCustomToolContextMenu(btn, link);
            };
            return btn;
        }

        private void AddBuiltInToolButtons()
        {
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
            }
        }

        private AntdUI.Avatar CreateAddCustomToolButton()
        {
            return CreateSubButton(CreatePlusToolImage(), (s, e) => ShowAddCustomToolDialog(), AntdUI.Localization.Get("App.Game.CustomToolAdd", "添加自定义工具"));
        }

        private static Bitmap CreatePlusToolImage()
        {
            var bmp = new Bitmap(42, 42);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(214, 82, 96, 118));
            using var pen = new Pen(Color.White, 3.2F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.FillEllipse(bg, 1, 1, 40, 40);
            g.DrawLine(pen, 21, 12, 21, 30);
            g.DrawLine(pen, 12, 21, 30, 21);
            return bmp;
        }

        private static Bitmap CreateCustomToolImage(string name)
        {
            var bmp = new Bitmap(42, 42);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            using var bg = new LinearGradientBrush(new Rectangle(1, 1, 40, 40), Color.FromArgb(255, 74, 104, 166), Color.FromArgb(255, 42, 182, 163), 135F);
            g.FillEllipse(bg, 1, 1, 40, 40);

            var text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();
            using var font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, new RectangleF(0, 0, 42, 41), sf);
            return bmp;
        }

        private static Bitmap LoadCustomToolImage(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) return null;

            try
            {
                using var src = Image.FromFile(iconPath);
                return CreateCircularImage(src, 42);
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap CreateCircularImage(Image src, int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            using var path = new GraphicsPath();
            path.AddEllipse(1, 1, size - 2, size - 2);
            g.SetClip(path);

            var scale = Math.Max(size / (float)src.Width, size / (float)src.Height);
            var drawWidth = src.Width * scale;
            var drawHeight = src.Height * scale;
            var x = (size - drawWidth) / 2F;
            var y = (size - drawHeight) / 2F;
            g.DrawImage(src, x, y, drawWidth, drawHeight);
            g.ResetClip();
            return bmp;
        }

        private AntdUI.TooltipComponent LeftTooltip()
        {
            return _leftTooltip ??= new AntdUI.TooltipComponent
            {
                ArrowAlign = AntdUI.TAlign.Left,
                Delay = 300,
                Radius = 8,
            };
        }

        private AntdUI.TooltipComponent TopTooltip()
        {
            return _topTooltip ??= new AntdUI.TooltipComponent
            {
                ArrowAlign = AntdUI.TAlign.Top,
                Delay = 300,
                Radius = 8,
            };
        }


        private AntdUI.TooltipComponent BottonTooltip()
        {
            return _bottomTooltip ??= new AntdUI.TooltipComponent
            {
                ArrowAlign = AntdUI.TAlign.Bottom,
                Delay = 300,
                Radius = 8,
            };
        }

        private void BuildToolSidebar()
        {
            if (_subBtns.Count == 0 || _coverPictureBox == null) return;

            int item = 42;
            int pad = 8;
            _toolSidebar = new AntdUI.Panel
            {
                Size = new Size(56, pad * 2 + _subBtns.Count * item),
                Radius = 24,
                Shadow = 0,
                ShadowOpacity = 0F,
                BackColor = Color.Transparent,
                Back = Color.FromArgb(188, 34, 37, 43),
                Anchor = AnchorStyles.None,
            };

            for (int i = 0; i < _subBtns.Count; i++)
            {
                var btn = _subBtns[i];
                btn.Visible = true;
                btn.Size = new Size(42, 42);
                btn.Radius = 21;
                btn.Round = true;
                btn.BackColor = Color.Transparent;
                btn.Location = new Point(7, pad + i * item);
                _toolSidebar.Controls.Add(btn);
            }

            _coverPictureBox.Controls.Add(_toolSidebar);
            _toolSidebar.BringToFront();
        }

        private void RebuildToolSidebar()
        {
            if (_toolSidebar == null) return;

            int item = 42;
            int pad = 8;
            _toolSidebar.Size = new Size(56, pad * 2 + _subBtns.Count * item);
            _toolSidebar.Controls.Clear();
            for (int i = 0; i < _subBtns.Count; i++)
            {
                var btn = _subBtns[i];
                btn.Visible = true;
                btn.Size = new Size(42, 42);
                btn.Radius = 21;
                btn.Round = true;
                btn.BackColor = Color.Transparent;
                btn.Location = new Point(7, pad + i * item);
                _toolSidebar.Controls.Add(btn);
            }
            PositionToolSidebar();
        }

        private void LoadCustomToolButtons()
        {
            var cfg = ConfigHelper.Load();
            if (!cfg.CustomToolLinks.TryGetValue(_game.IconName, out var links) || links == null) return;

            foreach (var link in links.Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Url)))
                _subBtns.Add(CreateCustomToolButton(link));
        }

        private void ShowAddCustomToolDialog()
        {
            var form = FindForm() as AntdUI.BaseForm;
            string selectedIconPath = "";
            var nameInput = new AntdUI.Input
            {
                PlaceholderText = AntdUI.Localization.Get("App.Game.CustomToolName", "工具名称"),
                Location = new Point(0, 0),
                Size = new Size(320, 40),
                Height = 40,
            };
            var urlInput = new AntdUI.Input
            {
                PlaceholderText = AntdUI.Localization.Get("App.Game.CustomToolUrl", "https://example.com"),
                Location = new Point(0, 48),
                Size = new Size(320, 40),
                Height = 40,
            };
            var iconPreview = new PictureBox
            {
                Location = new Point(0, 98),
                Size = new Size(42, 42),
                Image = CreatePlusToolImage(),
                SizeMode = PictureBoxSizeMode.StretchImage,
            };
            var chooseIcon = new AntdUI.Button
            {
                Text = AntdUI.Localization.Get("App.Game.CustomToolChooseIcon", "选择图标"),
                Location = new Point(52, 99),
                Size = new Size(112, 40),
                Radius = 8,
                Ghost = true,
            };
            chooseIcon.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = AntdUI.Localization.Get("App.Game.CustomToolChooseIcon", "选择图标"),
                    Filter = AntdUI.Localization.Get("App.Game.CustomToolIconFilter", "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.ico|所有文件|*.*"),
                    CheckFileExists = true,
                };
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                var preview = LoadCustomToolImage(dlg.FileName);
                if (preview == null)
                {
                    AntdUI.Message.error(_overview, AntdUI.Localization.Get("App.Game.CustomToolIconInvalid", "无法读取该图片"));
                    return;
                }

                iconPreview.Image?.Dispose();
                iconPreview.Image = preview;
                selectedIconPath = dlg.FileName;
            };
            var wrap = new Panel { Size = new Size(320, 144), Padding = new Padding(0, 0, 0, 0) };
            wrap.Controls.Add(chooseIcon);
            wrap.Controls.Add(iconPreview);
            wrap.Controls.Add(urlInput);
            wrap.Controls.Add(nameInput);

            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(form, AntdUI.Localization.Get("App.Game.CustomToolAdd", "添加自定义工具"), wrap)
            {
                OkText = AntdUI.Localization.Get("OK", "确定"),
                CancelText = AntdUI.Localization.Get("Cancel", "取消"),
            });
            if (result != DialogResult.OK) return;

            var name = nameInput.Text.Trim();
            var url = NormalizeCustomToolUrl(urlInput.Text.Trim());
            if (string.IsNullOrWhiteSpace(name) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                AntdUI.Message.error(_overview, AntdUI.Localization.Get("App.Game.CustomToolInvalid", "请输入有效的名称和链接"));
                return;
            }

            var link = new CustomToolLink
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                Name = name,
                Url = url,
            };

            if (!string.IsNullOrWhiteSpace(selectedIconPath))
            {
                var savedIconPath = SaveCustomToolIcon(selectedIconPath, link.Id);
                if (string.IsNullOrWhiteSpace(savedIconPath))
                {
                    AntdUI.Message.error(_overview, AntdUI.Localization.Get("App.Game.CustomToolIconInvalid", "无法读取该图片"));
                    return;
                }
                link.IconPath = savedIconPath;
            }

            var cfg = ConfigHelper.Load();
            if (!cfg.CustomToolLinks.TryGetValue(_game.IconName, out var links) || links == null)
                cfg.CustomToolLinks[_game.IconName] = links = new List<CustomToolLink>();
            links.Add(link);
            ConfigHelper.Save(cfg);

            int insertIndex = Math.Max(0, _subBtns.Count - 1);
            _subBtns.Insert(insertIndex, CreateCustomToolButton(link));
            RebuildToolSidebar();
        }

        private void ShowCustomToolContextMenu(Control target, CustomToolLink link)
        {
            AntdUI.ContextMenuStrip.open(target, it =>
            {
                var cfg = ConfigHelper.Load();
                if (cfg.CustomToolLinks.TryGetValue(_game.IconName, out var links) && links != null)
                {
                    links.RemoveAll(x => x.Id == link.Id || (x.Name == link.Name && x.Url == link.Url));
                    ConfigHelper.Save(cfg);
                }

                RebuildCustomToolButtons();
            }, new AntdUI.IContextMenuStripItem[]
            {
                new AntdUI.ContextMenuStripItem(AntdUI.Localization.Get("App.Sidebar.Delete", "删除")).SetIcon("DeleteOutlined"),
            });
        }

        private void RebuildCustomToolButtons()
        {
            _subBtns.Clear();
            AddBuiltInToolButtons();
            LoadCustomToolButtons();
            _subBtns.Add(CreateAddCustomToolButton());
            RebuildToolSidebar();
        }

        private static string NormalizeCustomToolUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            return url.Contains("://", StringComparison.Ordinal) ? url : "https://" + url;
        }

        private static string SaveCustomToolIcon(string sourcePath, string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return "";
                using var _ = Image.FromFile(sourcePath);

                Directory.CreateDirectory(ConfigHelper.CustomToolIconDir);
                var ext = Path.GetExtension(sourcePath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                var target = Path.Combine(ConfigHelper.CustomToolIconDir, SanitizeToolFileName(id) + ext.ToLowerInvariant());
                File.Copy(sourcePath, target, true);
                return target;
            }
            catch
            {
                return "";
            }
        }

        private static string SanitizeToolFileName(string value)
        {
            var name = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void PositionToolSidebar()
        {
            if (_toolSidebar == null || _coverPictureBox == null) return;

            _toolSidebarHome = GetToolSidebarHome();
            if (_switchAnimationActive) return;

            _toolSidebar.Location = _toolSidebarHome;
            _toolSidebar.BringToFront();
        }

        private Point GetToolSidebarHome()
        {
            if (_toolSidebar == null || _coverPictureBox == null) return Point.Empty;

            int availableHeight = Math.Max(1, _coverPictureBox.Height);
            int x = _coverPictureBox.Width - _toolSidebar.Width - 22;
            int y = Math.Max(76, (availableHeight - _toolSidebar.Height) / 2);
            return new Point(Math.Max(0, x), Math.Max(0, y));
        }

        public void PrepareSwitchInStart()
        {
            if (IsDisposed) return;

            CaptureSwitchAnimationHome();
            _switchAnimationActive = true;
            _switchAnimationProgress = 1F;
            _coverPictureBox?.SetFadeProgress(0F);
            ApplySwitchAnimationOffset(_switchAnimationProgress);
        }

        public async Task PlaySwitchOutAsync()
        {
            if (IsDisposed || !IsHandleCreated) return;

            CaptureSwitchAnimationHome();
            _switchAnimationActive = true;
            await AnimateSwitchAsync(1F);
        }

        public async Task PlaySwitchInAsync()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                _switchAnimationActive = false;
                return;
            }

            CaptureSwitchAnimationHome();
            _switchAnimationActive = true;
            _switchAnimationProgress = 1F;
            ApplySwitchAnimationOffset(_switchAnimationProgress);
            await AnimateSwitchAsync(0F);
            _switchAnimationActive = false;
            _switchAnimationProgress = 0F;
            _coverPictureBox?.FinishFade();
            ApplySwitchAnimationOffset(_switchAnimationProgress);
        }

        public Image CloneCoverImageForTransition()
        {
            if (_coverImage == null) return null;

            try
            {
                return new Bitmap(_coverImage);
            }
            catch
            {
                return null;
            }
        }

        public bool HasSameCoverAs(GamePage other)
        {
            if (other == null) return false;
            if (string.IsNullOrEmpty(_coverTransitionKey) || string.IsNullOrEmpty(other._coverTransitionKey))
                return false;

            if (string.Equals(_coverTransitionKey, other._coverTransitionKey, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrEmpty(_coverTransitionSignature)
                && string.Equals(_coverTransitionSignature, other._coverTransitionSignature, StringComparison.Ordinal);
        }

        public void PrepareCoverFadeFrom(Image image)
        {
            _coverPictureBox?.PrepareFadeFrom(image);
        }

        private static string NormalizeCoverTransitionKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static Image LoadMenuIcon(string fileName)
        {
            try
            {
                string path = FindResourceFile(fileName);
                if (!File.Exists(path)) return null;

                using var icon = new Icon(path, new Size(32, 32));
                return icon.ToBitmap();
            }
            catch
            {
                return null;
            }
        }

        private static string FindResourceFile(string fileName)
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", "Icon", fileName),
                Path.Combine(Application.StartupPath, "Resources", "Icon", fileName),
                Path.Combine(Environment.CurrentDirectory, "Resources", "Icon", fileName),
                Path.Combine(AppContext.BaseDirectory, "Resources", fileName),
                Path.Combine(Application.StartupPath, "Resources", fileName),
                Path.Combine(Environment.CurrentDirectory, "Resources", fileName),
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, "Resources", "Icon", fileName));
                candidates.Add(Path.Combine(dir.FullName, "Resources", fileName));
            }

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private static string CreateCoverTransitionSignature(Image image)
        {
            if (image == null) return "";

            try
            {
                var bitmap = image as Bitmap;
                bool ownsBitmap = bitmap == null;
                bitmap ??= new Bitmap(image);
                try
                {
                unchecked
                {
                    uint hash = 2166136261;
                    void Add(int value)
                    {
                        hash ^= (uint)value;
                        hash *= 16777619;
                    }

                    Add(bitmap.Width);
                    Add(bitmap.Height);
                    for (int y = 0; y < 4; y++)
                    {
                        int py = bitmap.Height <= 1 ? 0 : (int)Math.Round(y * (bitmap.Height - 1) / 3F);
                        for (int x = 0; x < 4; x++)
                        {
                            int px = bitmap.Width <= 1 ? 0 : (int)Math.Round(x * (bitmap.Width - 1) / 3F);
                            Add(bitmap.GetPixel(px, py).ToArgb());
                        }
                    }

                    return $"{bitmap.Width}x{bitmap.Height}:{hash:X8}";
                }
                }
                finally
                {
                    if (ownsBitmap) bitmap.Dispose();
                }
            }
            catch
            {
                return "";
            }
        }

        private void CaptureSwitchAnimationHome()
        {
            if (_coverPictureBox == null) return;

            _launchPanelHome = GetLaunchPanelHome();
            _noticePanelHome = GetNoticePanelHome(_launchPanelHome);
            _toolSidebarHome = GetToolSidebarHome();
        }

        private Task AnimateSwitchAsync(float target)
        {
            var tcs = new TaskCompletionSource<object>();
            if (IsDisposed || !IsHandleCreated)
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            float start = _switchAnimationProgress;
            var stopwatch = Stopwatch.StartNew();
            var timer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(
                    SwitchAnimationMinFrameIntervalMs,
                    AnimationFrameHelper.GetFrameInterval(this))
            };
            timer.Tick += (s, e) =>
            {
                if (IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    tcs.TrySetResult(null);
                    return;
                }

                float elapsedProgress = Math.Min(1F, (float)stopwatch.Elapsed.TotalMilliseconds / SwitchAnimationDurationMs);
                float easedProgress = elapsedProgress * elapsedProgress * (3F - 2F * elapsedProgress);
                _switchAnimationProgress = start + (target - start) * easedProgress;
                if (target < _switchAnimationProgress)
                    _coverPictureBox?.SetFadeProgress(1F - _switchAnimationProgress);

                if (elapsedProgress >= 1F)
                {
                    _switchAnimationProgress = target;
                    if (target <= 0F)
                        _coverPictureBox?.SetFadeProgress(1F);
                }

                ApplySwitchAnimationOffset(_switchAnimationProgress);

                if (elapsedProgress >= 1F)
                {
                    stopwatch.Stop();
                    timer.Stop();
                    timer.Dispose();
                    tcs.TrySetResult(null);
                }
            };
            timer.Start();
            return tcs.Task;
        }

        private void ApplySwitchAnimationOffset(float progress)
        {
            int verticalOffset = (int)Math.Round(Math.Max(180, Height / 4F) * progress);
            int horizontalOffset = (int)Math.Round(Math.Max(120, Width / 5F) * progress);

            if (panelLaunch != null)
            {
                panelLaunch.Location = new Point(_launchPanelHome.X, _launchPanelHome.Y + verticalOffset);
            }

            if (_noticePanel != null)
            {
                _noticePanel.Bounds = new Rectangle(
                    _noticePanelHome.X,
                    _noticePanelHome.Y + verticalOffset,
                    _noticePanelHome.Width,
                    _noticePanelHome.Height);
            }

            if (_toolSidebar != null)
            {
                _toolSidebar.Location = new Point(_toolSidebarHome.X + horizontalOffset, _toolSidebarHome.Y);
            }
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
        // Toggle the child buttons from the plus button.
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
                btnArknightsWiki.BackColor = AntdUI.Config.IsDark ? AppTheme.DarkSurfaceActive : Color.White;
                foreach (var btn in _subBtns) btn.Visible = false;
            }
        }

        // Position the floating wiki button.
        private void PositionAddBtnInBar(System.Windows.Forms.Panel bar)
        {
            btnArknightsWiki.Location = new Point(16, (bar.Height - btnArknightsWiki.Height) / 2);
        }
    }
}
