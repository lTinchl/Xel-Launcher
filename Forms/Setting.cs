using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher
{
    public partial class Setting : UserControl
    {
        AntdUI.BaseForm form;
        private UpdateInfo _updateInfo;
        private CancellationTokenSource _downloadCts;
        const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "Xel Launcher";

        public bool Animation, ShadowEnabled, ShowInWindow, ScrollBarHide, TextRenderingHighQuality, MinimizeToTray, StartWithWindows, CloseAfterLaunch, HideToTrayOnLaunch, UseExternalBrowser;

        public Setting(AntdUI.BaseForm _form)
        {
            form = _form;
            InitializeComponent();
            btnSoftware.Click += (s, e) => ShowPanel(0);
            btnLog.Click += (s, e) => ShowPanel(1);
            btnUpdate.Click += (s, e) => ShowPanel(2);
            LogHelper.OnLog += () => Invoke(new Action(RefreshLog));

            switch1.Checked = Animation = AntdUI.Config.Animation;
            switch2.Checked = ShadowEnabled = AntdUI.Config.ShadowEnabled;
            switch3.Checked = ShowInWindow = AntdUI.Config.ShowInWindow;
            switch4.Checked = ScrollBarHide = AntdUI.Config.ScrollBarHide;
            switch5.Checked = TextRenderingHighQuality = AntdUI.Config.TextRenderingHighQuality;
            switch6.Checked = MinimizeToTray = ConfigHelper.Load().MinimizeToTray;
            switch7.Checked = StartWithWindows = GetStartWithWindows();
            switch8.Checked = CloseAfterLaunch = ConfigHelper.Load().CloseAfterLaunch;
            switch9.Checked = HideToTrayOnLaunch = ConfigHelper.Load().HideToTrayOnLaunch;
            switch10.Checked = UseExternalBrowser = ConfigHelper.Load().UseExternalBrowser;

            switch1.CheckedChanged += (s, e) => { Animation = e.Value; };
            switch2.CheckedChanged += (s, e) => { ShadowEnabled = e.Value; };
            switch3.CheckedChanged += (s, e) => { ShowInWindow = e.Value; };
            switch4.CheckedChanged += (s, e) => { ScrollBarHide = e.Value; };
            switch5.CheckedChanged += (s, e) => { TextRenderingHighQuality = e.Value; };
            switch6.CheckedChanged += (s, e) => { MinimizeToTray = e.Value; };
            switch7.CheckedChanged += (s, e) => { StartWithWindows = e.Value; };
            switch8.CheckedChanged += (s, e) => { CloseAfterLaunch = e.Value; };
            switch9.CheckedChanged += (s, e) => { HideToTrayOnLaunch = e.Value; };
            switch10.CheckedChanged += (s, e) => { UseExternalBrowser = e.Value; };

            BindUpdatePanel();
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
        private void ShowPanel(int tab)
        {
            scrollSoftware.Visible = tab == 0;
            panelLog.Visible = tab == 1;
            panelUpdate.Visible = tab == 2;
            btnSoftware.Type = tab == 0 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            btnLog.Type = tab == 1 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            btnUpdate.Type = tab == 2 ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            if (tab == 1) RefreshLog();
        }

        private void RefreshLog()
        {
            txtLog.Text = LogHelper.GetAll();
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void BindUpdatePanel()
        {
            btnCheckUpdate.Click += async (s, e) =>
            {
                try { await CheckUpdateAsync(); }
                catch (Exception ex) { txtChangelog.Text = $"发生意外错误：{ex.Message}"; }
            };
            btnDownloadSetup.Click += async (s, e) =>
            {
                try { await DownloadAsync(isSetup: true); }
                catch (Exception ex) { lblDownloadStatus.Text = $"错误：{ex.Message}"; }
            };
            btnDownloadPortable.Click += async (s, e) =>
            {
                try { await DownloadAsync(isSetup: false); }
                catch (Exception ex) { lblDownloadStatus.Text = $"错误：{ex.Message}"; }
            };
            btnFallback.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(UpdateHelper.FallbackUrl) &&
                    UpdateHelper.FallbackUrl != "https://pan.quark.cn/s/54cd514d4236")
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = UpdateHelper.FallbackUrl,
                        UseShellExecute = true
                    });
                }
            };
        }

        private async Task CheckUpdateAsync()
        {
            btnCheckUpdate.Text = "检查中...";

            btnCheckUpdate.Enabled = false;
            try
            {
                var info = await UpdateHelper.CheckAsync();
                if (info == null)
                {
                    txtChangelog.Text = "检查失败，请检查网络连接。";
                    lblLatestVersion.Text = "—";
                    panelUpdateButtons.Visible = false;
                    return;
                }

                _updateInfo = info;
                lblLatestVersion.Text = "v" + info.LatestVersion;

                var currentVer = System.Windows.Forms.Application.ProductVersion;
                if (UpdateHelper.IsNewer(currentVer, info.LatestVersion))
                {
                    txtChangelog.Text = info.Changelog;
                    panelUpdateButtons.Visible = true;
                    btnDownloadSetup.Visible    = true;
                    btnDownloadPortable.Visible = true;
                    btnFallback.Visible         = false;
                    progressDownload.Visible    = false;
                    progressDownload.Value      = 0F;
                    lblDownloadStatus.Text      = "";
                }
                else
                {
                    txtChangelog.Text = "已是最新版本";
                    panelUpdateButtons.Visible = false;
                }
            }
            finally
            {
                btnCheckUpdate.Text    = "检查更新";
                btnCheckUpdate.Enabled = true;
            }
        }

        private async Task DownloadAsync(bool isSetup)
        {
            if (_updateInfo == null) return;

            string url = isSetup ? _updateInfo.SetupDownloadUrl : _updateInfo.PortableDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                ShowFallback();
                return;
            }

            string destPath;
            if (!isSetup)
            {
                var sfd = new System.Windows.Forms.SaveFileDialog
                {
                    Title            = "保存便携版",
                    FileName         = $"XelLauncher.v{_updateInfo.LatestVersion}-Portable.zip",
                    Filter           = "ZIP 压缩包|*.zip",
                    DefaultExt       = "zip",
                    RestoreDirectory = true
                };
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                destPath = sfd.FileName;
            }
            else
            {
                var tmpDir = Path.Combine(Path.GetTempPath(), "XelLauncher_Update");
                destPath = Path.Combine(tmpDir,
                    $"XelLauncher-{_updateInfo.LatestVersion}-Setup.exe");
                Directory.CreateDirectory(tmpDir);
            }

            btnDownloadSetup.Enabled    = false;
            btnDownloadPortable.Enabled = false;
            progressDownload.Visible    = true;
            progressDownload.Value      = 0F;
            lblDownloadStatus.Text      = "准备下载...";

            _downloadCts = new CancellationTokenSource();

            try
            {
                await UpdateHelper.DownloadAsync(url, destPath,
                    (pct, downloaded, total) =>
                    {
                        if (!IsHandleCreated) return;
                        Invoke(() =>
                        {
                            if (pct >= 0)
                            {
                                progressDownload.Value = pct / 100F;
                                var dlMB    = downloaded / 1048576.0;
                                var totalMB = total / 1048576.0;
                                lblDownloadStatus.Text = $"{dlMB:F1} MB / {totalMB:F1} MB  {pct}%";
                            }
                            else
                            {
                                var dlMB = downloaded / 1048576.0;
                                lblDownloadStatus.Text = $"{dlMB:F1} MB 已下载";
                            }
                        });
                    },
                    _downloadCts.Token);

                if (isSetup)
                {
                    var batDir  = Path.Combine(Path.GetTempPath(), "XelLauncher_Update");
                    var batPath = Path.Combine(batDir, "update.bat");
                    Directory.CreateDirectory(batDir);
                    File.WriteAllText(batPath,
                        "@echo off\r\n" +
                        "TIMEOUT /T 2 /NOBREAK >nul\r\n" +
                        $"start \"\" \"{destPath}\"\r\n" +
                        "exit\r\n");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        Arguments       = $"/c \"{batPath}\"",
                        WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                        UseShellExecute = true
                    });
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    lblDownloadStatus.Text = "下载完成！";
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"/select,\"{destPath}\"");
                }
            }
            catch (OperationCanceledException)
            {
                lblDownloadStatus.Text = "已取消";
            }
            catch (Exception)
            {
                lblDownloadStatus.Text = "下载失败";
                ShowFallback();
            }
            finally
            {
                btnDownloadSetup.Enabled    = true;
                btnDownloadPortable.Enabled = true;
            }
        }

        private void ShowFallback()
        {
            btnDownloadSetup.Visible    = false;
            btnDownloadPortable.Visible = false;
            btnFallback.Visible         = true;
        }
    }
}
