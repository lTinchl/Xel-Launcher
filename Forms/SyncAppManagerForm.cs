using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public class SyncAppManagerForm : UserControl, AntdUI.ControlEvent
    {
        private readonly GameEntry _game;
        private readonly Overview _overview;
        private Panel _listPanel;

        public void LoadCompleted() { }

        public SyncAppManagerForm(GameEntry game, Overview overview)
        {
            _game = game;
            _overview = overview;

            Font = new Font("Microsoft YaHei UI", 10F);
            Size = new Size(360, 428);
            BackColor = Color.White;

            var pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(360, 56),
                BackColor = Color.White,
            };

            var lblTitle = new AntdUI.Label
            {
                Text = "联动启动管理",
                Location = new Point(20, 14),
                Size = new Size(200, 28),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            };

            var btnAdd = new AntdUI.Button
            {
                Text = "+ 添加",
                Location = new Point(258, 13),
                Size = new Size(82, 30),
                Ghost = true,
            };
            btnAdd.Click += (s, e) => AddApp();

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnAdd);

            var dividerTop = new AntdUI.Divider
            {
                Location = new Point(0, 56),
                Size = new Size(360, 1),
                Thickness = 1F,
            };

            _listPanel = new Panel
            {
                Location = new Point(0, 64),
                Size = new Size(360, 300),
                AutoScroll = true,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(0, 8, 0, 8),
            };

            var dividerBottom = new AntdUI.Divider
            {
                Location = new Point(0, 372),
                Size = new Size(360, 1),
                Thickness = 1F,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };

            var btnBack = new AntdUI.Button
            {
                Text = "← 返回",
                Location = new Point(16, 382),
                Size = new Size(328, 32),
                Ghost = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            btnBack.Click += (s, e) => FindForm()?.Close();

            Controls.Add(pnlHeader);
            Controls.Add(dividerTop);
            Controls.Add(_listPanel);
            Controls.Add(dividerBottom);
            Controls.Add(btnBack);

            RefreshList();
        }

        private void RefreshList()
        {
            _listPanel.Controls.Clear();
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            var apps = entry?.SyncApps ?? new List<SyncApp>();

            if (apps.Count == 0)
            {
                var pnlEmpty = new Panel
                {
                    Location = new Point(0, 80),
                    Size = new Size(328, 80),
                    BackColor = Color.White,
                };

                var lblEmpty = new AntdUI.Label
                {
                    Text = "暂无联动软件",
                    Location = new Point(0, 0),
                    Size = new Size(328, 28),
                    Font = new Font("Microsoft YaHei UI", 10F),
                    ForeColor = Color.FromArgb(180, 180, 180),
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                var lblEmptySub = new AntdUI.Label
                {
                    Text = "点击右上角「+ 添加」来添加软件",
                    Location = new Point(0, 28),
                    Size = new Size(328, 24),
                    ForeColor = Color.FromArgb(200, 200, 200),
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                pnlEmpty.Controls.Add(lblEmpty);
                pnlEmpty.Controls.Add(lblEmptySub);
                _listPanel.Controls.Add(pnlEmpty);
                return;
            }

            int y = 8;
            foreach (var app in apps)
            {
                var appCopy = app;

                var card = new Panel
                {
                    Location = new Point(16, y),
                    Size = new Size(316, 56),
                    BackColor = Color.FromArgb(250, 250, 250),
                };

                card.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var pen = new Pen(Color.FromArgb(235, 235, 235), 1);
                    var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    int r = 8;
                    var path = new System.Drawing.Drawing2D.GraphicsPath();
                    path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
                    path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
                    path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                };

                var picIcon = new PictureBox
                {
                    Location = new Point(12, 12),
                    Size = new Size(32, 32),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                };
                try
                {
                    var ico = System.Drawing.Icon.ExtractAssociatedIcon(appCopy.Path);
                    if (ico != null) picIcon.Image = ico.ToBitmap();
                }
                catch { }

                var lblName = new Label
                {
                    Text = appCopy.Name,
                    Location = new Point(52, 10),
                    Size = new Size(196, 20),
                    Font = new Font("Microsoft YaHei UI", 9.5F),
                    ForeColor = Color.FromArgb(30, 30, 30),
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                };

                var lblPath = new Label
                {
                    Text = appCopy.Path,
                    Location = new Point(52, 30),
                    Size = new Size(196, 18),
                    Font = new Font("Microsoft YaHei UI", 7.5F),
                    ForeColor = Color.FromArgb(160, 160, 160),
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                };

                var btnDel = new AntdUI.Button
                {
                    Text = "删除",
                    Location = new Point(256, 13),
                    Size = new Size(52, 28),
                    Ghost = true,
                    ForeColor = Color.FromArgb(255, 77, 79),
                };
                btnDel.Click += (s, e) =>
                {
                    var c = ConfigHelper.Load();
                    var en = c.Games.Find(g => g.IconName == _game.IconName);
                    en?.SyncApps.RemoveAll(a => a.Path == appCopy.Path);
                    ConfigHelper.Save(c);
                    RefreshList();
                };

                card.Controls.Add(picIcon);
                card.Controls.Add(lblName);
                card.Controls.Add(lblPath);
                card.Controls.Add(btnDel);
                _listPanel.Controls.Add(card);

                y += 64;
            }

            _listPanel.AutoScrollMinSize = new Size(0, y + 8);
        }

        private void AddApp()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "选择要联动启动的程序",
                Filter = "可执行文件 (*)|*",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            string path = dlg.FileName;
            string name = Path.GetFileNameWithoutExtension(path);

            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            if (entry == null) return;

            if (entry.SyncApps.Exists(a => a.Path == path))
            {
                AntdUI.Message.warn(_overview, "该软件已在列表中");
                return;
            }

            entry.SyncApps.Add(new SyncApp { Name = name, Path = path });
            ConfigHelper.Save(cfg);
            RefreshList();
            AntdUI.Message.success(_overview, $"已添加：{name}");
        }
    }
}