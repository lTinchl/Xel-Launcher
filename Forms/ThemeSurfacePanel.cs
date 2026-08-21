using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    internal sealed class ThemeSurfacePanel : Panel
    {
        private Color _surfaceColor = Color.Transparent;
        private bool _overlayScrollEnabled;
        private bool _overlayScrollVisible;
        private bool _overlayScrollDragging;
        private int _scrollContentHeight;
        private int _scrollOffset;
        private int _scrollDragOffsetY;

        public event EventHandler ScrollPositionChanged;

        public ThemeSurfacePanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SurfaceColor
        {
            get => _surfaceColor;
            set
            {
                if (_surfaceColor.ToArgb() == value.ToArgb()) return;
                _surfaceColor = value;
                base.BackColor = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool OverlayScrollEnabled
        {
            get => _overlayScrollEnabled;
            set
            {
                if (_overlayScrollEnabled == value) return;
                _overlayScrollEnabled = value;
                AutoScroll = false;
                TabStop = value;
                SetStyle(ControlStyles.Selectable, value);

                foreach (Control control in Controls)
                {
                    if (value) HookScrollChild(control);
                    else UnhookScrollChild(control);
                }

                if (!value)
                {
                    _overlayScrollVisible = false;
                    SetScrollOffset(0);
                }
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ScrollContentHeight
        {
            get => _scrollContentHeight;
            set
            {
                value = Math.Max(0, value);
                if (_scrollContentHeight == value) return;
                _scrollContentHeight = value;
                SetScrollOffset(_scrollOffset);
                if (!CanOverlayScroll)
                    _overlayScrollVisible = false;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ScrollOffset => _scrollOffset;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(_surfaceColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_overlayScrollEnabled || !_overlayScrollVisible || !CanOverlayScroll) return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var thumb = GetScrollThumbBounds();
            var color = AntdUI.Config.IsDark ? Color.White : Color.Black;
            using var brush = new SolidBrush(Color.FromArgb(_overlayScrollDragging ? 180 : 125, color));
            e.Graphics.FillRectangle(brush, thumb);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (_overlayScrollEnabled)
                HookScrollChild(e.Control);
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            if (_overlayScrollEnabled)
                UnhookScrollChild(e.Control);
            base.OnControlRemoved(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (_overlayScrollEnabled)
                SetScrollOffset(_scrollOffset);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (_overlayScrollEnabled)
            {
                ShowOverlayScrollBar();
                Focus();
            }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_overlayScrollEnabled)
                HideOverlayScrollBarIfPointerOutside();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_overlayScrollEnabled && HandleOverlayMouseWheel(e)) return;
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_overlayScrollEnabled && e.Button == MouseButtons.Left && CanOverlayScroll)
            {
                var track = GetScrollTrackBounds();
                if (track.Contains(e.Location))
                {
                    var thumb = GetScrollThumbBounds();
                    if (thumb.Contains(e.Location))
                    {
                        _scrollDragOffsetY = e.Y - thumb.Y;
                    }
                    else
                    {
                        _scrollDragOffsetY = thumb.Height / 2;
                        ScrollToThumbTop(e.Y - _scrollDragOffsetY);
                    }

                    _overlayScrollDragging = true;
                    Capture = true;
                    ShowOverlayScrollBar();
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_overlayScrollDragging)
            {
                ScrollToThumbTop(e.Y - _scrollDragOffsetY);
                return;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_overlayScrollDragging)
            {
                _overlayScrollDragging = false;
                Capture = false;
                Invalidate();
                HideOverlayScrollBarIfPointerOutside();
                return;
            }
            base.OnMouseUp(e);
        }

        private bool CanOverlayScroll =>
            _scrollContentHeight > Math.Max(0, ClientSize.Height - Padding.Vertical);

        private int MaxScrollOffset => Math.Max(
            0,
            _scrollContentHeight - Math.Max(0, ClientSize.Height - Padding.Vertical));

        private void SetScrollOffset(int value)
        {
            value = Math.Max(0, Math.Min(MaxScrollOffset, value));
            if (_scrollOffset == value) return;
            _scrollOffset = value;
            ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private bool HandleOverlayMouseWheel(MouseEventArgs e)
        {
            if (!CanOverlayScroll || e.Delta == 0) return false;

            int notches = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (notches == 0) notches = Math.Sign(e.Delta);
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0
                ? Math.Max(1, ClientSize.Height - Padding.Vertical)
                : Math.Max(1, lines) * Math.Max(Font.Height, 16);
            SetScrollOffset(_scrollOffset - notches * step);
            ShowOverlayScrollBar();
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;
            return true;
        }

        private Rectangle GetScrollTrackBounds()
        {
            int width = Math.Max(8, (int)Math.Round(12F * DeviceDpi / 96F));
            return new Rectangle(ClientSize.Width - width, 0, width, ClientSize.Height);
        }

        private Rectangle GetScrollThumbBounds()
        {
            var track = GetScrollTrackBounds();
            int barWidth = Math.Max(3, (int)Math.Round(4F * DeviceDpi / 96F));
            int rightInset = Math.Max(2, (int)Math.Round(2F * DeviceDpi / 96F));
            int minHeight = Math.Max(24, (int)Math.Round(32F * DeviceDpi / 96F));
            int thumbHeight = Math.Max(
                minHeight,
                (int)Math.Round(track.Height * (double)track.Height / Math.Max(track.Height, _scrollContentHeight)));
            thumbHeight = Math.Min(track.Height, thumbHeight);
            int travel = Math.Max(0, track.Height - thumbHeight);
            int top = MaxScrollOffset == 0
                ? 0
                : (int)Math.Round(travel * (double)_scrollOffset / MaxScrollOffset);
            return new Rectangle(
                track.Right - barWidth - rightInset,
                track.Y + top,
                barWidth,
                thumbHeight);
        }

        private void ScrollToThumbTop(int thumbTop)
        {
            var track = GetScrollTrackBounds();
            var thumb = GetScrollThumbBounds();
            int travel = Math.Max(1, track.Height - thumb.Height);
            int clampedTop = Math.Max(track.Top, Math.Min(track.Bottom - thumb.Height, thumbTop));
            int offset = (int)Math.Round(
                MaxScrollOffset * (double)(clampedTop - track.Top) / travel);
            SetScrollOffset(offset);
        }

        private void ShowOverlayScrollBar()
        {
            if (_overlayScrollVisible || !CanOverlayScroll) return;
            _overlayScrollVisible = true;
            Invalidate();
        }

        private void HideOverlayScrollBarIfPointerOutside()
        {
            if (!IsHandleCreated || IsDisposed || _overlayScrollDragging) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || !IsHandleCreated || _overlayScrollDragging) return;
                if (ClientRectangle.Contains(PointToClient(Cursor.Position))) return;
                _overlayScrollVisible = false;
                Invalidate();
            }));
        }

        private void HookScrollChild(Control control)
        {
            control.MouseEnter += ScrollChild_MouseEnter;
            control.MouseLeave += ScrollChild_MouseLeave;
            control.MouseWheel += ScrollChild_MouseWheel;
        }

        private void UnhookScrollChild(Control control)
        {
            control.MouseEnter -= ScrollChild_MouseEnter;
            control.MouseLeave -= ScrollChild_MouseLeave;
            control.MouseWheel -= ScrollChild_MouseWheel;
        }

        private void ScrollChild_MouseEnter(object sender, EventArgs e)
        {
            ShowOverlayScrollBar();
            Focus();
        }

        private void ScrollChild_MouseLeave(object sender, EventArgs e) =>
            HideOverlayScrollBarIfPointerOutside();

        private void ScrollChild_MouseWheel(object sender, MouseEventArgs e) =>
            HandleOverlayMouseWheel(e);
    }
}
