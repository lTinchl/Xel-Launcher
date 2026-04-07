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
            btnMaa = new AntdUI.Button();
            dividerV = new AntdUI.Divider();
            panelRight = new System.Windows.Forms.Panel();
            panelLog = new System.Windows.Forms.Panel();
            txtLog = new System.Windows.Forms.RichTextBox();
            panelMaa = new System.Windows.Forms.Panel();
            tableMaa = new System.Windows.Forms.TableLayoutPanel();
            labelMaaOfficial = new AntdUI.Label();
            labelMaaBilibili = new AntdUI.Label();
            txtMaaOfficial = new AntdUI.Input();
            txtMaaBilibili = new AntdUI.Input();
            btnMaaOfficialBrowse = new AntdUI.Button();
            btnMaaBilibiliBrowse = new AntdUI.Button();
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
            panelLeft.SuspendLayout();
            panelRight.SuspendLayout();
            scrollSoftware.SuspendLayout();
            panelLog.SuspendLayout();
            panelMaa.SuspendLayout();
            tableMaa.SuspendLayout();
            tableSoftware.SuspendLayout();
            SuspendLayout();
            //
            // panelLeft
            //
            panelLeft.Controls.Add(btnLog);
            panelLeft.Controls.Add(btnMaa);
            panelLeft.Controls.Add(btnSoftware);
            panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            panelLeft.Width = 120;
            panelLeft.Name = "panelLeft";
            //
            // btnSoftware
            //
            btnSoftware.Text = "软件设置";
            btnSoftware.Dock = System.Windows.Forms.DockStyle.Top;
            btnSoftware.Height = 46;
            btnSoftware.Name = "btnSoftware";
            btnSoftware.Type = AntdUI.TTypeMini.Primary;
            //
            // btnLog
            //
            btnLog.Text = "软件日志";
            btnLog.Dock = System.Windows.Forms.DockStyle.Top;
            btnLog.Height = 46;
            btnLog.Name = "btnLog";
            btnLog.Type = AntdUI.TTypeMini.Default;
            //
            // btnMaa
            //
            //btnMaa.Text = "路径设置";
            //btnMaa.Dock = System.Windows.Forms.DockStyle.Top;
            //btnMaa.Height = 46;
            //btnMaa.Name = "btnMaa";
            //btnMaa.Type = AntdUI.TTypeMini.Default;
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
            panelRight.Controls.Add(panelMaa);
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
            // panelMaa
            //
            panelMaa.Controls.Add(tableMaa);
            panelMaa.Dock = System.Windows.Forms.DockStyle.Fill;
            panelMaa.Name = "panelMaa";
            panelMaa.Visible = false;
            //
            // tableMaa
            //
            tableMaa.ColumnCount = 3;
            tableMaa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            tableMaa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableMaa.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            tableMaa.Controls.Add(labelMaaOfficial, 0, 0);
            tableMaa.Controls.Add(txtMaaOfficial, 1, 0);
            tableMaa.Controls.Add(btnMaaOfficialBrowse, 2, 0);
            tableMaa.Controls.Add(labelMaaBilibili, 0, 1);
            tableMaa.Controls.Add(txtMaaBilibili, 1, 1);
            tableMaa.Controls.Add(btnMaaBilibiliBrowse, 2, 1);
            tableMaa.Dock = System.Windows.Forms.DockStyle.Fill;
            tableMaa.RowCount = 3;
            tableMaa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            tableMaa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            tableMaa.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableMaa.Name = "tableMaa";
            //
            // labelMaaOfficial
            //
            //labelMaaOfficial.Dock = System.Windows.Forms.DockStyle.Fill;
            //labelMaaOfficial.Name = "labelMaaOfficial";
            //labelMaaOfficial.Text = "MAA 路径";
            //labelMaaOfficial.TabIndex = 0;
            //
            // txtMaaOfficial
            //
            txtMaaOfficial.Dock = System.Windows.Forms.DockStyle.Fill;
            txtMaaOfficial.Name = "txtMaaOfficial";
            txtMaaOfficial.PlaceholderText = "请选择 MAA 官服路径...";
            txtMaaOfficial.TabIndex = 0;
            txtMaaOfficial.Margin = new System.Windows.Forms.Padding(0, 10, 6, 10);
            //
            // btnMaaOfficialBrowse
            //
            btnMaaOfficialBrowse.Text = "浏览";
            btnMaaOfficialBrowse.Dock = System.Windows.Forms.DockStyle.Fill;
            btnMaaOfficialBrowse.Name = "btnMaaOfficialBrowse";
            btnMaaOfficialBrowse.Type = AntdUI.TTypeMini.Default;
            btnMaaOfficialBrowse.Margin = new System.Windows.Forms.Padding(0, 10, 0, 10);
            //
            // labelMaaBilibili
            //
            labelMaaBilibili.Dock = System.Windows.Forms.DockStyle.Fill;
            labelMaaBilibili.Name = "labelMaaBilibili";
            labelMaaBilibili.Text = "MAA B服路径";
            labelMaaBilibili.TabIndex = 0;
            //
            // txtMaaBilibili
            //
            txtMaaBilibili.Dock = System.Windows.Forms.DockStyle.Fill;
            txtMaaBilibili.Name = "txtMaaBilibili";
            txtMaaBilibili.PlaceholderText = "请选择 MAA B服路径...";
            txtMaaBilibili.TabIndex = 0;
            txtMaaBilibili.Margin = new System.Windows.Forms.Padding(0, 10, 6, 10);
            //
            // btnMaaBilibiliBrowse
            //
            btnMaaBilibiliBrowse.Text = "浏览";
            btnMaaBilibiliBrowse.Dock = System.Windows.Forms.DockStyle.Fill;
            btnMaaBilibiliBrowse.Name = "btnMaaBilibiliBrowse";
            btnMaaBilibiliBrowse.Type = AntdUI.TTypeMini.Default;
            btnMaaBilibiliBrowse.Margin = new System.Windows.Forms.Padding(0, 10, 0, 10);
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
            tableSoftware.Dock = System.Windows.Forms.DockStyle.Top;
            tableSoftware.AutoSize = true;
            tableSoftware.RowCount = 10;
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
            label9.TabIndex = 0;
            //
            // switch9
            //
            switch9.Anchor = System.Windows.Forms.AnchorStyles.None;
            switch9.Name = "switch9";
            switch9.Size = new System.Drawing.Size(50, 30);
            switch9.TabIndex = 0;
            //
            // Setting
            //
            Controls.Add(panelRight);
            Controls.Add(dividerV);
            Controls.Add(panelLeft);
            Name = "Setting";
            Size = new System.Drawing.Size(600, 400);
            tableSoftware.ResumeLayout(false);
            tableMaa.ResumeLayout(false);
            panelMaa.ResumeLayout(false);
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
        private AntdUI.Button btnMaa;
        private AntdUI.Button btnLog;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Panel panelMaa;
        private System.Windows.Forms.TableLayoutPanel tableMaa;
        private AntdUI.Label labelMaaOfficial;
        private AntdUI.Label labelMaaBilibili;
        private AntdUI.Input txtMaaOfficial;
        private AntdUI.Input txtMaaBilibili;
        private AntdUI.Button btnMaaOfficialBrowse;
        private AntdUI.Button btnMaaBilibiliBrowse;
    }
}
