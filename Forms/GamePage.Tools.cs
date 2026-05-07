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
            var btn = new AntdUI.Avatar
            {
                Image = bmp,
                ImageFit = AntdUI.TFit.Cover,
                BackColor = Color.Transparent,
                BorderWidth = 0,
                Size = new Size(42, 42),
                Radius = 21,
                Round = true,
                Cursor = Cursors.Hand,
                Visible = true,
            };
            btn.Click += clickHandler;
            EnsureToolTooltip().SetTip(btn, tip);
            return btn;
        }

        private AntdUI.TooltipComponent EnsureToolTooltip()
        {
            return _toolTooltip ??= new AntdUI.TooltipComponent
            {
                ArrowAlign = AntdUI.TAlign.Left,
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
            await AnimateSwitchAsync(0F, 1F);
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
            await AnimateSwitchAsync(1F, 0F);
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
                Path.Combine(AppContext.BaseDirectory, "Resources", fileName),
                Path.Combine(Application.StartupPath, "Resources", fileName),
                Path.Combine(Environment.CurrentDirectory, "Resources", fileName),
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                candidates.Add(Path.Combine(dir.FullName, "Resources", fileName));

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private static string CreateCoverTransitionSignature(Image image)
        {
            if (image == null) return "";

            try
            {
                using var bitmap = new Bitmap(image);
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

        private Task AnimateSwitchAsync(float from, float to)
        {
            var tcs = new TaskCompletionSource<object>();
            if (IsDisposed || !IsHandleCreated)
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            var stopwatch = Stopwatch.StartNew();
            var timer = new System.Windows.Forms.Timer { Interval = 8 };
            timer.Tick += (s, e) =>
            {
                if (IsDisposed)
                {
                    stopwatch.Stop();
                    timer.Stop();
                    timer.Dispose();
                    tcs.TrySetResult(null);
                    return;
                }

                float elapsed = stopwatch.ElapsedMilliseconds;
                float progress = Math.Min(1F, elapsed / SwitchAnimationDuration);
                _switchAnimationProgress = from + (to - from) * progress;
                if (from > to)
                    _coverPictureBox?.SetFadeProgress(1F - _switchAnimationProgress);
                ApplySwitchAnimationOffset(_switchAnimationProgress);

                if (progress >= 1F)
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
