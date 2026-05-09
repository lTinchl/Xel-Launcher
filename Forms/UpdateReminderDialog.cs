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

        public UpdateReminderDialog(UpdateInfo info, string currentVersion)
        {
            Size = new Size(420, 230);
            MinimumSize = Size;
            BackColor = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkSurface : Color.White;

            var normalText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForeground : Color.FromArgb(24, 28, 34);
            var subtleText = AntdUI.Config.IsDark ? Helpers.AppTheme.DarkForegroundSecondary : Color.FromArgb(108, 116, 128);

            var title = new Label
            {
                Text = $"\u53d1\u73b0\u65b0\u7248\u672c v{info.LatestVersion}",
                AutoSize = false,
                Location = new Point(18, 14),
                Size = new Size(384, 30),
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = normalText,
                BackColor = Color.Transparent,
            };

            var version = new Label
            {
                Text = $"\u5f53\u524d\u7248\u672c v{currentVersion}\uff0c\u53ef\u66f4\u65b0\u5230 v{info.LatestVersion}",
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

            var btnUpdate = CreateButton("\u7acb\u5373\u66f4\u65b0", AntdUI.TTypeMini.Primary, 18, UpdateReminderAction.UpdateNow);
            var btnSkip = CreateButton("\u4e0b\u4e2a\u7248\u672c\u518d\u63d0\u9192", AntdUI.TTypeMini.Default, 154, UpdateReminderAction.SkipVersion);
            var btnDisable = CreateButton("\u4e0d\u518d\u63d0\u9192\u66f4\u65b0", AntdUI.TTypeMini.Default, 290, UpdateReminderAction.DisableReminder);
            btnDisable.Ghost = true;

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
            };
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
                return "\u6682\u65e0\u66f4\u65b0\u5185\u5bb9\u3002";

            var lines = changelog.Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimStart('-', '*', '\u2022').Trim();
                if (line.Length == 0) continue;
                if (result.Length > 0) result.AppendLine();
                result.Append("\u2022 ").Append(line);
                if (result.Length > 240) break;
            }

            return result.Length > 0 ? result.ToString() : "\u6682\u65e0\u66f4\u65b0\u5185\u5bb9\u3002";
        }
    }
}
