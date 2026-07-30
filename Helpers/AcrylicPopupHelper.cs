using System;
using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    /// <summary>
    /// Makes AntdUI's layered selection popups match the translucent surface
    /// used by the launcher notice panel.
    /// </summary>
    internal static class AcrylicPopupHelper
    {
        private const string DropdownStyleKey = nameof(AntdUI.Dropdown);
        private const string SelectStyleKey = nameof(AntdUI.Select);

        public static void Attach(AntdUI.Dropdown dropdown)
        {
            var session = new PopupSession(dropdown, DropdownStyleKey);

            dropdown.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && dropdown.SubForm() == null)
                    session.Prepare();
            };
            dropdown.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    QueuePopupConfiguration(dropdown, session, dropdown.SubForm);
            };
            dropdown.Disposed += (_, _) => session.Restore();
        }

        public static void Attach(AntdUI.Select select)
        {
            var session = new PopupSession(select, SelectStyleKey);

            select.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && select.SubForm() == null)
                    session.Prepare();
            };
            select.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    QueuePopupConfiguration(select, session, select.SubForm);
            };
            select.ExpandDropChanged += (_, e) =>
            {
                if (e.Value)
                {
                    session.Prepare();
                    session.Apply(select.SubForm());
                }
                else
                {
                    session.Restore();
                }
            };
            select.Disposed += (_, _) => session.Restore();
        }

        private static void QueuePopupConfiguration(
            Control owner,
            PopupSession session,
            Func<AntdUI.ILayeredForm> getPopup)
        {
            if (owner.IsDisposed || !owner.IsHandleCreated)
                return;

            try
            {
                owner.BeginInvoke(new Action(() => session.Apply(getPopup())));
            }
            catch (InvalidOperationException)
            {
                session.Restore();
            }
        }

        private sealed class PopupSession
        {
            private readonly Control _owner;
            private readonly string _styleKey;
            private Color _originalBackground;
            private bool _styleApplied;
            private AntdUI.ILayeredForm _popup;

            public PopupSession(Control owner, string styleKey)
            {
                _owner = owner;
                _styleKey = styleKey;
            }

            public void Prepare()
            {
                if (_styleApplied || _owner.IsDisposed)
                    return;

                _originalBackground = AntdUI.Style.Get(AntdUI.Colour.BgElevated, _styleKey);
                AntdUI.Style.Set(AntdUI.Colour.BgElevated, GetAcrylicBackground(), _styleKey);
                _styleApplied = true;
            }

            public void Apply(AntdUI.ILayeredForm popup)
            {
                if (popup == null || popup.IsDisposed || _owner.IsDisposed)
                {
                    Restore();
                    return;
                }

                Prepare();
                if (ReferenceEquals(_popup, popup))
                    return;

                _popup = popup;
                popup.Disposed += Popup_Disposed;
                // The popup's own opening animation renders the prepared acrylic
                // background. Forcing another full render here races that animation
                // and makes the first menu item's text jump into place.
            }

            public void Restore()
            {
                if (!_styleApplied)
                    return;

                AntdUI.Style.Set(AntdUI.Colour.BgElevated, _originalBackground, _styleKey);
                _styleApplied = false;
                _popup = null;
            }

            private void Popup_Disposed(object sender, EventArgs e)
            {
                if (ReferenceEquals(_popup, sender))
                    Restore();
            }

            private static Color GetAcrylicBackground()
            {
                // Keep this in sync with NoticeCarouselPanel.DrawExpanded.
                return Color.FromArgb(188, 34, 37, 43);
            }
        }
    }
}
