using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private const int WmSysCommand = 0x0112;
        private const int WmClose = 0x0010;
        private const int WmLButtonUp = 0x0202;
        private const int ScClose = 0xF060;

        private CloseCommandMessageFilter _closeCommandMessageFilter;
        private bool _applicationExitRequested;
        private bool _closingOwnedOverlays;

        private void InstallCloseCommandFilter()
        {
            _closeCommandMessageFilter ??= new CloseCommandMessageFilter(this);
            Application.AddMessageFilter(_closeCommandMessageFilter);
        }

        private void RemoveCloseCommandFilter()
        {
            if (_closeCommandMessageFilter == null) return;

            Application.RemoveMessageFilter(_closeCommandMessageFilter);
            _closeCommandMessageFilter = null;
        }

        private void RequestApplicationExit()
        {
            if (_applicationExitRequested) return;

            _applicationExitRequested = true;
            _forceClose = true;

            if (IsDisposed)
                return;

            void Exit()
            {
                if (_trayIcon != null) _trayIcon.Visible = false;
                CloseOwnedOverlayForms();
                Close();
            }

            if (InvokeRequired) BeginInvoke((Action)Exit);
            else Exit();
        }

        private bool IsOwnedByOverview(Form form)
        {
            for (var owner = form.Owner; owner != null; owner = owner.Owner)
            {
                if (ReferenceEquals(owner, this)) return true;
            }

            return false;
        }

        private bool HasOwnedOverlayForms()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (IsOwnedOverlayForm(form)) return true;
            }

            return false;
        }

        private bool IsMouseOverWindowCloseButton()
        {
            if (windowBar == null || windowBar.IsDisposed || !windowBar.IsHandleCreated) return false;

            var location = windowBar.PointToScreen(Point.Empty);
            var buttonWidth = Math.Max(windowBar.Height, 46);
            var closeRect = new Rectangle(
                location.X + windowBar.Width - buttonWidth,
                location.Y,
                buttonWidth,
                windowBar.Height);

            return closeRect.Contains(Control.MousePosition);
        }

        private void CloseOwnedOverlayForms()
        {
            if (_closingOwnedOverlays) return;

            _closingOwnedOverlays = true;
            var forms = new List<Form>();
            try
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (IsOwnedOverlayForm(form))
                        forms.Add(form);
                }

                forms.Sort((a, b) => GetOwnerDepth(b).CompareTo(GetOwnerDepth(a)));

                foreach (var form in forms)
                {
                    try
                    {
                        if (form.IsDisposed) continue;
                        form.Close();
                    }
                    catch
                    {
                        // Best effort cleanup while the app itself is exiting.
                    }
                }
            }
            finally
            {
                _closingOwnedOverlays = false;
            }
        }

        private static int GetOwnerDepth(Form form)
        {
            var depth = 0;
            for (var owner = form.Owner; owner != null; owner = owner.Owner)
                depth++;
            return depth;
        }

        private bool IsOwnedOverlayForm(Form form)
        {
            if (ReferenceEquals(form, this) || form.IsDisposed) return false;
            if (!IsOwnedByOverview(form)) return false;

            var typeName = form.GetType().Name;
            return typeName is "LayeredFormModal" or "LayeredFormDrawer" or "LayeredFormMask";
        }

        private bool ShouldTreatCloseMessageAsApplicationExit(IntPtr hwnd)
        {
            var target = Control.FromHandle(hwnd)?.FindForm();
            if (target == null) return false;

            return ReferenceEquals(target, this) && HasOwnedOverlayForms();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _forceClose = true;
            if (_trayIcon != null) _trayIcon.Visible = false;
            CloseOwnedOverlayForms();
            base.OnFormClosing(e);
        }

        private sealed class CloseCommandMessageFilter : IMessageFilter
        {
            private readonly Overview _overview;

            public CloseCommandMessageFilter(Overview overview)
            {
                _overview = overview;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (_overview.IsDisposed) return false;

                if (m.Msg == WmLButtonUp &&
                    _overview.HasOwnedOverlayForms() &&
                    _overview.IsMouseOverWindowCloseButton())
                {
                    _overview.RequestApplicationExit();
                    return true;
                }

                if (m.Msg == WmClose)
                {
                    if (!_overview.ShouldTreatCloseMessageAsApplicationExit(m.HWnd)) return false;

                    _overview.RequestApplicationExit();
                    return true;
                }

                if (m.Msg != WmSysCommand) return false;
                if (((long)m.WParam & 0xFFF0L) != ScClose) return false;
                if (!_overview.ShouldTreatCloseMessageAsApplicationExit(m.HWnd)) return false;

                _overview.RequestApplicationExit();
                return true;
            }
        }
    }
}
