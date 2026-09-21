using System;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private void InitTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.icon,
                Text = "Xel Launcher",
                Visible = false,
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add(Localizer.GetRequiredString("App.Tray.Show"), null, (s, e) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localizer.GetRequiredString("App.Tray.Exit"), null, (s, e) => { _forceClose = true; _trayIcon.Visible = false; Application.Exit(); });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _trayIcon.Visible = false;
        }

        public void HideToTray()
        {
            Hide();
            _trayIcon.Visible = true;
            Program.TrimMemory();
        }

        public void ShowFromTray()
        {
            if (!IsHandleCreated) return;
            Invoke(new Action(() =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
                _trayIcon.Visible = false;
            }));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (WindowState == FormWindowState.Minimized)
            {
                if (_minimizeToTray)
                {
                    Hide();
                    _trayIcon.Visible = true;
                }
                
                Program.TrimMemory();
            }
        }
    }
}
