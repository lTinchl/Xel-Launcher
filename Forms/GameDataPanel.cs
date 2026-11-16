using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XelLauncher.Forms;

internal sealed class GameDataPanel : Control
{
    private readonly Dictionary<string, Bitmap> icons = new();
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
    private void DrawDetails(Graphics g)
    {
        var rows = details.Split('\n');
        if (rows.Length != 3 || !rows[0].StartsWith("理智 ", StringComparison.Ordinal))
        {
            TextRenderer.DrawText(g, details, Font, new Rectangle(9, 29, Width - 18, Height - 34),
                ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return;
        }

        string[] names = { "sanity", "recruit", "daily", "weekly", "annihilation", "security" };
        int columnWidth = (Width - 18) / 2;
        for (int row = 0; row < rows.Length; row++)
        {
            var cells = rows[row].Split(new[] { "     " }, StringSplitOptions.None);
            for (int column = 0; column < Math.Min(2, cells.Length); column++)
            {
                int x = 9 + column * columnWidth;
                int y = 29 + row * 17;
                var icon = GetIcon(names[row * 2 + column]);
                if (icon != null) g.DrawImage(icon, new Rectangle(x, y + 1, 14, 14));
                TextRenderer.DrawText(g, cells[column], Font,
                    new Rectangle(x + 18, y, columnWidth - 20, 17), ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    private Bitmap GetIcon(string name)
    {
        if (icons.TryGetValue(name, out var icon)) return icon;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Icon", name + ".svg");
            // AntdUI renders the named SVG color white as transparent; use its RGB equivalent.
            var svg = File.ReadAllText(path).Replace("\"white\"", "\"#FFFFFF\"");
            if (name == "annihilation")
            {
                // This opaque white mask is equivalent to a clip path. Avoid
                // AntdUI's native-size opacity layer, which blurs the vector.
                var document = XDocument.Parse(svg);
                foreach (var mask in document.Descendants().Where(element => element.Name.LocalName == "mask").ToArray())
                {
                    mask.Name = mask.Name.Namespace + "clipPath";
                    mask.Attribute("style")?.Remove();
                    mask.Attribute("maskUnits")?.Remove();
                }
                foreach (var element in document.Descendants())
                {
                    if (element.Attribute("mask") is { } mask)
                    {
                        element.SetAttributeValue("clip-path", mask.Value);
                        mask.Remove();
                    }
                    element.Attribute("opacity")?.Remove();
                }
                using var rendered = AntdUI.SvgExtend.SvgToBmp(document.ToString(), 56, 56, null);
                if (rendered != null)
                {
                    icon = new Bitmap(56, 56);
                    using var graphics = Graphics.FromImage(icon);
                    using var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.5F });
                    graphics.DrawImage(rendered, new Rectangle(0, 0, 56, 56),
                        0, 0, rendered.Width, rendered.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            else
                icon = AntdUI.SvgExtend.SvgToBmp(svg, 28, 28, null);
        }
        catch (Exception ex)
        {
            XelLauncher.Helpers.LogHelper.LogError(ex, "Load dashboard icon: " + name);
        }
        icons[name] = icon;
        return icon;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var icon in icons.Values) icon?.Dispose();
            icons.Clear();
        }
        base.Dispose(disposing);
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
        if (expanded) DrawDetails(g);
        if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 6, 20));
    }
}
