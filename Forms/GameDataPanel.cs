using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XelLauncher.Forms;

internal sealed class GameDataPanel : Control
{
    private bool expanded;
    private string summary = "";
    private string details = "展开以加载游戏数据";
    public event EventHandler Expanded;
    public GameDataPanel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = Color.Gainsboro;
        Font = new Font("Microsoft YaHei UI", 8F);
        Size = new Size(260, 26);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = "游戏时长，展开或收起游戏数据";
        AccessibleRole = AccessibleRole.PushButton;
    }
    public void SetSummary(string value) { summary = value; Invalidate(); }
    public void SetDetails(string value) { details = value; Invalidate(); }
    private void Toggle()
    {
        expanded = !expanded;
        Height = expanded ? 84 : 26;
        Invalidate();
        if (expanded) Expanded?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); if (e.Button == MouseButtons.Left && e.Y < 26) Toggle(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) { Toggle(); e.Handled = true; }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        var w = Width - 1; var h = Height - 1;
        path.AddArc(0, 0, 16, 16, 180, 90); path.AddArc(w - 16, 0, 16, 16, 270, 90);
        path.AddArc(w - 16, h - 16, 16, 16, 0, 90); path.AddArc(0, h - 16, 16, 16, 90, 90); path.CloseFigure();
        using var brush = new SolidBrush(Color.FromArgb(100, 34, 37, 43));
        using var border = new Pen(Color.FromArgb(24, 255, 255, 255));
        g.FillPath(brush, path); g.DrawPath(border, path);
        TextRenderer.DrawText(g, summary, Font, new Rectangle(9, 0, Width - 34, 26), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, expanded ? "▴" : "▾", Font, new Rectangle(Width - 24, 0, 20, 26), ForeColor, TextFormatFlags.VerticalCenter);
        if (expanded) TextRenderer.DrawText(g, details, Font, new Rectangle(9, 29, Width - 18, Height - 34), ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 6, 20));
    }
}
