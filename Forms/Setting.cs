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
        public string MaaOfficial, MaaBilibili;

        public Setting(AntdUI.BaseForm _form)
        {
            form = _form;
            InitializeComponent();
            btnSoftware.Click += (s, e) => ShowPanel(0);
            btnLog.Click += (s, e) => ShowPanel(1);
            btnMaa.Click += (s, e) => ShowPanel(2);
            LogHelper.OnLog += () => Invoke(new Action(RefreshLog));

            switch1.Checked = Animation = AntdUI.Config.Animation;
            switch2.Checked = ShadowEnabled = AntdUI.Config.ShadowEnabled;
            switch3.Checked = ShowInWindow = AntdUI.Config.ShowInWindow;
            switch4.Checked = ScrollBarHide = AntdUI.Config.ScrollBarHide;
            switch5.Checked = TextRenderingHighQuality = AntdUI.Config.TextRenderingHighQuality;
            switch6.Checked = MinimizeToTray = ConfigHelper.Load().MinimizeToTray;
            switch7.Checked = StartWithWindows = GetStartWithWindows();
            switch8.Checked = CloseAfterLaunch = ConfigHelper.Load().CloseAfterLaunch;

            var cfg = ConfigHelper.Load();
            txtMaaOfficial.Text = MaaOfficial = cfg.MAA_Official ?? "";
            txtMaaBilibili.Text = MaaBilibili = cfg.MAA_Bilibili ?? "";

            switch1.CheckedChanged += (s, e) => { Animation = e.Value; };
            switch2.CheckedChanged += (s, e) => { ShadowEnabled = e.Value; };
            switch3.CheckedChanged += (s, e) => { ShowInWindow = e.Value; };
            switch4.CheckedChanged += (s, e) => { ScrollBarHide = e.Value; };
            switch5.CheckedChanged += (s, e) => { TextRenderingHighQuality = e.Value; };
            switch6.CheckedChanged += (s, e) => { MinimizeToTray = e.Value; };
            switch7.CheckedChanged += (s, e) => { StartWithWindows = e.Value; };
            switch8.CheckedChanged += (s, e) => { CloseAfterLaunch = e.Value; };

            txtMaaOfficial.TextChanged += (s, e) => { MaaOfficial = txtMaaOfficial.Text; };
            txtMaaBilibili.TextChanged += (s, e) => { MaaBilibili = txtMaaBilibili.Text; };

            //btnMaaOfficialBrowse.Click += (s, e) => BrowseMaaPath(txtMaaOfficial, ref MaaOfficial);
            //btnMaaBilibiliBrowse.Click += (s, e) => BrowseMaaPath(txtMaaBilibili, ref MaaBilibili);
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

        //private void BrowseMaaPath(AntdUI.Input input, ref string field)
        //{
        //    using var dlg = new OpenFileDialog
        //    {
        //        Title = "选择 MAA.exe",
        //        Filter = "MAA 程序 (MAA.exe)|MAA.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
        //        CheckFileExists = true
        //    };
        //    if (!string.IsNullOrEmpty(input.Text))
        //        dlg.InitialDirectory = System.IO.Path.GetDirectoryName(input.Text);
        //    if (dlg.ShowDialog() == DialogResult.OK)
        //    {
        //        input.Text = dlg.FileName;
        //        field = dlg.FileName;
        //    }
        //}

        private void ShowPanel(int tab)
        {
            tableSoftware.Visible = tab == 0;
            panelLog.Visible = tab == 1;
            panelMaa.Visible = tab == 2;
            btnSoftware.Type = tab == 0 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            btnLog.Type = tab == 1 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            btnMaa.Type = tab == 2 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            if (tab == 1) RefreshLog();
        }

        private void RefreshLog()
        {
            txtLog.Text = LogHelper.GetAll();
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }
    }
}
