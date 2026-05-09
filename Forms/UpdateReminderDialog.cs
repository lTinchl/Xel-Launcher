using System;
using System.Drawing;
using System.Windows.Forms;
using XelLauncher.Models;

namespace XelLauncher.Forms
{
    internal enum UpdateReminderAction
    {
        None,
        UpdateNow,
        SkipVersion,
        DisableReminder
    }

    internal sealed class UpdateReminderDialog : UserControl
    {
        public UpdateReminderAction SelectedAction { get; private set; }

        private static string L(string key, string fallback) =>
            AntdUI.Localization.Get(key, fallback);

        public UpdateReminderDialog(UpdateInfo info, string currentVersion)
        {
            Size = new Size(420, 230);
            MinimumSize = Size;
            BackColor = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkSurface : Color.White;

            var normalText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var subtleText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForegroundSecondary : Color.FromArgb(108, 116, 128);

            var title = new Label
            {
                Text = string.Format(L("App.Update.ReminderTitle", "发现新版本 v{0}"), info.LatestVersion),
                AutoSize = false,
                Location = new Point(18, 14),
                Size = new Size(384, 30),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = normalText,
                BackColor = Color.Transparent,
            };

            var version = new Label
            {
                Text = string.Format(L("App.Update.ReminderSubtitle", "当前版本 v{0}，可更新到 v{1}"), currentVersion, info.LatestVersion),
                AutoSize = false,
                Location = new Point(18, 48),
                Size = new Size(384, 24),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = subtleText,
                BackColor = Color.Transparent,
            };

            var notes = new TextBox
            {
                Text = BuildSummary(info.Changelog),
                Location = new Point(18, 82),
                Size = new Size(384, 80),
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Microsoft YaHei UI", 9F),
                BackColor = BackColor,
                ForeColor = normalText,
                TabStop = false,
            };

            var btnUpdate = CreateButton(L("App.Update.DownloadNow", "立即更新"), AntdUI.TTypeMini.Success, 18, UpdateReminderAction.UpdateNow);
            var btnSkip = CreateButton(L("App.Update.SkipVersion", "下个版本再提醒"), AntdUI.TTypeMini.Default, 154, UpdateReminderAction.SkipVersion);
            var btnDisable = CreateButton(L("App.Update.DisableReminder", "不再提醒更新"), AntdUI.TTypeMini.Default, 290, UpdateReminderAction.DisableReminder);

            Controls.Add(title);
            Controls.Add(version);
            Controls.Add(notes);
            Controls.Add(btnUpdate);
            Controls.Add(btnSkip);
            Controls.Add(btnDisable);
        }

        private AntdUI.Button CreateButton(string text, AntdUI.TTypeMini type, int x, UpdateReminderAction action)
        {
            var button = new AntdUI.Button
            {
                Text = text,
                Type = type,
                Radius = 6,
                Location = new Point(x, 178),
                Size = new Size(118, 34),
                TabStop = false,
                WaveSize = 0,
            };
            if (type == AntdUI.TTypeMini.Default)
            {
                button.Ghost = true;
                button.BorderWidth = 1F;
                button.DefaultBorderColor = AntdUI.Config.IsDark ? Color.FromArgb(70, 78, 92) : Color.FromArgb(226, 232, 240);
                button.BackHover = AntdUI.Config.IsDark ? Color.FromArgb(218, 226, 238) : Color.FromArgb(76, 86, 102);
                button.BackActive = AntdUI.Config.IsDark ? Color.FromArgb(238, 242, 248) : Color.FromArgb(48, 58, 72);
            }
            button.Click += (s, e) => Complete(action);
            return button;
        }

        private void Complete(UpdateReminderAction action)
        {
            SelectedAction = action;
            if (FindForm() is Form form)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        }

        private static string BuildSummary(string changelog)
        {
            if (string.IsNullOrWhiteSpace(changelog))
                return L("App.Update.NoChangelog", "暂无更新内容。");

            var lines = changelog.Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimStart('-', '*', '•').Trim();
                if (line.Length == 0) continue;
                if (result.Length > 0) result.AppendLine();
                result.Append("• ").Append(line);
                if (result.Length > 240) break;
            }

            return result.Length > 0 ? result.ToString() : L("App.Update.NoChangelog", "暂无更新内容。");
        }
    }
}
