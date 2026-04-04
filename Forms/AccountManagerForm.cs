using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    public class AccountManagerForm : UserControl
    {
        private readonly Overview _overview;
        private readonly GamePage _gamePage;
        private readonly string _iconName;
        private AntdUI.Table table;
        private readonly HashSet<string> _pendingDelete = new();

        private bool IsEndfield => _iconName == "Endfield";

        public AccountManagerForm(Overview overview, GamePage gamePage, string iconName = "Arknights")
        {
            _overview = overview;
            _gamePage = gamePage;
            _iconName = iconName;
            Font = new Font("Microsoft YaHei UI", 10F);
            Size = new Size(720, 560);
            BackColor = Color.Transparent;

            var lblTitle = new AntdUI.Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            };

            table = new AntdUI.Table
            {
                Dock = DockStyle.Fill,
                Radius = 6,
                BorderWidth = 1F,
                Font = new Font("Microsoft YaHei UI", 10F),
            };
            table.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.ColumnSort() { Fixed = true },
                new AntdUI.Column("name", "账号名称", AntdUI.ColumnAlign.Center),
                new AntdUI.Column("isDefault", "默认", AntdUI.ColumnAlign.Center).SetWidth("80"),
                new AntdUI.Column("isEnabled", "状态", AntdUI.ColumnAlign.Center).SetWidth("80"),
                new AntdUI.ColumnSwitch("enabledSwitch", "启用", AntdUI.ColumnAlign.Center).SetWidth("80"),
                new AntdUI.Column("action", "操作", AntdUI.ColumnAlign.Center).SetWidth("320"),
            };
            table.CellButtonClick += Table_CellButtonClick;
            table.SortRows += Table_SortRows;
            table.CheckedChanged += Table_CheckedChanged;

            var btndune = new AntdUI.Button
            {
                Text = "完成",
                Dock = DockStyle.Bottom,
                Height = 40,
                Type = AntdUI.TTypeMini.Primary,
                Radius = 6,
                Margin = new Padding(12, 4, 12, 12),
            };
            btndune.Click += (s, e) => { var f = FindForm(); if (f != null) f.DialogResult = DialogResult.OK; };

            var btnAdd = new AntdUI.Button
            {
                Text = "+ 添加账号",
                Dock = DockStyle.Bottom,
                Height = 40,
                Ghost = true,
                Radius = 6,
                Margin = new Padding(12, 4, 12, 12),
            };
            btnAdd.Click += (s, e) => ShowAddDialog();

            Controls.Add(table);
            Controls.Add(btnAdd);
            Controls.Add(btndune);
            Controls.Add(lblTitle);

            RefreshTable();
        }

        private (Dictionary<string, string> accounts, List<string> order, string defaultId, HashSet<string> disabled) GetAccountData(AppConfig cfg)
        {
            if (IsEndfield)
                return (cfg.EndfieldAccounts, cfg.EndfieldAccountOrder, cfg.EndfieldDefaultAccount, cfg.EndfieldDisabledAccounts);
            return (cfg.Accounts, cfg.AccountOrder, cfg.DefaultAccount, cfg.DisabledAccounts);
        }

        private List<string> GetOrderedIds()
        {
            var cfg = ConfigHelper.Load();
            var (accounts, order, _, _) = GetAccountData(cfg);
            var ordered = order.Where(id => accounts.ContainsKey(id)).ToList();
            foreach (var id in accounts.Keys)
                if (!ordered.Contains(id)) ordered.Add(id);
            return ordered;
        }

        private void RefreshTable()
        {
            var cfg = ConfigHelper.Load();
            var (accounts, _, defaultId, disabled) = GetAccountData(cfg);
            var rows = new List<AccountRow>();
            var ordered = GetOrderedIds();
            for (int i = 0; i < ordered.Count; i++)
            {
                var id = ordered[i];
                if (!accounts.TryGetValue(id, out var name)) continue;
                bool isDef = id == defaultId;
                bool isDisabled = disabled.Contains(id);
                rows.Add(new AccountRow
                {
                    id = id,
                    name = name,
                    isDefault = isDef ? new AntdUI.CellTag("默认", AntdUI.TTypeMini.Primary) : null,
                    isEnabled = isDisabled
                        ? new AntdUI.CellBadge(AntdUI.TState.Default, "禁用")
                        : new AntdUI.CellBadge(AntdUI.TState.Processing, "启用"),
                    enabledSwitch = !isDisabled,
                    action = new AntdUI.CellLink[]
                    {
                        new AntdUI.CellButton("record",    "保存账号", AntdUI.TTypeMini.Info),
                        new AntdUI.CellButton("setDefault","设为默认", AntdUI.TTypeMini.Success),
                        new AntdUI.CellButton("rename",    "重命名",   AntdUI.TTypeMini.Default),
                        _pendingDelete.Contains(id)
                            ? new AntdUI.CellButton("delete", "确认删除", AntdUI.TTypeMini.Error)
                            : new AntdUI.CellButton("delete", "删除", AntdUI.TTypeMini.Primary).SetBack(System.Drawing.Color.Orange),
                    }
                });
            }
            table.DataSource = rows;
        }

        private void Table_SortRows(object sender, AntdUI.IntEventArgs e)
        {
            var sorted = table.SortList();
            var cfg = ConfigHelper.Load();
            var newOrder = sorted.OfType<AccountRow>().Select(r => r.id).ToList();
            if (IsEndfield) cfg.EndfieldAccountOrder = newOrder;
            else cfg.AccountOrder = newOrder;
            ConfigHelper.Save(cfg);
            _gamePage.LoadAccountSelect();
        }

        private void Table_CheckedChanged(object sender, AntdUI.TableCheckEventArgs e)
        {
            if (e.Record is not AccountRow row) return;
            var cfg = ConfigHelper.Load();
            if (IsEndfield)
            {
                if (e.Value) cfg.EndfieldDisabledAccounts.Remove(row.id);
                else cfg.EndfieldDisabledAccounts.Add(row.id);
            }
            else
            {
                if (e.Value) cfg.DisabledAccounts.Remove(row.id);
                else cfg.DisabledAccounts.Add(row.id);
            }
            ConfigHelper.Save(cfg);
            _gamePage.LoadAccountSelect();
            RefreshTable();
        }

        private void Table_CellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
        {
            if (e.Btn == null || e.Record is not AccountRow row) return;

            switch (e.Btn.Id)
            {
                case "record":
                    var form = FindForm() as AntdUI.BaseForm;
                    AntdUI.Message.loading(form, "保存中...", async config =>
                    {
                        try
                        {
                            if (IsEndfield) await GameLauncher.BackupEndfieldAccount(row.id);
                            else await GameLauncher.BackupAccount(row.id);
                            config.OK($"已保存账号「{row.name}」");
                        }
                        catch (Exception ex)
                        {
                            config.Error(ex.Message);
                        }
                    });
                    break;
                case "setDefault":
                    var cfg = ConfigHelper.Load();
                    if (IsEndfield) cfg.EndfieldDefaultAccount = row.id;
                    else cfg.DefaultAccount = row.id;
                    ConfigHelper.Save(cfg);
                    _gamePage.LoadAccountSelect();
                    RefreshTable();
                    break;
                case "rename":
                    ShowRenameDialog(row.id, row.name);
                    break;
                case "delete":
                    if (!_pendingDelete.Contains(row.id))
                    {
                        _pendingDelete.Add(row.id);
                        RefreshTable();
                        break;
                    }
                    _pendingDelete.Remove(row.id);
                    var cfg2 = ConfigHelper.Load();
                    if (IsEndfield)
                    {
                        cfg2.EndfieldAccounts.Remove(row.id);
                        cfg2.EndfieldAccountOrder.Remove(row.id);
                        if (cfg2.EndfieldDefaultAccount == row.id) cfg2.EndfieldDefaultAccount = "";
                    }
                    else
                    {
                        cfg2.Accounts.Remove(row.id);
                        cfg2.AccountOrder.Remove(row.id);
                        if (cfg2.DefaultAccount == row.id) cfg2.DefaultAccount = "";
                    }
                    ConfigHelper.Save(cfg2);
                    string backupDir = System.IO.Path.Combine(
                        IsEndfield ? ConfigHelper.EndfieldAccountBackupDir : ConfigHelper.AccountBackupDir,
                        row.id);
                    if (System.IO.Directory.Exists(backupDir))
                        System.IO.Directory.Delete(backupDir, true);
                    _gamePage.LoadAccountSelect();
                    RefreshTable();
                    break;
            }
        }

        private void ShowAddDialog()
        {
            var form = FindForm() as AntdUI.BaseForm;
            var input = new AntdUI.Input
            {
                PlaceholderText = "输入账号名称",
                Dock = DockStyle.Fill,
            };
            var panel = new System.Windows.Forms.Panel { Height = 40, Dock = DockStyle.Top };
            panel.Controls.Add(input);
            var wrap = new System.Windows.Forms.Panel { Size = new Size(260, 40) };
            wrap.Controls.Add(panel);

            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(form, "添加账号", wrap)
            {
                OkText = "确定",
                CancelText = "取消",
            });
            if (result != DialogResult.OK) return;

            string name = input.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var cfg = ConfigHelper.Load();
            string id = "A" + DateTime.Now.Ticks.ToString()[^6..];
            if (IsEndfield)
            {
                cfg.EndfieldAccounts[id] = name;
                cfg.EndfieldAccountOrder.Add(id);
                if (string.IsNullOrEmpty(cfg.EndfieldDefaultAccount)) cfg.EndfieldDefaultAccount = id;
            }
            else
            {
                cfg.Accounts[id] = name;
                cfg.AccountOrder.Add(id);
                if (string.IsNullOrEmpty(cfg.DefaultAccount)) cfg.DefaultAccount = id;
            }
            ConfigHelper.Save(cfg);
            _gamePage.LoadAccountSelect();
            RefreshTable();
        }

        private void ShowRenameDialog(string id, string currentName)
        {
            var form = FindForm() as AntdUI.BaseForm;
            var input = new AntdUI.Input
            {
                Text = currentName,
                PlaceholderText = "输入新名称",
                Dock = DockStyle.Fill,
            };
            var panel = new System.Windows.Forms.Panel { Height = 40, Dock = DockStyle.Top };
            panel.Controls.Add(input);
            var wrap = new System.Windows.Forms.Panel { Size = new Size(260, 40) };
            wrap.Controls.Add(panel);

            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(form, "重命名账号", wrap)
            {
                OkText = "确定",
                CancelText = "取消",
            });
            if (result != DialogResult.OK) return;

            string name = input.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var cfg = ConfigHelper.Load();
            if (IsEndfield) cfg.EndfieldAccounts[id] = name;
            else cfg.Accounts[id] = name;
            ConfigHelper.Save(cfg);
            _gamePage.LoadAccountSelect();
            RefreshTable();
        }

        private class AccountRow
        {
            public string id { get; set; }
            public string name { get; set; }
            public AntdUI.CellTag isDefault { get; set; }
            public AntdUI.CellBadge isEnabled { get; set; }
            public bool enabledSwitch { get; set; }
            public AntdUI.CellLink[] action { get; set; }
        }
    }
}
