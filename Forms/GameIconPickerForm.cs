using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public class GameIconPickerForm : UserControl
    {
        private static readonly (string IconName, string StoreName, string LabelZh, string LabelEn)[] Icons =
        {
            ("Arknights",      "明日方舟",        "明日方舟",        "Arknights (Official)"),
            ("BiliArknights",  "明日方舟(B服)",   "明日方舟(B服)",   "Arknights (Bilibili)"),
            ("Endfield",       "终末地",           "终末地",          "Endfield (Official)"),
            ("BiliEndfield",   "终末地(B服)",     "终末地(B服)",     "Endfield (Bilibili)"),
            ("GlobalEndfield", "终末地(国际服)",  "终末地(国际服)",  "Endfield (Global)"),
        };

        private readonly System.Collections.Generic.List<CardPanel> _cards = new();
        private readonly Overview _overview;

        public GameIconPickerForm(Overview overview)
        {
            _overview = overview;
            Font = new Font("Microsoft YaHei UI", 9F);
            Size = new Size(360, 180);
            BackColor = Color.Transparent;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(8, 8, 8, 8),
            };

            var lblTitle = new AntdUI.Label
            {
                Text = AntdUI.Localization.Get("App.Picker.Title", "选择要添加的游戏"),
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Font = new Font("Microsoft YaHei UI", 12F),
            };

            foreach (var (iconName, storeName, labelZh, labelEn) in Icons)
            {
                var key = iconName;
                var store = storeName;
                bool isEn = AntdUI.Localization.CurrentLanguage.StartsWith("en");
                var lbl = isEn ? labelEn : labelZh;
                var card = new CardPanel(key, lbl)
                {
                    Width = 120,
                    Height = 110,
                    Margin = new Padding(8),
                    Cursor = Cursors.Hand,
                };
                card.Click += (s, e) =>
                {
                    var cfg = ConfigHelper.Load();
                    if (cfg.Games.Exists(x => x.IconName == key))
                    {
                        AntdUI.Message.error(_overview, string.Format(AntdUI.Localization.Get("App.Picker.AlreadyAdded", "「{0}」已在列表中，不能重复添加。"), lbl));
                        return;
                    }
                    var entry = new GameEntry { Name = store, IconName = key };
                    cfg.Games.Add(entry);
                    ConfigHelper.Save(cfg);
                    _overview.RebuildSidebar();
                    _overview.RebuildGameButtons();
                    _overview.RebuildFloatMenu();
                    var f = FindForm();
                    if (f != null) f.Close();
                };
                _cards.Add(card);
                flow.Controls.Add(card);
            }

            Controls.Add(flow);
            Controls.Add(lblTitle);
        }

        private class CardPanel : Panel
        {
            private readonly string _iconName;
            private readonly string _label;
            private Bitmap _icon;
            private bool _hovered;
            private Color _bgNormal, _bgHover, _border, _fore;

            public CardPanel(string iconName, string label)
            {
                _iconName = iconName;
                _label = label;
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                _icon = LoadIcon(iconName);
                UpdateTheme(AntdUI.Config.IsDark);
            }

            public void UpdateTheme(bool dark)
            {
                if (dark)
                {
                    _bgNormal = Color.FromArgb(36, 36, 36);
                    _bgHover  = Color.FromArgb(55, 55, 55);
                    _border   = Color.FromArgb(60, 60, 60);
                    _fore     = Color.FromArgb(220, 220, 220);
                }
                else
                {
                    _bgNormal = Color.FromArgb(248, 248, 248);
                    _bgHover  = Color.FromArgb(235, 235, 235);
                    _border   = Color.FromArgb(220, 220, 220);
                    _fore     = Color.FromArgb(30, 30, 30);
                }
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = new Rectangle(1, 1, Width - 3, Height - 3);
                int r = 10;

                using var bgBrush = new SolidBrush(_hovered ? _bgHover : _bgNormal);
                using var path = RoundRect(rect, r);
                g.FillPath(bgBrush, path);

                var borderColor = _hovered ? AntdUI.Style.Db.Primary : _border;
                using var pen = new Pen(borderColor, _hovered ? 2f : 1f);
                g.DrawPath(pen, path);

                if (_icon != null)
                {
                    int iconSize = 56;
                    int ix = (Width - iconSize) / 2;
                    int iy = 18;
                    g.DrawImage(_icon, new Rectangle(ix, iy, iconSize, iconSize));
                }

                using var foreBrush = new SolidBrush(_fore);
                var textRect = new Rectangle(4, Height - 36, Width - 8, 32);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                };
                using var font = new Font("Microsoft YaHei UI", 8.5F);
                g.DrawString(_label, font, foreBrush, textRect, sf);
            }

            private static GraphicsPath RoundRect(Rectangle r, int radius)
            {
                int d = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            private static Bitmap LoadIcon(string key)
            {
                try
                {
                    string basePath = Path.Combine(AppContext.BaseDirectory, "Resources");
                    string file = key switch
                    {
                        "Arknights"      => "Arknights.ico",
                        "BiliArknights"  => "BiliArknights.ico",
                        "Endfield"       => "Endfield.ico",
                        "BiliEndfield"   => "BiliEndfield.ico",
                        "GlobalEndfield" => "GlobalEndfield.ico",
                        _ => null
                    };
                    if (file == null) return null;
                    string full = Path.Combine(basePath, file);
                    if (!File.Exists(full)) return null;
                    using var ico = new Icon(full, new Size(256, 256));
                    var src = ico.ToBitmap();
                    var dst = new Bitmap(56, 56);
                    using var g = Graphics.FromImage(dst);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, 56, 56);
                    return key == "GlobalEndfield" ? ApplyRoundedCorners(dst, 12) : dst;
                }
                catch { return null; }
            }

            private static Bitmap ApplyRoundedCorners(Bitmap src, int radius)
            {
                var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(dst);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var rect = new Rectangle(0, 0, src.Width, src.Height);
                int d = radius * 2;
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.SetClip(path);
                g.DrawImage(src, rect);
                return dst;
            }
        }
    }
}
