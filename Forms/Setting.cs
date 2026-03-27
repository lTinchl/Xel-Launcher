using System;
using Microsoft.Win32;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher
{
    public partial class Setting : UserControl
    {
        AntdUI.BaseForm form;
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "Xel Launcher";

        public bool Animation, ShadowEnabled, ShowInWindow, ScrollBarHide, TextRenderingHighQuality, MinimizeToTray, StartWithWindows, CloseAfterLaunch;

        public Setting(AntdUI.BaseForm _form)
        {
            form = _form;
            InitializeComponent();
            btnSoftware.Click += (s, e) => ShowPanel(false);
            btnLog.Click += (s, e) => ShowPanel(true);
            LogHelper.OnLog += () => Invoke(new Action(RefreshLog));

            switch1.Checked = Animation = AntdUI.Config.Animation;
            switch2.Checked = ShadowEnabled = AntdUI.Config.ShadowEnabled;
            switch3.Checked = ShowInWindow = AntdUI.Config.ShowInWindow;
            switch4.Checked = ScrollBarHide = AntdUI.Config.ScrollBarHide;
            switch5.Checked = TextRenderingHighQuality = AntdUI.Config.TextRenderingHighQuality;
            switch6.Checked = MinimizeToTray = ConfigHelper.Load().MinimizeToTray;
            switch7.Checked = StartWithWindows = GetStartWithWindows();
            switch8.Checked = CloseAfterLaunch = ConfigHelper.Load().CloseAfterLaunch;

            switch1.CheckedChanged += (s, e) => { Animation = e.Value; };
            switch2.CheckedChanged += (s, e) => { ShadowEnabled = e.Value; };
            switch3.CheckedChanged += (s, e) => { ShowInWindow = e.Value; };
            switch4.CheckedChanged += (s, e) => { ScrollBarHide = e.Value; };
            switch5.CheckedChanged += (s, e) => { TextRenderingHighQuality = e.Value; };
            switch6.CheckedChanged += (s, e) => { MinimizeToTray = e.Value; };
            switch7.CheckedChanged += (s, e) => { StartWithWindows = e.Value; };
            switch8.CheckedChanged += (s, e) => { CloseAfterLaunch = e.Value; };
        }

        private static bool GetStartWithWindows()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }

        public static void ApplyStartWithWindows(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (enable)
                key.SetValue(AppName, System.Windows.Forms.Application.ExecutablePath);
            else
                key.DeleteValue(AppName, false);
        }

        private void ShowPanel(bool showLog)
        {
            tableSoftware.Visible = !showLog;
            panelLog.Visible = showLog;
            btnSoftware.Type = showLog ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
            btnLog.Type = showLog ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            if (showLog) RefreshLog();
        }

        private void RefreshLog()
        {
            txtLog.Text = LogHelper.GetAll();
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }
    }
}
