using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public sealed class ServerPayloadUpdateForm : UserControl
    {
        private const int FormWidth = 720;
        private const int FormHeight = 520;
        private const int RowHeight = 52;

        private readonly Overview _overview;
        private readonly Dictionary<string, PayloadRow> _rows =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly AntdUI.Checkbox _selectAll;
        private readonly AntdUI.Label _selectedCount;
        private readonly AntdUI.Label _progressText;
        private readonly AntdUI.Progress _progress;
        private readonly AntdUI.Button _btnUpdate;
        private readonly AntdUI.Button _btnClose;

        private CancellationTokenSource _cancellation;
        private bool _busy;
        private bool _suppressSelectionEvents;

        public ServerPayloadUpdateForm(Overview overview, string currentIconName)
        {
            _overview = overview;

            var dark = AntdUI.Config.IsDark;
            var surface = dark ? AppTheme.DarkBackground : Color.White;
            var card = dark ? AppTheme.DarkSurface : Color.White;
            var border = dark ? AppTheme.DarkBorder : Color.FromArgb(225, 229, 235);
            var normalText = dark ? AppTheme.DarkForeground : Color.FromArgb(28, 32, 38);
            var subtleText = dark ? AppTheme.DarkForegroundSecondary : Color.FromArgb(105, 112, 122);

            Font = new Font("Microsoft YaHei UI", 9.5F);
            Size = new Size(FormWidth, FormHeight);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = surface;

            var description = new AntdUI.Label
            {
                Text = L("App.PayloadUpdate.Description",
                    "选择需要更新的服区，仅下载官方清单中发生变化的渠道文件。"),
                Location = new Point(22, 12),
                Size = new Size(FormWidth - 266, 28),
                ForeColor = subtleText,
                Font = new Font(Font.FontFamily, 9.5F),
                BackColor = Color.Transparent,
            };

            _selectAll = new AntdUI.Checkbox
            {
                Text = L("App.PayloadUpdate.SelectAll", "全部服区"),
                Location = new Point(24, 45),
                Size = new Size(130, 30),
                ForeColor = normalText,
                BackColor = Color.Transparent,
            };
            _selectAll.CheckedChanged += (s, e) =>
            {
                if (_suppressSelectionEvents) return;
                SetAllSelected(_selectAll.Checked);
            };

            _selectedCount = new AntdUI.Label
            {
                Location = new Point(FormWidth - 210, 45),
                Size = new Size(186, 30),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = subtleText,
                BackColor = Color.Transparent,
            };

            var btnOpenPayloadDirectory = new AntdUI.Button
            {
                Text = L("App.PayloadUpdate.OpenFolder", "打开切服资源目录"),
                IconSvg = "FolderOpenOutlined",
                Location = new Point(FormWidth - 210, 5),
                Size = new Size(186, 30),
                BorderWidth = 0,
                Radius = 7,
                WaveSize = 0,
                Type = AntdUI.TTypeMini.Default,
                Ghost = true,
            };
            btnOpenPayloadDirectory.Click += (s, e) => OpenPayloadDirectory();

            var header = new Panel
            {
                Location = new Point(22, 80),
                Size = new Size(FormWidth - 44, 30),
                BackColor = surface,
            };
            header.Controls.Add(CreateHeaderLabel(
                L("App.PayloadUpdate.Region", "服区"), 48, 250, subtleText));
            header.Controls.Add(CreateHeaderLabel(
                L("App.PayloadUpdate.ResourceVersion", "资源版本"), 310, 150, subtleText));
            header.Controls.Add(CreateHeaderLabel(
                L("App.PayloadUpdate.Status", "状态"), 475, 175, subtleText));

            var list = new Panel
            {
                Location = new Point(22, 110),
                Size = new Size(FormWidth - 44, RowHeight * ServerPayloadUpdater.Profiles.Count + 2),
                BackColor = surface,
            };

            var rowIndex = 0;
            _suppressSelectionEvents = true;
            try
            {
                foreach (var profile in ServerPayloadUpdater.Profiles)
                {
                    var state = ServerPayloadUpdater.GetState(profile.IconName);
                    var row = new PayloadRow(
                        profile,
                        GetProfileDisplayName(profile.IconName),
                        state?.Version,
                        card,
                        border,
                        normalText,
                        subtleText)
                    {
                        Location = new Point(0, rowIndex * RowHeight),
                        Size = new Size(list.Width, RowHeight - 4),
                    };
                    row.SelectionChanged += OnRowSelectionChanged;
                    _rows[profile.IconName] = row;
                    row.Selected = string.Equals(
                        profile.IconName, currentIconName, StringComparison.OrdinalIgnoreCase);
                    list.Controls.Add(row);
                    rowIndex++;
                }
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            _progressText = new AntdUI.Label
            {
                Text = L("App.PayloadUpdate.Ready", "请选择需要更新的服区。"),
                Location = new Point(24, 428),
                Size = new Size(FormWidth - 48, 24),
                ForeColor = subtleText,
                BackColor = Color.Transparent,
            };

            _progress = new AntdUI.Progress
            {
                Location = new Point(24, 454),
                Size = new Size(FormWidth - 48, 16),
                Value = 0F,
                Radius = 5,
                Visible = false,
            };

            _btnClose = new AntdUI.Button
            {
                Text = L("App.PayloadUpdate.Close", "关闭"),
                Location = new Point(FormWidth - 260, 480),
                Size = new Size(88, 34),
                Radius = 7,
                Type = AntdUI.TTypeMini.Default,
            };
            _btnClose.Click += (s, e) =>
            {
                if (_busy)
                {
                    _cancellation?.Cancel();
                    _btnClose.Enabled = false;
                    _progressText.Text = L("App.PayloadUpdate.Canceling", "正在取消...");
                }
                else
                {
                    FindForm()?.Close();
                }
            };

            _btnUpdate = new AntdUI.Button
            {
                Text = L("App.PayloadUpdate.UpdateSelected", "更新所选"),
                IconSvg = "CloudDownloadOutlined",
                Location = new Point(FormWidth - 164, 480),
                Size = new Size(142, 34),
                Radius = 7,
                Type = AntdUI.TTypeMini.Primary,
            };
            _btnUpdate.Click += async (s, e) => await UpdateSelectedAsync();

            Controls.Add(description);
            Controls.Add(_selectAll);
            Controls.Add(_selectedCount);
            Controls.Add(btnOpenPayloadDirectory);
            Controls.Add(header);
            Controls.Add(list);
            Controls.Add(_progressText);
            Controls.Add(_progress);
            Controls.Add(_btnClose);
            Controls.Add(_btnUpdate);

            UpdateSelectionSummary();
        }

        private void OpenPayloadDirectory()
        {
            try
            {
                Directory.CreateDirectory(ServerPayloadUpdater.PayloadRoot);
                Process.Start(new ProcessStartInfo
                {
                    FileName = ServerPayloadUpdater.PayloadRoot,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ServerPayloadDirectory.Open");
                AntdUI.Message.error(
                    FindForm() ?? _overview,
                    L("App.PayloadUpdate.OpenFolderFailed",
                        "无法打开切服资源目录。"));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _cancellation?.Cancel();

            base.Dispose(disposing);
        }

        private async Task UpdateSelectedAsync()
        {
            if (_busy) return;

            var selected = _rows.Values
                .Where(x => x.Selected)
                .Select(x => x.Profile)
                .ToArray();
            if (selected.Length == 0)
            {
                AntdUI.Message.warn(
                    FindForm() ?? _overview,
                    L("App.PayloadUpdate.NoSelection", "请至少选择一个服区。"));
                return;
            }

            SetBusy(true);
            _cancellation?.Dispose();
            var operation = new CancellationTokenSource();
            _cancellation = operation;

            var succeeded = 0;
            var failed = 0;
            var canceled = false;

            try
            {
                for (var profileIndex = 0; profileIndex < selected.Length; profileIndex++)
                {
                    var profile = selected[profileIndex];
                    var row = _rows[profile.IconName];
                    row.SetStatus(L("App.PayloadUpdate.Checking", "检查清单..."), StatusKind.Running);

                    var capturedIndex = profileIndex;
                    var progress = new Progress<ServerPayloadUpdateProgress>(value =>
                    {
                        ApplyProgress(value, row, capturedIndex, selected.Length);
                    });

                    try
                    {
                        var result = await ServerPayloadUpdater.UpdateAsync(
                            profile,
                            force: true,
                            progress,
                            operation.Token);

                        if (!IsDisposed && !Disposing)
                        {
                            row.SetVersion(result.Version);
                            row.SetStatus(
                                result.AlreadyCurrent
                                    ? L("App.PayloadUpdate.Latest", "已是最新")
                                    : L("App.PayloadUpdate.Success", "更新完成"),
                                StatusKind.Success);
                        }
                        succeeded++;
                    }
                    catch (OperationCanceledException)
                    {
                        if (!IsDisposed && !Disposing)
                        {
                            row.SetStatus(
                                L("App.PayloadUpdate.Canceled", "已取消"),
                                StatusKind.Warning);
                        }
                        canceled = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        if (!IsDisposed && !Disposing)
                        {
                            row.SetStatus(
                                L("App.PayloadUpdate.Failed", "更新失败"),
                                StatusKind.Error,
                                ex.Message);
                        }
                        LogHelper.LogError(
                            ex, $"ServerPayloadUpdate({profile.IconName})");
                    }

                    if (!IsDisposed && !Disposing)
                        _progress.Value = (profileIndex + 1F) / selected.Length;
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    SetBusy(false);
                if (ReferenceEquals(_cancellation, operation))
                    _cancellation = null;
                operation.Dispose();
            }

            if (IsDisposed || Disposing) return;

            if (canceled)
            {
                _progressText.Text = L("App.PayloadUpdate.Canceled", "已取消");
            }
            else
            {
                _progress.Value = 1F;
                _progress.Loading = false;
                _progressText.Text = string.Format(
                    L("App.PayloadUpdate.Summary", "更新完成：成功 {0} 个，失败 {1} 个。"),
                    succeeded,
                    failed);

                ShowCompletionNotification(succeeded, failed, _progressText.Text);
            }
        }

        private void ShowCompletionNotification(
            int succeeded,
            int failed,
            string summary)
        {
            var notificationHost = FindForm() ?? _overview;
            var allFailed = succeeded == 0 && failed > 0;
            var type = failed == 0
                ? AntdUI.TType.Success
                : allFailed
                    ? AntdUI.TType.Error
                    : AntdUI.TType.Warn;
            var title = failed == 0
                ? L("App.PayloadUpdate.Notification.SuccessTitle", "切服文件更新完成")
                : allFailed
                    ? L("App.PayloadUpdate.Notification.FailedTitle", "切服文件更新失败")
                    : L("App.PayloadUpdate.Notification.PartialTitle", "部分切服文件更新失败");

            AntdUI.Notification.open(new AntdUI.Notification.Config(
                notificationHost,
                title,
                summary,
                type,
                AntdUI.TAlignFrom.TR)
            {
                ShowInWindow = true,
                TopMost = false,
                AutoClose = failed == 0 ? 5 : 8,
                ClickClose = true,
                CloseIcon = true,
                EnableSound = false,
            });
        }

        private void ApplyProgress(
            ServerPayloadUpdateProgress value,
            PayloadRow row,
            int profileIndex,
            int profileCount)
        {
            if (IsDisposed || Disposing) return;

            var stageProgress = GetStageProgress(value);
            _progress.Loading = value.Stage is ServerPayloadUpdateStage.Checking
                or ServerPayloadUpdateStage.Comparing;
            _progress.Value = Math.Clamp(
                (profileIndex + stageProgress) / profileCount, 0F, 1F);

            switch (value.Stage)
            {
                case ServerPayloadUpdateStage.Checking:
                    row.SetStatus(
                        L("App.PayloadUpdate.Checking", "检查清单..."),
                        StatusKind.Running);
                    _progressText.Text = string.Format(
                        L("App.PayloadUpdate.CheckingRegion", "正在检查 {0} 的官方资源清单..."),
                        GetProfileDisplayName(value.Profile.IconName));
                    break;
                case ServerPayloadUpdateStage.Comparing:
                    row.SetStatus(
                        L("App.PayloadUpdate.Comparing", "比较文件..."),
                        StatusKind.Running);
                    _progressText.Text = FormatFileProgress(
                        L("App.PayloadUpdate.Comparing", "比较文件..."), value);
                    break;
                case ServerPayloadUpdateStage.Downloading:
                    row.SetStatus(
                        L("App.PayloadUpdate.Downloading", "下载中..."),
                        StatusKind.Running);
                    _progress.Loading = value.TotalBytes <= 0;
                    _progressText.Text = value.TotalBytes > 0
                        ? string.Format(
                            L("App.PayloadUpdate.DownloadProgress",
                                "正在下载 {0}：{1:F1} / {2:F1} MB"),
                            GetProfileDisplayName(value.Profile.IconName),
                            value.DownloadedBytes / 1048576D,
                            value.TotalBytes / 1048576D)
                        : FormatFileProgress(
                            L("App.PayloadUpdate.Downloading", "下载中..."), value);
                    break;
                case ServerPayloadUpdateStage.Verifying:
                    row.SetStatus(
                        L("App.PayloadUpdate.Verifying", "校验中..."),
                        StatusKind.Running);
                    _progressText.Text = FormatFileProgress(
                        L("App.PayloadUpdate.Verifying", "校验中..."), value);
                    break;
                case ServerPayloadUpdateStage.Applying:
                    row.SetStatus(
                        L("App.PayloadUpdate.Applying", "正在应用..."),
                        StatusKind.Running);
                    _progressText.Text = string.Format(
                        L("App.PayloadUpdate.ApplyingRegion", "正在应用 {0} 的更新..."),
                        GetProfileDisplayName(value.Profile.IconName));
                    break;
                case ServerPayloadUpdateStage.Completed:
                    row.SetVersion(value.Version);
                    break;
            }
        }

        private static float GetStageProgress(ServerPayloadUpdateProgress value)
        {
            return value.Stage switch
            {
                ServerPayloadUpdateStage.Checking => 0.04F,
                ServerPayloadUpdateStage.Comparing =>
                    0.04F + 0.16F * Ratio(value.FileIndex, value.FileCount),
                ServerPayloadUpdateStage.Downloading =>
                    0.20F + 0.58F * Ratio(value.DownloadedBytes, value.TotalBytes),
                ServerPayloadUpdateStage.Verifying =>
                    0.78F + 0.18F * Ratio(value.FileIndex, value.FileCount),
                ServerPayloadUpdateStage.Applying => 0.98F,
                ServerPayloadUpdateStage.Completed => 1F,
                _ => 0F
            };
        }

        private static float Ratio(long current, long total)
        {
            return total <= 0 ? 0F : Math.Clamp((float)current / total, 0F, 1F);
        }

        private static string FormatFileProgress(
            string stage,
            ServerPayloadUpdateProgress value)
        {
            if (value.FileCount <= 0) return stage;
            return $"{stage}  {value.FileIndex} / {value.FileCount}";
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _selectAll.Enabled = !busy;
            _btnUpdate.Enabled = !busy;
            _btnClose.Enabled = true;
            _btnClose.Text = busy
                ? L("App.PayloadUpdate.Cancel", "取消")
                : L("App.PayloadUpdate.Close", "关闭");

            foreach (var row in _rows.Values)
                row.SelectionEnabled = !busy;

            if (busy)
            {
                _progress.Visible = true;
                _progress.Value = 0F;
                _progress.Loading = true;
            }
            else
            {
                _progress.Loading = false;
            }
        }

        private void SetAllSelected(bool selected)
        {
            _suppressSelectionEvents = true;
            try
            {
                foreach (var row in _rows.Values)
                    row.Selected = selected;
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            UpdateSelectionSummary();
        }

        private void OnRowSelectionChanged()
        {
            if (_suppressSelectionEvents) return;
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            var selected = _rows.Values.Count(x => x.Selected);
            _selectedCount.Text = string.Format(
                L("App.PayloadUpdate.SelectedCount", "已选择 {0} / {1}"),
                selected,
                _rows.Count);

            _suppressSelectionEvents = true;
            try
            {
                _selectAll.Checked = selected == _rows.Count;
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            _btnUpdate.Enabled = !_busy && selected > 0;
        }

        private static AntdUI.Label CreateHeaderLabel(
            string text,
            int left,
            int width,
            Color color)
        {
            return new AntdUI.Label
            {
                Text = text,
                Location = new Point(left, 0),
                Size = new Size(width, 30),
                ForeColor = color,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                BackColor = Color.Transparent,
            };
        }

        private static string GetProfileDisplayName(string iconName)
        {
            return iconName switch
            {
                "Arknights" => L("App.PayloadUpdate.Region.Arknights", "明日方舟（官服）"),
                "BiliArknights" => L("App.PayloadUpdate.Region.BiliArknights", "明日方舟（B服）"),
                "Endfield" => L("App.PayloadUpdate.Region.Endfield", "终末地（官服）"),
                "BiliEndfield" => L("App.PayloadUpdate.Region.BiliEndfield", "终末地（B服）"),
                "GlobalEndfield" => L("App.PayloadUpdate.Region.GlobalEndfield", "终末地（国际服）"),
                "PlayEndfield" => L("App.PayloadUpdate.Region.PlayEndfield", "终末地（Google Play）"),
                _ => iconName
            };
        }

        private static string L(string key, string fallback)
        {
            return AntdUI.Localization.Get(key, fallback);
        }

        private enum StatusKind
        {
            Normal,
            Running,
            Success,
            Warning,
            Error
        }

        private sealed class PayloadRow : AntdUI.Panel
        {
            private readonly AntdUI.Checkbox _checkbox;
            private readonly AntdUI.Label _version;
            private readonly AntdUI.Label _status;
            private readonly Color _subtleText;
            private readonly AntdUI.TooltipComponent _tooltip = new();

            public PayloadRow(
                ServerPayloadProfile profile,
                string displayName,
                string version,
                Color background,
                Color border,
                Color normalText,
                Color subtleText)
            {
                Profile = profile;
                _subtleText = subtleText;

                BackColor = background;
                Back = background;
                BorderColor = border;
                BorderWidth = 1F;
                Radius = 8;

                _checkbox = new AntdUI.Checkbox
                {
                    Location = new Point(16, 9),
                    Size = new Size(30, 30),
                    AccessibleName = displayName,
                    BackColor = Color.Transparent,
                };
                _checkbox.CheckedChanged += (s, e) => SelectionChanged?.Invoke();

                var name = new AntdUI.Label
                {
                    Text = displayName,
                    Location = new Point(48, 7),
                    Size = new Size(250, 34),
                    ForeColor = normalText,
                    Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                    BackColor = Color.Transparent,
                };

                _version = new AntdUI.Label
                {
                    Text = string.IsNullOrWhiteSpace(version)
                        ? L("App.PayloadUpdate.NotUpdated", "未更新")
                        : version,
                    Location = new Point(310, 7),
                    Size = new Size(150, 34),
                    ForeColor = subtleText,
                    BackColor = Color.Transparent,
                };

                _status = new AntdUI.Label
                {
                    Text = string.IsNullOrWhiteSpace(version)
                        ? L("App.PayloadUpdate.Waiting", "等待更新")
                        : L("App.PayloadUpdate.Cached", "已有缓存"),
                    Location = new Point(475, 7),
                    Size = new Size(180, 34),
                    ForeColor = subtleText,
                    BackColor = Color.Transparent,
                };

                Controls.Add(_checkbox);
                Controls.Add(name);
                Controls.Add(_version);
                Controls.Add(_status);
            }

            public event Action SelectionChanged;

            public ServerPayloadProfile Profile { get; }

            [System.ComponentModel.DesignerSerializationVisibility(
                System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public bool Selected
            {
                get => _checkbox.Checked;
                set => _checkbox.Checked = value;
            }

            [System.ComponentModel.DesignerSerializationVisibility(
                System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public bool SelectionEnabled
            {
                set => _checkbox.Enabled = value;
            }

            public void SetVersion(string version)
            {
                if (!string.IsNullOrWhiteSpace(version))
                    _version.Text = version;
            }

            public void SetStatus(
                string text,
                StatusKind kind,
                string tooltip = null)
            {
                _status.Text = text;
                _status.ForeColor = kind switch
                {
                    StatusKind.Running => Color.FromArgb(22, 119, 255),
                    StatusKind.Success => Color.FromArgb(46, 160, 67),
                    StatusKind.Warning => Color.FromArgb(210, 140, 20),
                    StatusKind.Error => Color.FromArgb(220, 53, 69),
                    _ => _subtleText
                };

                _tooltip.SetTip(_status, tooltip);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _tooltip.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
