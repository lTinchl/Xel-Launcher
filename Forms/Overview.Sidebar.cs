using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        public void RebuildFloatMenu()
        {
            var config = ConfigHelper.Load();
            if (_currentGame == null && config.Games.Count > 0)
                SelectGame(config.Games[0]);
        }

        public void RebuildGameButtons() { }

        public void RebuildSidebar()
        {
            panelSidebarItems.Controls.Clear();
            _sidebarBtns.Clear();

            var config = ConfigHelper.Load();
            foreach (var game in config.Games)
            {
                var g = game;
                var btn = new SidebarButton();
                btn.Width = SidebarButtonWidth;
                btn.Height = SidebarButtonHeight;
                btn.Margin = new Padding(2);
                btn.Cursor = Cursors.Hand;
                btn.ShowSelectionBar = false;
                try
                {
                    var ico = GetSidebarIcon(g.IconName);
                    if (ico != null)
                    {
                        var src = ico.ToBitmap();
                        var iconSize = SidebarIconSize;
                        var dst = new System.Drawing.Bitmap(iconSize, iconSize);
                        using var g2 = System.Drawing.Graphics.FromImage(dst);
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g2.DrawImage(src, 0, 0, iconSize, iconSize);
                        btn.GameIcon = (g.IconName == "GlobalEndfield" || g.IconName == "PlayEndfield") ? ApplyRoundedCorners(dst, ScaleForDpi(10)) : dst;
                    }
                }
                catch { }
                btn.Click += (s, e) =>
                {
                    if (_suppressSidebarClick)
                    {
                        _suppressSidebarClick = false;
                        return;
                    }
                    SelectGame(g);
                };
                btn.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _dragBtn = btn;
                        _dragStartPos = e.Location;
                        _isDragging = false;
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        AntdUI.ContextMenuStrip.open(btn, it =>
                        {
                            var cfg = ConfigHelper.Load();
                            cfg.Games.RemoveAll(x => x.RootPath == g.RootPath && x.Name == g.Name);
                            ConfigHelper.Save(cfg);
                            RebuildSidebar();
                            RebuildGameButtons();
                            RebuildFloatMenu();
                        }, new AntdUI.IContextMenuStripItem[]
                        {
                            new AntdUI.ContextMenuStripItem(AntdUI.Localization.Get("App.Sidebar.Delete", "删除")).SetIcon("DeleteOutlined"),
                        });
                    }
                };
                btn.MouseMove += SidebarBtn_MouseMove;
                btn.MouseUp += SidebarBtn_MouseUp;
                _sidebarBtns.Add(btn);
                panelSidebarItems.Controls.Add(btn);
            }
            LayoutSidebarButtons(false);
            if (_currentGame != null)
                UpdateSelectedGameButton(_currentGame);
        }

        private void SidebarBtn_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragBtn == null || e.Button != MouseButtons.Left) return;
            if (!_isDragging)
            {
                if (Math.Abs(e.X - _dragStartPos.X) < 4 && Math.Abs(e.Y - _dragStartPos.Y) < 4) return;
                _isDragging = true;
            }
            var posInPanel = panelSidebarItems.PointToClient(_dragBtn.PointToScreen(e.Location));
            int targetIndex = GetDropIndex(posInPanel.Y);
            int currentIndex = panelSidebarItems.Controls.IndexOf(_dragBtn);
            if (targetIndex != currentIndex && targetIndex >= 0 && targetIndex < panelSidebarItems.Controls.Count)
            {
                StopSidebarReorderAnimation(true);
                var before = CaptureSidebarBounds();
                panelSidebarItems.Controls.SetChildIndex(_dragBtn, targetIndex);
                StartSidebarReorderAnimation(before);
            }
        }

        private void SidebarBtn_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.Button != MouseButtons.Left)
            {
                _dragBtn = null;
                _isDragging = false;
                return;
            }
            _isDragging = false;
            _suppressSidebarClick = true;
            BeginInvoke(() => _suppressSidebarClick = false);
            _dragBtn = null;
            StopSidebarReorderAnimation(true);
            var cfg = ConfigHelper.Load();
            var newOrder = new List<GameEntry>();
            foreach (Control c in panelSidebarItems.Controls)
            {
                int idx = _sidebarBtns.IndexOf(c as SidebarButton);
                if (idx >= 0 && idx < cfg.Games.Count)
                    newOrder.Add(cfg.Games[idx]);
            }
            if (newOrder.Count == cfg.Games.Count)
            {
                cfg.Games = newOrder;
                ConfigHelper.Save(cfg);
            }
            _sidebarBtns.Clear();
            foreach (Control c in panelSidebarItems.Controls)
            {
                if (c is SidebarButton btn)
                    _sidebarBtns.Add(btn);
            }
        }

        private int GetDropIndex(int y)
        {
            int count = panelSidebarItems.Controls.Count;
            int virtualY = y - panelSidebarItems.AutoScrollPosition.Y;
            int step = SidebarButtonHeight + SidebarButtonGap;

            for (int i = 0; i < count; i++)
            {
                int mid = SidebarButtonTop + i * step + SidebarButtonHeight / 2;
                if (virtualY < mid) return i;
            }
            return count - 1;
        }

        private void LayoutSidebarButtons(bool invalidate)
        {
            int count = panelSidebarItems.Controls.Count;
            int contentHeight = SidebarButtonTop + count * (SidebarButtonHeight + SidebarButtonGap) + SidebarButtonTop;
            panelSidebarItems.AutoScrollMinSize = new System.Drawing.Size(0, contentHeight);

            for (int i = 0; i < count; i++)
            {
                var control = panelSidebarItems.Controls[i];
                control.Bounds = GetSidebarButtonBounds(i);
                if (invalidate) control.Invalidate();
            }

            if (invalidate)
                panelSidebarItems.Invalidate();

            PositionSidebarSelectionIndicator(false);
        }

        private System.Drawing.Rectangle GetSidebarButtonBounds(int index)
        {
            int x = Math.Max(panelSidebarItems.Padding.Left, (panelSidebarItems.ClientSize.Width - SidebarButtonWidth) / 2);
            int y = SidebarButtonTop + index * (SidebarButtonHeight + SidebarButtonGap) + panelSidebarItems.AutoScrollPosition.Y;
            return new System.Drawing.Rectangle(x, y, SidebarButtonWidth, SidebarButtonHeight);
        }

        private Dictionary<Control, System.Drawing.Rectangle> CaptureSidebarBounds()
        {
            var bounds = new Dictionary<Control, System.Drawing.Rectangle>();
            foreach (Control control in panelSidebarItems.Controls)
                bounds[control] = control.Bounds;
            return bounds;
        }

        private void StartSidebarReorderAnimation(Dictionary<Control, System.Drawing.Rectangle> before)
        {
            LayoutSidebarButtons(false);

            _sidebarAnimFrom = new Dictionary<Control, System.Drawing.Rectangle>();
            _sidebarAnimTo = new Dictionary<Control, System.Drawing.Rectangle>();

            foreach (Control control in panelSidebarItems.Controls)
            {
                if (ReferenceEquals(control, _dragBtn)) continue;
                if (!before.TryGetValue(control, out var oldBounds)) continue;

                var newBounds = control.Bounds;
                if (oldBounds.Location == newBounds.Location) continue;

                _sidebarAnimFrom[control] = oldBounds;
                _sidebarAnimTo[control] = newBounds;
            }

            if (_sidebarAnimTo.Count == 0) return;

            foreach (var pair in _sidebarAnimFrom)
            {
                if (!pair.Key.IsDisposed)
                {
                    pair.Key.Bounds = pair.Value;
                    pair.Key.Invalidate();
                }
            }
            panelSidebarItems.Invalidate();

            _sidebarReorderWatch = Stopwatch.StartNew();
            _sidebarReorderTimer = new Timer { Interval = AnimationFrameHelper.GetFrameInterval(panelSidebarItems) };
            _sidebarReorderTimer.Tick += SidebarReorderTimer_Tick;
            _sidebarReorderTimer.Start();
        }

        private void SidebarReorderTimer_Tick(object sender, EventArgs e)
        {
            const double duration = 130D;
            if (_sidebarReorderWatch == null) return;

            double progress = Math.Min(1D, _sidebarReorderWatch.Elapsed.TotalMilliseconds / duration);
            double eased = 1D - Math.Pow(1D - progress, 3D);

            foreach (var pair in _sidebarAnimTo.ToArray())
            {
                var control = pair.Key;
                if (control.IsDisposed || !_sidebarAnimFrom.TryGetValue(control, out var from)) continue;

                var to = pair.Value;
                int x = from.X + (int)Math.Round((to.X - from.X) * eased);
                int y = from.Y + (int)Math.Round((to.Y - from.Y) * eased);
                control.Location = new System.Drawing.Point(x, y);
                control.Invalidate();
            }
            panelSidebarItems.Invalidate();

            if (progress < 1D) return;

            StopSidebarReorderAnimation(true);
        }

        private void StopSidebarReorderAnimation(bool finish)
        {
            if (_sidebarReorderTimer != null)
            {
                _sidebarReorderTimer.Stop();
                _sidebarReorderTimer.Tick -= SidebarReorderTimer_Tick;
                _sidebarReorderTimer.Dispose();
                _sidebarReorderTimer = null;
            }

            _sidebarReorderWatch?.Stop();
            _sidebarReorderWatch = null;

            if (finish)
            {
                foreach (var pair in _sidebarAnimTo.ToArray())
                {
                    if (!pair.Key.IsDisposed)
                        pair.Key.Bounds = pair.Value;
                }
            }

            _sidebarAnimFrom.Clear();
            _sidebarAnimTo.Clear();

            if (finish)
                LayoutSidebarButtons(false);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        private void PositionSidebarSelectionIndicator(bool animate)
        {
            var active = _sidebarBtns.FirstOrDefault(x => x.Selected && !x.IsDisposed);
            if (active == null ||
                active.Parent == null ||
                !active.Parent.IsHandleCreated ||
                panelSidebar == null ||
                panelSidebar.IsDisposed ||
                !panelSidebar.IsHandleCreated ||
                _sidebarSelectionIndicator == null ||
                _sidebarSelectionIndicator.IsDisposed)
            {
                _sidebarSelectionInitialized = false;
                _sidebarSelectionTimer?.Stop();
                if (_sidebarSelectionIndicator != null) _sidebarSelectionIndicator.Visible = false;
                return;
            }

            var point = panelSidebar.PointToClient(active.Parent.PointToScreen(new System.Drawing.Point(
                active.Left + 6,
                active.Top + (int)Math.Round((active.Height - SidebarIconSize) / 2F))));
            _sidebarSelectionTarget = new System.Drawing.RectangleF(
                point.X,
                point.Y,
                ScaleForDpi(4),
                SidebarIconSize);
            _sidebarSelectionIndicator.AccentColor = _sidebarSelectionColor;
            _sidebarSelectionIndicator.Visible = true;
            _sidebarSelectionIndicator.BringToFront();

            if (!_sidebarSelectionInitialized || !animate)
            {
                _sidebarSelectionBounds = _sidebarSelectionTarget;
                _sidebarSelectionInitialized = true;
                _sidebarSelectionTimer?.Stop();
                ApplySidebarSelectionIndicatorBounds();
                return;
            }

            if (NearlySame(_sidebarSelectionBounds, _sidebarSelectionTarget, 0.5F))
            {
                _sidebarSelectionBounds = _sidebarSelectionTarget;
                ApplySidebarSelectionIndicatorBounds();
                return;
            }

            if (_sidebarSelectionTimer == null)
            {
                _sidebarSelectionTimer = new Timer { Interval = AnimationFrameHelper.GetFrameInterval(panelSidebar) };
                _sidebarSelectionTimer.Tick += SidebarSelectionTimer_Tick;
            }

            AnimationFrameHelper.ApplyFrameInterval(_sidebarSelectionTimer, panelSidebar);
            if (!_sidebarSelectionTimer.Enabled)
                _sidebarSelectionTimer.Start();
        }

        private void SidebarSelectionTimer_Tick(object sender, EventArgs e)
        {
            _sidebarSelectionBounds = new System.Drawing.RectangleF(
                Ease(_sidebarSelectionBounds.X, _sidebarSelectionTarget.X, _sidebarSelectionTimer),
                Ease(_sidebarSelectionBounds.Y, _sidebarSelectionTarget.Y, _sidebarSelectionTimer),
                Ease(_sidebarSelectionBounds.Width, _sidebarSelectionTarget.Width, _sidebarSelectionTimer),
                Ease(_sidebarSelectionBounds.Height, _sidebarSelectionTarget.Height, _sidebarSelectionTimer));

            if (NearlySame(_sidebarSelectionBounds, _sidebarSelectionTarget, 0.5F))
            {
                _sidebarSelectionBounds = _sidebarSelectionTarget;
                _sidebarSelectionTimer?.Stop();
            }

            ApplySidebarSelectionIndicatorBounds();
        }

        private void ApplySidebarSelectionIndicatorBounds()
        {
            if (_sidebarSelectionIndicator == null ||
                _sidebarSelectionIndicator.IsDisposed ||
                _sidebarSelectionIndicator.Parent == null ||
                panelSidebar == null ||
                panelSidebar.IsDisposed)
            {
                _sidebarSelectionInitialized = false;
                _sidebarSelectionTimer?.Stop();
                return;
            }

            _sidebarSelectionIndicator.Bounds = System.Drawing.Rectangle.Round(_sidebarSelectionBounds);
            _sidebarSelectionIndicator.Invalidate();
        }

        private static float Ease(float current, float target, Timer timer) =>
            current + (target - current) * AnimationFrameHelper.ScaleEase(0.28F, timer);

        private static bool NearlySame(System.Drawing.RectangleF a, System.Drawing.RectangleF b, float tolerance) =>
            Math.Abs(a.X - b.X) <= tolerance &&
            Math.Abs(a.Y - b.Y) <= tolerance &&
            Math.Abs(a.Width - b.Width) <= tolerance &&
            Math.Abs(a.Height - b.Height) <= tolerance;

        private static System.Drawing.Bitmap ApplyRoundedCorners(System.Drawing.Bitmap src, int radius)
        {
            var dst = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(dst);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            var rect = new System.Drawing.Rectangle(0, 0, src.Width, src.Height);
            int d = radius * 2;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.SetClip(path);
            g.DrawImage(src, rect);
            return dst;
        }

        private System.Drawing.Icon GetSidebarIcon(string iconName)
        {
            try
            {
                string basePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Icon");
                string file = iconName switch
                {
                    "Arknights" => "Arknights.ico",
                    "BiliArknights" => "BiliArknights.ico",
                    "Endfield" => "Endfield.ico",
                    "BiliEndfield" => "BiliEndfield.ico",
                    "GlobalEndfield" => "GlobalEndfield.ico",
                    "PlayEndfield" => "PlayEndfield.ico",
                    "official" => "official.ico",
                    _ => null
                };
                if (file == null) return null;
                string fullPath = System.IO.Path.Combine(basePath, file);
                if (!System.IO.File.Exists(fullPath))
                    fullPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", file);
                if (!System.IO.File.Exists(fullPath)) return null;
                return new System.Drawing.Icon(fullPath, new System.Drawing.Size(256, 256));
            }
            catch { return null; }
        }

        private void btnSidebarManage_Click(object sender, EventArgs e)
        {
            var picker = new GameIconPickerForm(this);
            AntdUI.Drawer.open(this, picker, AntdUI.TAlignMini.Bottom);
        }
    }
}
