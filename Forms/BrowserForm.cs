using AntdUI;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    public class BrowserForm : AntdUI.Window
    {
        private TabHeader tabHeader;
        private System.Windows.Forms.Panel panelContent;
        private readonly Dictionary<string, WebView2> _browsers = new();
        private readonly Dictionary<string, TagTabItem> _tabs = new();
        private int _tabCounter = 0;

        public BrowserForm(string startUrl = "https://www.google.com")
        {
            this.Text = AntdUI.Localization.Get("App.Browser.Title", "浏览器");
            this.Size = new System.Drawing.Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterParent;

            // 内容区域先加（在下层）
            panelContent = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(panelContent);

            // TabHeader 后加（在上层）
            tabHeader = new TabHeader
            {
                Dock = DockStyle.Top,
                ShowAdd = true,
                DragSort = true,
                Height = 44
            };
            this.Controls.Add(tabHeader);

            // 事件绑定——用标准 EventHandler 签名，让 VS 推断
            tabHeader.TabChanged += TabHeader_TabChanged;
            tabHeader.TabClosing += TabHeader_TabClosing;
            tabHeader.AddClick += (s, e) => AddTab("https://www.google.com");

            AddTab(startUrl);
        }

        private void TabHeader_TabChanged(object sender, TabChangedEventArgs e)
        {
            OnTabChanged(e.Value);
        }

        private void TabHeader_TabClosing(object sender, TabCloseEventArgs e)
        {
            OnTabClosing(e.Value);
        }

        private async void AddTab(string url)
        {
            _tabCounter++;
            string tabId = "tab_" + _tabCounter;

            var tab = new TagTabItem(url) { ShowClose = true, Tag = tabId };
            _tabs[tabId] = tab;
            tabHeader.AddTab(tab, true);

            foreach (var kv in _browsers)
                kv.Value.Visible = false;

            var browser = new WebView2 { Dock = DockStyle.Fill, Visible = true };
            _browsers[tabId] = browser;
            panelContent.Controls.Add(browser);

            try
            {
                await browser.EnsureCoreWebView2Async();
                browser.CoreWebView2.Navigate(url);

                browser.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    if (this.IsHandleCreated)
                        this.Invoke(() => tab.Text = browser.CoreWebView2.DocumentTitle);
                };
            }
            catch
            {
                AntdUI.Message.error(this, AntdUI.Localization.Get("App.Browser.NoRuntime", "未找到 WebView2 Runtime，请先安装！"));
            }
        }

        private void OnTabChanged(TagTabItem tab)
        {
            if (tab?.Tag is not string tabId) return;
            foreach (var kv in _browsers)
                kv.Value.Visible = kv.Key == tabId;
        }

        private void OnTabClosing(TagTabItem tab)
        {
            if (tab?.Tag is not string tabId) return;

            if (_browsers.TryGetValue(tabId, out var browser))
            {
                browser.Dispose();
                _browsers.Remove(tabId);
            }
            _tabs.Remove(tabId);

            if (tabHeader.Items.Count == 0)
                this.Close();
        }
    }
}
