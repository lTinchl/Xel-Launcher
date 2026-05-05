
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher
{
    public partial class Main : AntdUI.Window
    {
        public Main()
        {
            InitializeComponent();
            if (AntdUI.Config.IsDark)
            {
                BackColor = AppTheme.DarkBackground;
                ForeColor = AppTheme.DarkForeground;
                panel_top.BackColor = AppTheme.DarkHeader;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            DraggableMouseDown();
            base.OnMouseDown(e);
        }
    }
}
