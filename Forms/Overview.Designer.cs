using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    partial class Overview
    {
        private System.ComponentModel.IContainer components = null;



        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            btn_bgcolor = new AntdUI.Dropdown();
            btn_mode = new AntdUI.Button();
            btn_more = new AntdUI.Dropdown();
            btn_global = new AntdUI.Dropdown();
            btn_setting = new AntdUI.Button();
            windowBar = new AntdUI.PageHeader();
            colorTheme = new AntdUI.ColorPicker();
            panelSidebar = new System.Windows.Forms.Panel();
            panelSidebarItems = new System.Windows.Forms.FlowLayoutPanel();
            btnSidebarManage = new AntdUI.Button();
            dividerSidebar = new AntdUI.Divider();
            dividerSidebarV = new AntdUI.Divider();
            sidebarBottomPad = new System.Windows.Forms.Panel();
            panelMain = new System.Windows.Forms.Panel();
            windowBar.SuspendLayout();
            SuspendLayout();
            //
            // btn_bgcolor
            //
            btn_bgcolor.Dock = DockStyle.Right;
            btn_bgcolor.Ghost = true;
            btn_bgcolor.IconSvg = "BgColorsOutlined";
            btn_bgcolor.Name = "btn_bgcolor";
            btn_bgcolor.Radius = 0;
            btn_bgcolor.Size = new Size(46, 40);
            btn_bgcolor.TabIndex = 10;
            btn_bgcolor.WaveSize = 0;
            btn_bgcolor.DropDownRadius = 6;
            btn_bgcolor.Placement = AntdUI.TAlignFrom.BR;
            btn_bgcolor.SelectedValueChanged += btn_bgcolor_Changed;
            //
            // btn_mode
            //
            btn_mode.Dock = DockStyle.Right;
            btn_mode.Ghost = true;
            btn_mode.IconSvg = "SunOutlined";
            btn_mode.Location = new Point(972, 0);
            btn_mode.Name = "btn_mode";
            btn_mode.Radius = 0;
            btn_mode.Size = new Size(46, 40);
            btn_mode.TabIndex = 6;
            btn_mode.ToggleIconSvg = "MoonOutlined";
            btn_mode.WaveSize = 0;
            btn_mode.Click += btn_mode_Click;
            //
            // btn_more
            //
            btn_more.Dock = DockStyle.Right;
            btn_more.DropDownRadius = 6;
            btn_more.Ghost = true;
            btn_more.IconSvg = "MoreOutlined";
            btn_more.Location = new Point(1110, 0);
            btn_more.Name = "btn_more";
            btn_more.Placement = AntdUI.TAlignFrom.BR;
            btn_more.Radius = 0;
            btn_more.Size = new Size(46, 40);
            btn_more.TabIndex = 9;
            btn_more.WaveSize = 0;
            btn_more.SelectedValueChanged += btn_more_Changed;
            //
            // btn_global
            //
            btn_global.Dock = DockStyle.Right;
            btn_global.DropDownRadius = 6;
            btn_global.Ghost = true;
            btn_global.IconSvg = "GlobalOutlined";
            btn_global.Location = new Point(1018, 0);
            btn_global.Name = "btn_global";
            btn_global.Placement = AntdUI.TAlignFrom.BR;
            btn_global.Radius = 0;
            btn_global.Size = new Size(46, 40);
            btn_global.TabIndex = 7;
            btn_global.WaveSize = 0;
            btn_global.SelectedValueChanged += btn_global_Changed;
            //
            // btn_setting
            //
            btn_setting.Dock = DockStyle.Right;
            btn_setting.Ghost = true;
            btn_setting.IconSvg = "SettingOutlined";
            btn_setting.Location = new Point(1064, 0);
            btn_setting.Name = "btn_setting";
            btn_setting.Radius = 0;
            btn_setting.Size = new Size(46, 40);
            btn_setting.TabIndex = 8;
            btn_setting.WaveSize = 0;
            btn_setting.Click += btn_setting_Click;
            //
            // colorTheme
            //
            colorTheme.Dock = DockStyle.Right;
            colorTheme.Location = new Point(932, 0);
            colorTheme.Name = "colorTheme";
            colorTheme.Padding = new Padding(5);
            colorTheme.Size = new Size(40, 40);
            colorTheme.TabIndex = 8;
            colorTheme.ValueChanged += colorTheme_ValueChanged;
            //
            // windowBar
            //
            windowBar.BackgroundImageLayout = ImageLayout.Stretch;
            windowBar.Controls.Add(colorTheme);
            windowBar.Controls.Add(btn_bgcolor);
            windowBar.Controls.Add(btn_mode);
            windowBar.Controls.Add(btn_global);
            windowBar.Controls.Add(btn_setting);
            windowBar.Controls.Add(btn_more);
            windowBar.DividerMargin = 3;
            windowBar.DividerShow = true;
            windowBar.Dock = DockStyle.Top;
            windowBar.Icon = Properties.Resources.logo;
            windowBar.Location = new Point(0, 0);
            windowBar.Name = "windowBar";
            windowBar.ShowButton = true;
            windowBar.ShowIcon = true;
            windowBar.Size = new Size(1300, 40);
            windowBar.SubText = "v" + Application.ProductVersion;
            windowBar.SubFont = new Font("Microsoft YaHei UI", 9F);
            windowBar.Text = "Xel Launcher";
            windowBar.TabIndex = 0;
            windowBar.BackClick += btn_back_Click;

            //
            // panelSidebar
            //
            panelSidebar.Dock = DockStyle.Left;
            panelSidebar.Width = 120;
            panelSidebar.Name = "panelSidebar";
            panelSidebar.Controls.Add(panelSidebarItems);
            panelSidebar.Controls.Add(btnSidebarManage);
            panelSidebar.Controls.Add(dividerSidebar);
            panelSidebar.Controls.Add(sidebarBottomPad);
            //
            // panelSidebarItems
            //
            panelSidebarItems.Dock = DockStyle.Fill;
            panelSidebarItems.FlowDirection = FlowDirection.TopDown;
            panelSidebarItems.WrapContents = false;
            panelSidebarItems.AutoScroll = true;
            panelSidebarItems.Name = "panelSidebarItems";
            panelSidebarItems.Padding = new Padding(4, 4, 4, 4);
            //
            // btnSidebarManage
            //
            btnSidebarManage.Dock = DockStyle.Bottom;
            btnSidebarManage.Height = 40;
            btnSidebarManage.IconSvg = "AppstoreOutlined";
            btnSidebarManage.Ghost = true;
            btnSidebarManage.Name = "btnSidebarManage";
            btnSidebarManage.Click += new System.EventHandler(this.btnSidebarManage_Click);
            //
            // dividerSidebar
            //
            dividerSidebar.Dock = DockStyle.Bottom;
            dividerSidebar.Name = "dividerSidebar";
            dividerSidebar.Size = new Size(120, 1);
            dividerSidebar.Thickness = 1F;
            dividerSidebar.Margin = new Padding(6, 0, 6, 0);
            //
            // sidebarBottomPad
            //
            sidebarBottomPad.Dock = DockStyle.Bottom;
            sidebarBottomPad.Height = 24;
            sidebarBottomPad.Name = "sidebarBottomPad";
            //
            // panelMain
            //
            panelMain.Dock = DockStyle.Fill;
            panelMain.Name = "panelMain";
            //
            // dividerSidebarV
            //
            dividerSidebarV.Dock = DockStyle.Left;
            dividerSidebarV.Vertical = true;
            dividerSidebarV.Name = "dividerSidebarV";
            dividerSidebarV.Size = new Size(1, 720);
            dividerSidebarV.Thickness = 1F;
            //

            ClientSize = new Size(1280, 760);
            Controls.Add(panelMain);
            Controls.Add(dividerSidebarV);
            Controls.Add(panelSidebar);
            Controls.Add(windowBar);
            Font = new Font("Microsoft YaHei UI", 12F);
            ForeColor = Color.Black;
            Icon = Properties.Resources.icon;
            MinimumSize = new Size(660, 400);
            Name = "Overview";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Xel Launcher";
            windowBar.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Button btn_mode;
        private AntdUI.Dropdown btn_bgcolor;
        private AntdUI.Dropdown btn_more;
        private AntdUI.Dropdown btn_global;
        private AntdUI.Button btn_setting;
        private AntdUI.PageHeader windowBar;
        private AntdUI.ColorPicker colorTheme;
        private System.Windows.Forms.Panel panelSidebar;
        private System.Windows.Forms.FlowLayoutPanel panelSidebarItems;
        private AntdUI.Button btnSidebarManage;
        private System.Windows.Forms.Panel sidebarBottomPad;
        private AntdUI.Divider dividerSidebar;
        private AntdUI.Divider dividerSidebarV;
        private System.Windows.Forms.Panel panelMain;
    }
}
