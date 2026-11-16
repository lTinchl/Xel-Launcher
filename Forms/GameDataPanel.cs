using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms;

internal sealed class GameDataPanel : Control
{
    private readonly Dictionary<string, Bitmap> icons = new();
    private Font valueFont;
    private AntdUI.Button disclaimerButton;
    private AntdUI.TooltipComponent disclaimerTooltip;
    private Color accentColor = Color.FromArgb(48, 214, 238);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get => accentColor; set { accentColor = value; Invalidate(); } }
    private bool expanded;
    private readonly bool supportsGameData;
    private string summary = "";
    private string details = Localizer.GetRequiredString("App.Game.Dashboard.ExpandToLoad");
    public event EventHandler Expanded;
    public event EventHandler ExpansionChanged;
    public bool IsExpanded => expanded;
    public GameDataPanel(bool supportsGameData = true)
    {
        this.supportsGameData = supportsGameData;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        ForeColor = Color.Gainsboro;
        Font = new Font("Microsoft YaHei UI", 8F);
        UpdateLayout();
        Cursor = Cursors.Default;
        TabStop = supportsGameData;
        AccessibleName = Localizer.GetRequiredString(supportsGameData
            ? "App.Game.Dashboard.AccessibleName" : "App.Game.Dashboard.Playtime");
        AccessibleRole = supportsGameData ? AccessibleRole.PushButton : AccessibleRole.StaticText;
        disclaimerButton = new AntdUI.Button
        {
            IconSvg = "QuestionCircleFilled",
            Ghost = true,
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.Transparent,
            BorderWidth = 0,
            Radius = 99,
            Padding = Padding.Empty,
            Cursor = Cursors.Help,
            AccessibleName = XelLauncher.Helpers.Localizer.GetRequiredString("App.Game.DataDisclaimer"),
            Visible = false
        };
        disclaimerTooltip = new AntdUI.TooltipComponent { Delay = 250, Radius = 8, ArrowAlign = AntdUI.TAlign.Left , Font = new Font("Microsoft YaHei UI", 11F) };
        disclaimerTooltip.SetTip(disclaimerButton, XelLauncher.Helpers.Localizer.GetRequiredString("App.Game.DataDisclaimer"));
        Controls.Add(disclaimerButton);
        UpdateLayout();
    }
    private int availableWidth = int.MaxValue;
    private int columns = 2;
    private int columnWidth;
    private int headerHeight;
    private int rowHeight;
    private int bodyHeight;
    private int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96F));
    private string[] SummaryCells => summary.Split(new[] { "    " }, 2, StringSplitOptions.None);
    private string SanityPrefix => Localizer.GetRequiredString("App.Game.Dashboard.Sanity") + " ";
    private string Identity => details.Contains("\n" + SanityPrefix) ? details.Substring(0, details.IndexOf("\n" + SanityPrefix, StringComparison.Ordinal)) : "";
    private string DataText => Identity.Length > 0 ? details.Substring(Identity.Length + 1) : details;
    private int IdentityHeight => HasData && Identity.Length > 0 ? rowHeight + S(3) : 0;
    private string[] DetailCells => DataText.Split(new[] { "\n", "     " }, StringSplitOptions.None);
    private bool HasData => DetailCells.Length is 5 or 6 && DataText.StartsWith(SanityPrefix, StringComparison.Ordinal);
    public event EventHandler LayoutMetricsChanged;
    public void SetAvailableWidth(int value)
    {
        value = Math.Max(1, value);
        if (availableWidth == value) return;
        availableWidth = value;
        UpdateLayout();
    }
    public void SetSummary(string value) { if (summary == value) return; summary = value; UpdateLayout(); }
    public void SetDetails(string value) { details = value; UpdateLayout(); }
    private int Measure(string text) => TextRenderer.MeasureText(text, Font, Size.Empty,
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
    private void UpdateLayout()
    {
        if (Font == null) return;
        int preferredColumn = Math.Max(Measure(string.Format(Localizer.GetRequiredString("App.Game.Dashboard.TotalPlaytime"), 0D)), SummaryCells.Max(Measure));
        if (HasData) preferredColumn = Math.Max(preferredColumn, DetailCells.Max(MeasureCell) + S(18));
        preferredColumn += S(8);
        int preferredWidth = preferredColumn * 2 + S(18);
        columns = availableWidth < preferredWidth ? 1 : 2;
        int width = Math.Min(availableWidth, preferredColumn * columns + S(18));
        if (Identity.Length > 0) width = Math.Min(availableWidth, Math.Max(width, Measure(Identity) + S(40)));
        columnWidth = Math.Max(1, (width - S(18)) / columns);
        rowHeight = Math.Max((valueFont ?? Font).Height + S(3), S(17));
        headerHeight = rowHeight * (columns == 1 ? SummaryCells.Length : 1) + S(9);
        bodyHeight = headerHeight;
        if (expanded)
        {
            int detailHeight = HasData ? rowHeight * ((DetailCells.Length + columns - 1) / columns) + IdentityHeight :
                TextRenderer.MeasureText(details, Font, new Size(Math.Max(1, width - S(18)), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            bodyHeight += S(3) + detailHeight + S(5);
        }
        Size = new Size(Math.Max(1, width), bodyHeight + (supportsGameData ? S(12) : 0) + 1);
        Invalidate();
        if (disclaimerButton != null)
        {
            disclaimerButton.Visible = expanded && IdentityHeight > 0;
            if (expanded && IdentityHeight > 0)
            {
                int split = Identity.LastIndexOf(" Lv.", StringComparison.Ordinal);
                string name = split >= 0 ? Identity.Substring(0, split) : Identity;
                string level = split >= 0 ? Identity.Substring(split + 1) : "";
                int levelWidth = Measure(level);
                int nameWidth = Math.Min(Measure(name), Math.Max(1, Width - S(46) - levelWidth));
                disclaimerButton.Bounds = new Rectangle(S(21) + nameWidth + levelWidth,
                    headerHeight + (rowHeight - S(16)) / 2, S(16), S(16));
                disclaimerTooltip.CustomWidth = S(280);
            }
        }
        LayoutMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        valueFont?.Dispose();
        valueFont = new Font(Font.FontFamily, Font.Size + 1F, FontStyle.Bold, Font.Unit);
        UpdateLayout();
    }
    private string[] CellParts(string text)
    {
        int space = text.LastIndexOf(' ');
        int slash = text.IndexOf('/', Math.Max(0, space));
        if (space < 0 || slash < 0) return new[] { text, "", "" };
        return new[] { text.Substring(0, space + 1), text.Substring(space + 1, slash - space - 1), text.Substring(slash) };
    }
    private int MeasureCell(string text)
    {
        var parts = CellParts(text);
        return Measure(parts[0]) + TextRenderer.MeasureText(parts[1], valueFont ?? Font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + Measure(parts[2]);
    }
    private void DrawCell(Graphics g, string text, Rectangle bounds)
    {
        var parts = CellParts(text);
        for (int i = 0; i < parts.Length && bounds.Width > 0; i++)
        {
            var font = i == 1 ? valueFont ?? Font : Font;
            int width = TextRenderer.MeasureText(parts[i], font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            TextRenderer.DrawText(g, parts[i], font, new Rectangle(bounds.X, bounds.Y, Math.Min(width, bounds.Width), bounds.Height),
                i == 1 ? AccentColor : ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            bounds.X += width;
            bounds.Width -= width;
        }
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        foreach (var icon in icons.Values) icon?.Dispose();
        icons.Clear();
        UpdateLayout();
    }
    private void Toggle() => SetExpanded(!expanded);
    public void SetExpanded(bool value)
    {
        if (!supportsGameData) return;
        if (expanded == value) return;
        expanded = value;
        UpdateLayout();
        ExpansionChanged?.Invoke(this, EventArgs.Empty);
        if (expanded) Expanded?.Invoke(this, EventArgs.Empty);
    }
    private Rectangle ToggleBounds => new Rectangle(Width / 2 - S(22), bodyHeight - S(6), S(44), S(19));    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = supportsGameData && ToggleBounds.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (supportsGameData && e.Button == MouseButtons.Left && ToggleBounds.Contains(e.Location))
        {
            Focus();
            Toggle();
        }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) { Toggle(); e.Handled = true; }
    }
    private void DrawDetails(Graphics g)
    {
        if (!HasData)
        {
            TextRenderer.DrawText(g, details, Font,
                new Rectangle(S(9), headerHeight + S(3), Math.Max(1, Width - S(18)), bodyHeight - headerHeight),
                ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return;
        }
        string[] names = DetailCells.Length == 5
            ? new[] { "sanity", "endfield-daily", "endfield-weekly", "endfield-protocol", "endfield-authority" }
            : new[] { "sanity", "recruit", "daily", "weekly", "annihilation", "security" };
        var cells = DetailCells;
        if (IdentityHeight > 0)
        {
            int split = Identity.LastIndexOf(" Lv.", StringComparison.Ordinal);
            string name = split >= 0 ? Identity.Substring(0, split) : Identity;
            string level = split >= 0 ? Identity.Substring(split + 1) : "";
            int levelWidth = Measure(level);
            int nameWidth = Math.Min(Measure(name), Math.Max(1, Width - S(46) - levelWidth));
            TextRenderer.DrawText(g, name, Font, new Rectangle(S(9), headerHeight, nameWidth, rowHeight), ForeColor,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, level, Font, new Rectangle(S(15) + nameWidth, headerHeight, levelWidth, rowHeight), AccentColor,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
        }
        for (int index = 0; index < cells.Length; index++)
        {
            int x = S(9) + index % columns * columnWidth;
            int y = headerHeight + IdentityHeight + S(3) + index / columns * rowHeight;
            var icon = GetIcon(names[index]);
            if (icon != null) g.DrawImage(icon, new Rectangle(x, y + (rowHeight - S(14)) / 2, S(14), S(14)));
            DrawCell(g, cells[index], new Rectangle(x + S(18), y, Math.Max(1, columnWidth - S(20)), rowHeight));
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
                using var rendered = AntdUI.SvgExtend.SvgToBmp(document.ToString(), S(56), S(56), null);
                if (rendered != null)
                {
                    icon = new Bitmap(S(56), S(56));
                    using var graphics = Graphics.FromImage(icon);
                    using var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.5F });
                    graphics.DrawImage(rendered, new Rectangle(0, 0, S(56), S(56)),
                        0, 0, rendered.Width, rendered.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            else
                icon = AntdUI.SvgExtend.SvgToBmp(svg, S(28), S(28), null);
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
            valueFont?.Dispose();
            disclaimerTooltip?.Dispose();
        }
        base.Dispose(disposing);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        var w = Width - 1; var h = bodyHeight;
        float center = w / 2F;
        float radius = Math.Min(S(8), Math.Min(w / 2F, h / 2F));
        if (radius <= 0) return;
        float diameter = radius * 2;
        float tab = Math.Min(S(24), Math.Max(1, center - radius));
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(w - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(w - diameter, h - diameter, diameter, diameter, 0, 90);
        if (supportsGameData)
        {
            path.AddLine(w - radius, h, center + tab, h);
            path.AddBezier(center + tab, h, center + tab / 2, h, center + tab / 2, h + S(12), center, h + S(12));
            path.AddBezier(center, h + S(12), center - tab / 2, h + S(12), center - tab / 2, h, center - tab, h);
            path.AddLine(center - tab, h, radius, h);
        }
        path.AddArc(0, h - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();        using var brush = new SolidBrush(Color.FromArgb(188, 34, 37, 43));
        using var border = new Pen(Color.FromArgb(24, 255, 255, 255));
        g.FillPath(brush, path); g.DrawPath(border, path);
        var summaryCells = SummaryCells;
        for (int index = 0; index < summaryCells.Length; index++)
            TextRenderer.DrawText(g, summaryCells[index], Font,
                new Rectangle(S(9) + index % columns * columnWidth, S(4) + index / columns * rowHeight,
                    Math.Max(1, columnWidth - S(4)), rowHeight), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);        using var arrow = new Pen(ForeColor, 1.3F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (!supportsGameData) return;
        float arrowY = h + S(5);
        g.DrawLines(arrow, expanded
            ? new[] { new PointF(center - S(3), arrowY + S(2)), new PointF(center, arrowY - S(1)), new PointF(center + S(3), arrowY + S(2)) }
            : new[] { new PointF(center - S(3), arrowY - S(1)), new PointF(center, arrowY + S(2)), new PointF(center + S(3), arrowY - S(1)) });
        if (expanded) DrawDetails(g);
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Color.FromArgb(120, ForeColor));
            g.DrawEllipse(focus, center - S(8), h - S(2), S(16), S(12));
        }
    }
}
