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
            mainPanel = new TableLayoutPanel();
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
            avatar.Anchor = AnchorStyles.None;
            avatar.Name = "avatar";
            avatar.Size = new Size(64, 64);
            avatar.Radius = 8;
            avatar.Margin = new Padding(0, 10, 0, 0);
            avatar.TabIndex = 0;
            //
            // labelName
            //
            labelName.Anchor = AnchorStyles.None;
            labelName.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            labelName.Name = "labelName";
            labelName.Size = new Size(252, 36);
            labelName.Margin = new Padding(0);
            labelName.TabIndex = 1;
            labelName.Text = "Xel Launcher";
            labelName.TextAlign = ContentAlignment.TopCenter;
            //
            // labelAuthor
            //
            labelAuthor.Anchor = AnchorStyles.None;
            labelAuthor.Name = "labelAuthor";
            labelAuthor.Size = new Size(252, 26);
            labelAuthor.Margin = new Padding(0);
            labelAuthor.TabIndex = 3;
            labelAuthor.Text = "By：Tinch";
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
            shieldDownloads.Size = new Size(130, 22);
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
            shieldPanel.Anchor = AnchorStyles.None;
            shieldPanel.FlowDirection = FlowDirection.LeftToRight;
            shieldPanel.WrapContents = false;
            shieldPanel.AutoSize = true;
            shieldPanel.Margin = new Padding(0, 6, 0, 0);
            shieldPanel.Name = "shieldPanel";
            shieldPanel.TabIndex = 7;
            //
            // btnClose
            //
            btnClose.Anchor = AnchorStyles.None;
            btnClose.Type = AntdUI.TTypeMini.Primary;
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(96, 34);
            btnClose.Margin = new Padding(0, 12, 0, 8);
            btnClose.TabIndex = 4;
            btnClose.Text = "关闭";
            btnClose.LocalizationText = "Close";
            btnClose.Click += (s, e) => ParentForm?.Close();
            //
            // mainPanel
            //
            mainPanel.ColumnCount = 1;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainPanel.RowCount = 5;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            mainPanel.Controls.Add(avatar, 0, 0);
            mainPanel.Controls.Add(labelName, 0, 1);
            mainPanel.Controls.Add(labelAuthor, 0, 2);
            mainPanel.Controls.Add(shieldPanel, 0, 3);
            mainPanel.Controls.Add(btnClose, 0, 4);
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.Name = "mainPanel";
            mainPanel.TabIndex = 8;
            //
            // About
            //
            Controls.Add(mainPanel);
            Name = "About";
            Size = new Size(470, 236);
            BackColor = Color.Transparent;
            shieldPanel.ResumeLayout(false);
            mainPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        private TableLayoutPanel mainPanel;
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
