namespace XelLauncher.Forms
{
    partial class TabHeaderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            AntdUI.TagTabItem tagTabItem1 = new AntdUI.TagTabItem();
            tabHeader1 = new AntdUI.TabHeader();
            SuspendLayout();

            // tabHeader1
            tabHeader1.Dock = System.Windows.Forms.DockStyle.Top;
            tabHeader1.DragSort = true;
            tabHeader1.ShowAdd = true;
            tabHeader1.ShowButton = true;
            tabHeader1.ShowIcon = true;
            tabHeader1.IconSvg = "ChromeFilled";
            tabHeader1.BackActive = System.Drawing.Color.White;
            tabHeader1.BackColor = System.Drawing.Color.FromArgb(232, 232, 232);
            tabHeader1.BorderWidth = 1F;
            tabHeader1.Size = new System.Drawing.Size(1200, 44);
            tabHeader1.TabIndex = 0;
            tabHeader1.AddClick += tabHeader1_AddClick;
            tabHeader1.TabChanged += tabHeader1_TabChanged;
            tabHeader1.TabClosing += tabHeader1_TabClosing;

            // TabHeaderForm
            ClientSize = new System.Drawing.Size(1200, 800);
            Controls.Add(tabHeader1);
            Icon = Properties.Resources.icon;
            Text = "浏览器";
            ResumeLayout(false);
        }

        private AntdUI.TabHeader tabHeader1;
    }
}