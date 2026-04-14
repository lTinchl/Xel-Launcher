namespace XelLauncher
{
    partial class Setting
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            panelLeft = new System.Windows.Forms.Panel();
            btnSoftware = new AntdUI.Button();
            btnLog = new AntdUI.Button();
            btnUpdate = new AntdUI.Button();
            dividerV = new AntdUI.Divider();
            panelRight = new System.Windows.Forms.Panel();
            panelLog = new System.Windows.Forms.Panel();
            panelUpdate = new System.Windows.Forms.Panel();
            txtLog = new System.Windows.Forms.RichTextBox();
            tableSoftware = new System.Windows.Forms.TableLayoutPanel();
            scrollSoftware = new System.Windows.Forms.Panel();
            label1 = new AntdUI.Label();
            label2 = new AntdUI.Label();
            label3 = new AntdUI.Label();
            label4 = new AntdUI.Label();
            label5 = new AntdUI.Label();
            label6 = new AntdUI.Label();
            switch1 = new AntdUI.Switch();
            switch2 = new AntdUI.Switch();
            switch3 = new AntdUI.Switch();
            switch4 = new AntdUI.Switch();
            switch5 = new AntdUI.Switch();
            switch6 = new AntdUI.Switch();
            label7 = new AntdUI.Label();
            switch7 = new AntdUI.Switch();
            label8 = new AntdUI.Label();
            switch8 = new AntdUI.Switch();
            label9 = new AntdUI.Label();
            switch9 = new AntdUI.Switch();
            label10 = new AntdUI.Label();
            switch10 = new AntdUI.Switch();
            panelLeft.SuspendLayout();
            panelRight.SuspendLayout();
            scrollSoftware.SuspendLayout();
            panelLog.SuspendLayout();
            panelUpdate.SuspendLayout();
            tableSoftware.SuspendLayout();
            SuspendLayout();
            //
            // panelLeft
            //
            panelLeft.Controls.Add(btnLog);
            panelLeft.Controls.Add(btnUpdate);
            panelLeft.Controls.Add(btnSoftware);
            panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            panelLeft.Width = 120;
            panelLeft.Name = "panelLeft";
            //
            // btnSoftware
            //
            btnSoftware.Text = "软件设置";
            btnSoftware.LocalizationText = "App.Setting.Software";
            btnSoftware.Dock = System.Windows.Forms.DockStyle.Top;
            btnSoftware.Height = 46;
            btnSoftware.Name = "btnSoftware";
            btnSoftware.Type = AntdUI.TTypeMini.Primary;
            //
            // btnLog
            //
            btnLog.Text = "软件日志";
            btnLog.LocalizationText = "App.Setting.Log";
            btnLog.Dock = System.Windows.Forms.DockStyle.Top;
            btnLog.Height = 46;
            btnLog.Name = "btnLog";
            btnLog.Type = AntdUI.TTypeMini.Default;
            //
            // btnUpdate
            //
            btnUpdate.Text = "软件更新";
            btnUpdate.LocalizationText = "App.Setting.Update";
            btnUpdate.Dock = System.Windows.Forms.DockStyle.Top;
            btnUpdate.Height = 46;
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Type = AntdUI.TTypeMini.Default;
            //
            // dividerV
            //
            dividerV.Dock = System.Windows.Forms.DockStyle.Left;
            dividerV.Name = "dividerV";
            dividerV.Vertical = true;
            dividerV.Size = new System.Drawing.Size(20, 260);
            //
            // panelRight
            //
            panelRight.Controls.Add(panelLog);
            panelRight.Controls.Add(panelUpdate);
            panelRight.Controls.Add(scrollSoftware);
            panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            panelRight.Name = "panelRight";
            //
            // scrollSoftware
            //
            scrollSoftware.Controls.Add(tableSoftware);
            scrollSoftware.Dock = System.Windows.Forms.DockStyle.Fill;
            scrollSoftware.AutoScroll = true;
            scrollSoftware.Name = "scrollSoftware";
            //
            // panelLog
            //
            panelLog.Controls.Add(txtLog);
            panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            panelLog.Name = "panelLog";
            panelLog.Visible = false;
            //
            // panelUpdate
            //
            panelUpdate.Dock = System.Windows.Forms.DockStyle.Fill;
            panelUpdate.Name = "panelUpdate";
            panelUpdate.Visible = false;
            panelUpdate.Padding = new System.Windows.Forms.Padding(8);

            // tableUpdate：版本信息 + 检查按钮
            tableUpdate = new System.Windows.Forms.TableLayoutPanel();
            tableUpdate.SuspendLayout();
            tableUpdate.ColumnCount = 3;
            tableUpdate.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableUpdate.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableUpdate.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            tableUpdate.RowCount = 2;
            tableUpdate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            tableUpdate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            tableUpdate.Dock = System.Windows.Forms.DockStyle.Top;
            tableUpdate.Height = 60;
            tableUpdate.Name = "tableUpdate";

            lblCurrentVersionTitle = new AntdUI.Label();
            lblCurrentVersionTitle.Text = "当前版本";
            lblCurrentVersionTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            lblCurrentVersionTitle.Name = "lblCurrentVersionTitle";

            lblLatestVersionTitle = new AntdUI.Label();
            lblLatestVersionTitle.Text = "最新版本";
            lblLatestVersionTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            lblLatestVersionTitle.Name = "lblLatestVersionTitle";

            lblCurrentVersion = new AntdUI.Label();
            lblCurrentVersion.Text = "v" + System.Windows.Forms.Application.ProductVersion;
            lblCurrentVersion.Dock = System.Windows.Forms.DockStyle.Fill;
            lblCurrentVersion.Name = "lblCurrentVersion";

            lblLatestVersion = new AntdUI.Label();
            lblLatestVersion.Text = "—";
            lblLatestVersion.Dock = System.Windows.Forms.DockStyle.Fill;
            lblLatestVersion.Name = "lblLatestVersion";

            btnCheckUpdate = new AntdUI.Button();
            btnCheckUpdate.Text = "检查更新";
            btnCheckUpdate.Name = "btnCheckUpdate";
            btnCheckUpdate.Dock = System.Windows.Forms.DockStyle.Fill;
            btnCheckUpdate.Margin = new System.Windows.Forms.Padding(4, 2, 0, 2);

            tableUpdate.Controls.Add(lblCurrentVersionTitle, 0, 0);
            tableUpdate.Controls.Add(lblLatestVersionTitle, 1, 0);
            tableUpdate.Controls.Add(lblCurrentVersion, 0, 1);
            tableUpdate.Controls.Add(lblLatestVersion, 1, 1);
            tableUpdate.SetRowSpan(btnCheckUpdate, 2);
            tableUpdate.Controls.Add(btnCheckUpdate, 2, 0);

            // txtChangelog：更新日志
            txtChangelog = new System.Windows.Forms.RichTextBox();
            txtChangelog.Dock = System.Windows.Forms.DockStyle.Fill;
            txtChangelog.Name = "txtChangelog";
            txtChangelog.ReadOnly = true;
            txtChangelog.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtChangelog.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
            txtChangelog.Font = new System.Drawing.Font("Consolas", 9F);
            txtChangelog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            txtChangelog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtChangelog.Text = "点击「检查更新」查看最新版本信息";

            // panelUpdateButtons：下载按钮 + 进度
            panelUpdateButtons = new System.Windows.Forms.Panel();
            panelUpdateButtons.SuspendLayout();
            panelUpdateButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            panelUpdateButtons.Height = 80;
            panelUpdateButtons.Name = "panelUpdateButtons";
            panelUpdateButtons.Visible = false;

            // panelButtons：按钮行，停靠顶部
            var panelButtons = new System.Windows.Forms.Panel();
            panelButtons.Dock = System.Windows.Forms.DockStyle.Top;
            panelButtons.Height = 42;
            panelButtons.Name = "panelButtons";

            btnDownloadSetup = new AntdUI.Button();
            btnDownloadSetup.Text = "⬇ 下载安装版";
            btnDownloadSetup.Name = "btnDownloadSetup";
            btnDownloadSetup.Width = 130;
            btnDownloadSetup.Height = 34;
            btnDownloadSetup.Location = new System.Drawing.Point(0, 4);

            btnDownloadPortable = new AntdUI.Button();
            btnDownloadPortable.Text = "⬇ 下载便携版";
            btnDownloadPortable.Name = "btnDownloadPortable";
            btnDownloadPortable.Width = 130;
            btnDownloadPortable.Height = 34;
            btnDownloadPortable.Location = new System.Drawing.Point(138, 4);

            btnFallback = new AntdUI.Button();
            btnFallback.Text = "打开网盘下载页";
            btnFallback.Name = "btnFallback";
            btnFallback.Width = 130;
            btnFallback.Height = 34;
            btnFallback.Location = new System.Drawing.Point(0, 4);
            btnFallback.Visible = false;
            btnFallback.Type = AntdUI.TTypeMini.Warn;

            panelButtons.Controls.Add(btnDownloadSetup);
            panelButtons.Controls.Add(btnDownloadPortable);
            panelButtons.Controls.Add(btnFallback);

            // lblDownloadStatus：状态文字，停靠底部
            lblDownloadStatus = new AntdUI.Label();
            lblDownloadStatus.Name = "lblDownloadStatus";
            lblDownloadStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            lblDownloadStatus.Height = 20;
            lblDownloadStatus.Text = "";

            // progressDownload：进度条，停靠底部（在 lblDownloadStatus 上方）
            progressDownload = new AntdUI.Progress();
            progressDownload.Name = "progressDownload";
            progressDownload.Dock = System.Windows.Forms.DockStyle.Bottom;
            progressDownload.Height = 10;
            progressDownload.Value = 0F;
            progressDownload.Visible = false;

            panelUpdateButtons.Controls.Add(panelButtons);
            panelUpdateButtons.Controls.Add(progressDownload);
            panelUpdateButtons.Controls.Add(lblDownloadStatus);

            panelUpdate.Controls.Add(txtChangelog);
            panelUpdate.Controls.Add(tableUpdate);
            panelUpdate.Controls.Add(panelUpdateButtons);
            //
            // txtLog
            //
            txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtLog.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
            txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            txtLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            txtLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            //
            // tableSoftware
            //
            tableSoftware.ColumnCount = 2;
            tableSoftware.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableSoftware.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            tableSoftware.Controls.Add(label1, 0, 0);
            tableSoftware.Controls.Add(label2, 0, 1);
            tableSoftware.Controls.Add(label3, 0, 2);
            tableSoftware.Controls.Add(label4, 0, 3);
            tableSoftware.Controls.Add(label5, 0, 4);
            tableSoftware.Controls.Add(label6, 0, 5);
            tableSoftware.Controls.Add(switch1, 1, 0);
            tableSoftware.Controls.Add(switch2, 1, 1);
            tableSoftware.Controls.Add(switch3, 1, 2);
            tableSoftware.Controls.Add(switch4, 1, 3);
            tableSoftware.Controls.Add(switch5, 1, 4);
            tableSoftware.Controls.Add(switch6, 1, 5);
            tableSoftware.Controls.Add(label7, 0, 6);
            tableSoftware.Controls.Add(switch7, 1, 6);
            tableSoftware.Controls.Add(label8, 0, 7);
            tableSoftware.Controls.Add(switch8, 1, 7);
            tableSoftware.Controls.Add(label9, 0, 8);
            tableSoftware.Controls.Add(switch9, 1, 8);
            tableSoftware.Controls.Add(label10, 0, 9);
            tableSoftware.Controls.Add(switch10, 1, 9);
            tableSoftware.Dock = System.Windows.Forms.DockStyle.Top;
            tableSoftware.AutoSize = true;
            tableSoftware.RowCount = 11;
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            tableSoftware.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableSoftware.Name = "tableSoftware";
            //
            // label1
            //
            label1.Dock = System.Windows.Forms.DockStyle.Fill;
            label1.LocalizationText = "AnimationEnabled";
            label1.Name = "label1";
            label1.Text = "动画使能";
            label1.TabIndex = 0;
            //
            // label2
            //
            label2.Dock = System.Windows.Forms.DockStyle.Fill;
            label2.LocalizationText = "ShadowEnabled";
            label2.Name = "label2";
            label2.Text = "阴影使能";
            label2.TabIndex = 0;
            //
            // label3
            //
            label3.Dock = System.Windows.Forms.DockStyle.Fill;
            label3.LocalizationText = "PopupWindow";
            label3.Name = "label3";
            label3.Text = "弹出在窗口";
            label3.TabIndex = 0;
            //
            // label4
            //
            label4.Dock = System.Windows.Forms.DockStyle.Fill;
            label4.LocalizationText = "ScrollBarHidden";
            label4.Name = "label4";
            label4.Text = "滚动条隐藏样式";
            label4.TabIndex = 0;
            //
            // label5
            //
            label5.Dock = System.Windows.Forms.DockStyle.Fill;
            label5.LocalizationText = "TextRenderingHighQuality";
            label5.Name = "label5";
            label5.Text = "文本高质量呈现";
            label5.TabIndex = 0;
            //
            // label6
            //
            label6.Dock = System.Windows.Forms.DockStyle.Fill;
            label6.Name = "label6";
            label6.Text = "最小化到托盘";
            label6.LocalizationText = "App.Setting.MinimizeToTray";
            label6.TabIndex = 0;
            //
            // switch1
            //
            switch1.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch1.Name = "switch1";
            switch1.Size = new System.Drawing.Size(50, 30);
            switch1.TabIndex = 0;
            //
            // switch2
            //
            switch2.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch2.Name = "switch2";
            switch2.Size = new System.Drawing.Size(50, 30);
            switch2.TabIndex = 0;
            //
            // switch3
            //
            switch3.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch3.Name = "switch3";
            switch3.Size = new System.Drawing.Size(50, 30);
            switch3.TabIndex = 0;
            //
            // switch4
            //
            switch4.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch4.Name = "switch4";
            switch4.Size = new System.Drawing.Size(50, 30);
            switch4.TabIndex = 0;
            //
            // switch5
            //
            switch5.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch5.Name = "switch5";
            switch5.Size = new System.Drawing.Size(50, 30);
            switch5.TabIndex = 0;
            //
            // switch6
            //
            switch6.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch6.Name = "switch6";
            switch6.Size = new System.Drawing.Size(50, 30);
            switch6.TabIndex = 0;
            //
            // label7
            //
            label7.Dock = System.Windows.Forms.DockStyle.Fill;
            label7.Name = "label7";
            label7.Text = "开机自启动";
            label7.LocalizationText = "App.Setting.StartWithWindows";
            label7.TabIndex = 0;
            //
            // switch7
            //
            switch7.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch7.Name = "switch7";
            switch7.Size = new System.Drawing.Size(50, 30);
            switch7.TabIndex = 0;
            //
            // label8
            //
            label8.Dock = System.Windows.Forms.DockStyle.Fill;
            label8.Name = "label8";
            label8.Text = "启动游戏后关闭软件";
            label8.LocalizationText = "App.Setting.CloseAfterLaunch";
            label8.TabIndex = 0;
            //
            // switch8
            //
            switch8.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch8.Name = "switch8";
            switch8.Size = new System.Drawing.Size(50, 30);
            switch8.TabIndex = 0;
            //
            // label9
            //
            label9.Dock = System.Windows.Forms.DockStyle.Fill;
            label9.Name = "label9";
            label9.Text = "启动游戏后隐藏至托盘";
            label9.LocalizationText = "App.Setting.HideToTrayOnLaunch";
            label9.TabIndex = 0;
            //
            // switch9
            //
            switch9.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch9.Name = "switch9";
            switch9.Size = new System.Drawing.Size(50, 30);
            switch9.TabIndex = 0;
            //
            // label10
            //
            label10.Dock = System.Windows.Forms.DockStyle.Fill;
            label10.Name = "label10";
            label10.Text = "使用外部浏览器";
            label10.LocalizationText = "App.Setting.UseExternalBrowser";
            label10.TabIndex = 0;
            //
            // switch10
            //
            switch10.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch10.Name = "switch10";
            switch10.Size = new System.Drawing.Size(50, 30);
            switch10.TabIndex = 0;
            //
            // Setting
            //
            Controls.Add(panelRight);
            Controls.Add(dividerV);
            Controls.Add(panelLeft);
            Name = "Setting";
            Size = new System.Drawing.Size(600, 400);
            tableSoftware.ResumeLayout(false);
            panelUpdateButtons.ResumeLayout(false);
            tableUpdate.ResumeLayout(false);
            panelUpdate.ResumeLayout(false);
            panelLog.ResumeLayout(false);
            scrollSoftware.ResumeLayout(false);
            panelRight.ResumeLayout(false);
            panelLeft.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelLeft;
        private AntdUI.Button btnSoftware;
        private AntdUI.Divider dividerV;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel scrollSoftware;
        private System.Windows.Forms.TableLayoutPanel tableSoftware;
        private AntdUI.Label label1;
        private AntdUI.Label label2;
        private AntdUI.Label label3;
        private AntdUI.Label label4;
        private AntdUI.Label label5;
        private AntdUI.Label label6;
        private AntdUI.Switch switch1;
        private AntdUI.Switch switch2;
        private AntdUI.Switch switch3;
        private AntdUI.Switch switch4;
        private AntdUI.Switch switch5;
        private AntdUI.Switch switch6;
        private AntdUI.Label label7;
        private AntdUI.Switch switch7;
        private AntdUI.Label label8;
        private AntdUI.Switch switch8;
        private AntdUI.Label label9;
        private AntdUI.Switch switch9;
        private AntdUI.Label label10;
        private AntdUI.Switch switch10;
        private AntdUI.Button btnLog;
        private AntdUI.Button btnUpdate;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Panel panelUpdate;
        private AntdUI.Label lblCurrentVersion;
        private AntdUI.Label lblLatestVersion;
        private AntdUI.Label lblCurrentVersionTitle;
        private AntdUI.Label lblLatestVersionTitle;
        private AntdUI.Button btnCheckUpdate;
        private System.Windows.Forms.RichTextBox txtChangelog;
        private AntdUI.Button btnDownloadSetup;
        private AntdUI.Button btnDownloadPortable;
        private AntdUI.Button btnFallback;
        private AntdUI.Progress progressDownload;
        private AntdUI.Label lblDownloadStatus;
        private System.Windows.Forms.TableLayoutPanel tableUpdate;
        private System.Windows.Forms.Panel panelUpdateButtons;
    }
}
