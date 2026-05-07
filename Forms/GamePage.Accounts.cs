using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;
namespace XelLauncher.Forms
{
    public partial class GamePage : UserControl
    {
        private bool IsAccountGame => _game?.IconName is "Arknights" or "Endfield" or "GlobalEndfield";

        private void UpdateAccountControlsVisibility()
        {
            var cfg = ConfigHelper.Load();
            var entry = cfg.Games.Find(g => g.IconName == _game.IconName);
            bool hideAccounts = !(entry?.AccountSwitchEnabled ?? false) || !IsAccountGame;
            btnAccountManage.Visible = !hideAccounts;
            accountSelect.Visible = !hideAccounts;

            int targetWidth = hideAccounts ? 224 : 448;
            int targetGS = hideAccounts ? 0 : 224;
            int targetFM = hideAccounts ? 176 : 400;
            panelLaunch.Width = targetWidth;
            GameStart.Location = new Point(targetGS, 0);
            floatMenu.Location = new Point(targetFM, 2);
            PositionLaunchPanel();
            PositionNoticePanel();
        }

        public void LoadAccountSelect()
        {
            var cfg = ConfigHelper.Load();
            accountSelect.Items.Clear();

            Dictionary<string, string> accounts;
            List<string> order;
            string defaultId;
            HashSet<string> disabled;

            if (_game?.IconName == "Endfield")
            {
                accounts = cfg.EndfieldAccounts;
                order = cfg.EndfieldAccountOrder;
                defaultId = cfg.EndfieldDefaultAccount;
                disabled = cfg.EndfieldDisabledAccounts;
            }
            else if (_game?.IconName == "GlobalEndfield")
            {
                accounts = cfg.GlobalEndfieldAccounts;
                order = cfg.GlobalEndfieldAccountOrder;
                defaultId = cfg.GlobalEndfieldDefaultAccount;
                disabled = cfg.GlobalEndfieldDisabledAccounts;
            }
            else
            {
                accounts = cfg.Accounts;
                order = cfg.AccountOrder;
                defaultId = cfg.DefaultAccount;
                disabled = cfg.DisabledAccounts;
            }

            var ordered = order.Where(id => accounts.ContainsKey(id)).ToList();
            foreach (var id in accounts.Keys)
                if (!ordered.Contains(id)) ordered.Add(id);
            foreach (var id in ordered)
                if (!disabled.Contains(id))
                    accountSelect.Items.Add(new AntdUI.SelectItem("  " + accounts[id], id));
            if (!string.IsNullOrEmpty(defaultId) && !disabled.Contains(defaultId))
                accountSelect.SelectedValue = defaultId;
            else if (accountSelect.Items.Count > 0)
                accountSelect.SelectedValue = ((AntdUI.SelectItem)accountSelect.Items[0]).Tag;
            else
                accountSelect.SelectedValue = null;
        }
    }
}
