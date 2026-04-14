using AntdUI;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace XelLauncher.Forms
{
    public partial class TabHeaderForm : AntdUI.Window
    {
        private readonly Dictionary<string, WebView2> _browsers = new();
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.Panel panelToolbar;
        private AntdUI.Button btnBack, btnForward, btnRefresh;
        private AntdUI.Input txtUrl;
        private int _tabCounter = 0;
        private string _currentTabId = null;


        public TabHeaderForm(string startUrl = "https://www.google.com/")
        {
            InitializeComponent();
            this.Icon = Properties.Resources.icon;        // 任务栏图标
            tabHeader1.IconSvg = "ChromeFilled";

            // 工具栏
            panelToolbar = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new System.Drawing.Rectangle(6, 6, 6, 6).IsEmpty ? new Padding(6) : new Padding(6)
            };

            btnBack = new AntdUI.Button
            {
                Width = 34,
                Height = 34,
                IconSvg = "LeftOutlined",
                BorderWidth = 0,
                Dock = DockStyle.Left
            };
            btnBack.Click += (s, e) => CurrentBrowser()?.GoBack();

            btnForward = new AntdUI.Button
            {
                Width = 34,
                Height = 34,
                IconSvg = "RightOutlined",
                BorderWidth = 0,
                Dock = DockStyle.Left
            };
            btnForward.Click += (s, e) => CurrentBrowser()?.GoForward();

            btnRefresh = new AntdUI.Button
            {
                Width = 34,
                Height = 34,
                IconSvg = "ReloadOutlined",
                BorderWidth = 0,
                Dock = DockStyle.Left
            };
            btnRefresh.Click += (s, e) => CurrentBrowser()?.CoreWebView2?.Reload();

            txtUrl = new AntdUI.Input
            {
                Dock = DockStyle.Fill,
                Radius = 20,
                PlaceholderText = "输入网址后按回车访问，或直接搜索..."
            };
            txtUrl.KeyDown += TxtUrl_KeyDown;

            panelToolbar.Controls.Add(txtUrl);
            panelToolbar.Controls.Add(btnRefresh);
            panelToolbar.Controls.Add(btnForward);
            panelToolbar.Controls.Add(btnBack);

            // 内容区
            panelContent = new System.Windows.Forms.Panel { Dock = DockStyle.Fill };

            // 按顺序添加：TabHeader → 工具栏 → 内容区
            this.Controls.Add(panelContent);      // 先加内容区（Fill，在最底层）
            this.Controls.Add(panelToolbar);      // 再加工具栏（Top，在内容区上方）

            this.Controls.SetChildIndex(panelContent, 0);
            this.Controls.SetChildIndex(panelToolbar, 1);
            ApplyTheme(AntdUI.Config.IsDark);
            this.StartPosition = FormStartPosition.CenterScreen;
            AddTab(startUrl);
        }

        private void ApplyTheme(bool dark)
        {
            var bg     = dark ? System.Drawing.Color.FromArgb(30, 30, 30)   : System.Drawing.Color.FromArgb(242, 242, 242);
            var tabBg  = dark ? System.Drawing.Color.FromArgb(25, 25, 25)   : System.Drawing.Color.FromArgb(232, 232, 232);
            var active = dark ? System.Drawing.Color.FromArgb(50, 50, 50)   : System.Drawing.Color.White;

            this.BackColor          = bg;
            panelToolbar.BackColor  = bg;
            tabHeader1.BackColor    = tabBg;
            tabHeader1.BackActive   = active;
        }

        private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            var input = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            // 判断是网址还是搜索词
            string url;
            if (input.StartsWith("http://") || input.StartsWith("https://"))
                url = input;
            else if (input.Contains(".") && !input.Contains(" "))
                url = "https://" + input;
            else
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);

            // 当前标签导航，不新建
            var browser = CurrentBrowser();
            if (browser?.CoreWebView2 != null)
                browser.CoreWebView2.Navigate(url);
        }

        private WebView2 CurrentBrowser()
        {
            if (_currentTabId == null) return null;
            _browsers.TryGetValue(_currentTabId, out var b);
            return b;
        }

        private async void AddTab(string url)
        {
            _tabCounter++;
            string tabId = "tab_" + _tabCounter;

            var tab = new TagTabItem(url) { ShowClose = true };
            tab.Tag = tabId;
            tabHeader1.AddTab(tab, true);
            _currentTabId = tabId;

            foreach (var kv in _browsers)
                kv.Value.Visible = false;

            var browser = new WebView2 { Dock = DockStyle.Fill, Visible = true };
            _browsers[tabId] = browser;
            panelContent.Controls.Add(browser);

            try
            {
                string cacheDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XelLauncher", "WebView2Cache");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                    .CreateAsync(null, cacheDir);

                await browser.EnsureCoreWebView2Async(env);

                // 让网页 prefers-color-scheme 跟随系统深色设置
                browser.CoreWebView2.Profile.PreferredColorScheme =
                    AntdUI.Config.IsDark
                        ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark
                        : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;

                if (browser.CoreWebView2 != null)
                {
                    browser.CoreWebView2.Navigate(url);
                }

                // 拦截新窗口请求（target="_blank" / window.open），在内部新标签页打开
                browser.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;   // 阻止唤起外部浏览器
                    var newUrl = e.Uri;
                    if (this.IsHandleCreated)
                        this.Invoke(() => AddTab(newUrl));
                };

                // 标题同步到标签，若是当前激活标签则同步到窗口标题栏
                browser.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    if (this.IsHandleCreated)
                        this.Invoke(() =>
                        {
                            var title = browser.CoreWebView2.DocumentTitle;
                            tab.Text = title;
                            if (_currentTabId == tabId)
                                this.Text = title;
                        });
                };

                // 地址栏同步当前网址
                browser.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (this.IsHandleCreated)
                        this.Invoke(() => txtUrl.Text = browser.CoreWebView2.Source);
                };
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this, ex.InnerException?.Message ?? ex.Message);
            }
        }

        private void tabHeader1_TabChanged(object sender, TabChangedEventArgs e)
        {
            if (e.Value?.Tag is not string tabId) return;
            _currentTabId = tabId;
            foreach (var kv in _browsers)
                kv.Value.Visible = kv.Key == tabId;

            // 同步地址栏和窗口标题
            if (_browsers.TryGetValue(tabId, out var browser) && browser.CoreWebView2 != null)
            {
                txtUrl.Text = browser.CoreWebView2.Source;
                this.Text = browser.CoreWebView2.DocumentTitle;
            }
        }

        private void tabHeader1_TabClosing(object sender, TabCloseEventArgs e)
        {
            if (e.Value?.Tag is not string tabId) return;
            if (_browsers.TryGetValue(tabId, out var browser))
            {
                browser.Dispose();
                _browsers.Remove(tabId);
            }
            if (tabHeader1.Items.Count == 0)
                this.Close();
        }

        private void tabHeader1_AddClick(object sender, EventArgs e)
        {
            AddTab("https://www.google.com/");
        }
    }
}