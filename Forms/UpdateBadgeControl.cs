using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XelLauncher.Forms
{
    /// <summary>
    /// 红色圆形微标控件，叠加在版本号右上角，有新版本时显示。
    /// </summary>
    public class UpdateBadgeControl : Control
    {
        public UpdateBadgeControl()
        {
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Size = new Size(10, 10);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 描边颜色跟随父控件背景，深色模式下用深色描边（自然融入），浅色模式下用白色
            var borderColor = Parent?.BackColor ?? Color.White;

            // 描边（1px，向外扩展半像素实现抗锯齿融合）
            using var borderBrush = new SolidBrush(borderColor);
            g.FillEllipse(borderBrush, -1f, -1f, Width + 2f, Height + 2f);

            // 红色主体
            using var redBrush = new SolidBrush(Color.FromArgb(255, 77, 79));
            g.FillEllipse(redBrush, 0.5f, 0.5f, Width - 1f, Height - 1f);
        }
    }
}
