using System.Drawing;
using System.Windows.Forms;

namespace XelLauncher
{
    partial class About
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            mainPanel = new FlowLayoutPanel();
            shieldPanel = new FlowLayoutPanel();
            avatar = new AntdUI.Avatar();
            labelName = new AntdUI.Label();
            labelAuthor = new AntdUI.Label();
            shieldVersion = new AntdUI.Shield();
            shieldLicense = new AntdUI.Shield();
            shieldStars = new AntdUI.Shield();
            shieldDownloads = new AntdUI.Shield();
            btnClose = new AntdUI.Button();
            mainPanel.SuspendLayout();
            shieldPanel.SuspendLayout();
            SuspendLayout();
            //
            // avatar
            //
            avatar.Image = new System.Drawing.Icon(Properties.Resources.icon, 64, 64).ToBitmap();
            avatar.Name = "avatar";
            avatar.Size = new Size(64, 64);
            avatar.Radius = 8;
            avatar.Margin = new Padding(178, 10, 0, 0);
            avatar.TabIndex = 0;
            //
            // labelName
            //
            labelName.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            labelName.Name = "labelName";
            labelName.Size = new Size(252, 36);
            labelName.Margin = new Padding(84, 6, 0, 0);
            labelName.TabIndex = 1;
            labelName.Text = "Xel Launcher";
            labelName.TextAlign = ContentAlignment.TopCenter;
            //
            // labelAuthor
            //
            labelAuthor.Name = "labelAuthor";
            labelAuthor.Size = new Size(252, 26);
            labelAuthor.Margin = new Padding(84, 2, 0, 0);
            labelAuthor.TabIndex = 3;
            labelAuthor.Text = "By\uff1aTinch";
            labelAuthor.TextAlign = ContentAlignment.TopCenter;
            //
            // shieldVersion
            //
            shieldVersion.AutoSizeMode = AntdUI.TAutoSize.Auto;
            shieldVersion.Color = Color.FromArgb(0, 126, 198);
            shieldVersion.Label = "version";
            shieldVersion.Text = "v" + Application.ProductVersion;
            shieldVersion.Radius = 4;
            shieldVersion.Size = new Size(100, 22);
            shieldVersion.Font = new Font("Microsoft YaHei UI", 8F);
            shieldVersion.Margin = new Padding(0, 0, 6, 0);
            shieldVersion.Name = "shieldVersion";
            shieldVersion.TabIndex = 2;
            //
            // shieldLicense
            //
            shieldLicense.AutoSizeMode = AntdUI.TAutoSize.Auto;
            shieldLicense.Color = Color.FromArgb(50, 160, 80);
            shieldLicense.Label = "license";
            shieldLicense.Text = "Apache 2.0";
            shieldLicense.Radius = 4;
            shieldLicense.Size = new Size(80, 22);
            shieldLicense.Font = new Font("Microsoft YaHei UI", 8F);
            shieldLicense.Margin = new Padding(0);
            shieldLicense.Name = "shieldLicense";
            shieldLicense.TabIndex = 6;
            //
            // shieldStars
            //
            shieldStars.AutoSizeMode = AntdUI.TAutoSize.Auto;
            shieldStars.Color = Color.FromArgb(255, 170, 0);
            shieldStars.Label = "stars";
            shieldStars.LogoSvg = "StarOutlined";
            shieldStars.Text = "...";
            shieldStars.Radius = 4;
            shieldStars.Size = new Size(80, 22);
            shieldStars.Font = new Font("Microsoft YaHei UI", 8F);
            shieldStars.Margin = new Padding(6, 0, 0, 0);
            shieldStars.Name = "shieldStars";
            shieldStars.TabIndex = 7;
            //
            // shieldDownloads
            //
            shieldDownloads.AutoSizeMode = AntdUI.TAutoSize.Auto;
            shieldDownloads.Color = Color.FromArgb(100, 100, 200);
            shieldDownloads.Label = "downloads";
            shieldDownloads.LogoSvg = "DownloadOutlined";
            shieldDownloads.Text = "...";
            shieldDownloads.Radius = 4;
            shieldDownloads.Size = new Size(100, 22);
            shieldDownloads.Font = new Font("Microsoft YaHei UI", 8F);
            shieldDownloads.Margin = new Padding(6, 0, 0, 0);
            shieldDownloads.Name = "shieldDownloads";
            shieldDownloads.TabIndex = 8;
            //
            // shieldPanel
            //
            shieldPanel.Controls.Add(shieldVersion);
            shieldPanel.Controls.Add(shieldLicense);
            shieldPanel.Controls.Add(shieldStars);
            shieldPanel.Controls.Add(shieldDownloads);
            shieldPanel.FlowDirection = FlowDirection.LeftToRight;
            shieldPanel.WrapContents = false;
            shieldPanel.AutoSize = true;
            shieldPanel.Margin = new Padding(46, 6, 0, 0);
            shieldPanel.Name = "shieldPanel";
            shieldPanel.TabIndex = 7;
            //
            // btnClose
            //
            btnClose.Type = AntdUI.TTypeMini.Primary;
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(80, 32);
            btnClose.Margin = new Padding(170, 8, 0, 8);
            btnClose.TabIndex = 4;
            btnClose.Text = "关闭";
            btnClose.LocalizationText = "Cancel";
            btnClose.Click += (s, e) => ParentForm?.Close();
            //
            // mainPanel
            //
            mainPanel.Controls.Add(avatar);
            mainPanel.Controls.Add(labelName);
            mainPanel.Controls.Add(labelAuthor);
            mainPanel.Controls.Add(shieldPanel);
            mainPanel.Controls.Add(btnClose);
            mainPanel.FlowDirection = FlowDirection.TopDown;
            mainPanel.WrapContents = false;
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Name = "mainPanel";
            mainPanel.TabIndex = 8;
            //
            // About
            //
            Controls.Add(mainPanel);
            Name = "About";
            Size = new Size(410, 220);
            BackColor = Color.Transparent;
            shieldPanel.ResumeLayout(false);
            mainPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        private FlowLayoutPanel mainPanel;
        private FlowLayoutPanel shieldPanel;
        private AntdUI.Avatar avatar;
        private AntdUI.Label labelName;
        private AntdUI.Label labelAuthor;
        private AntdUI.Shield shieldVersion;
        private AntdUI.Shield shieldLicense;
        private AntdUI.Shield shieldStars;
        private AntdUI.Shield shieldDownloads;
        private AntdUI.Button btnClose;
    }
}
