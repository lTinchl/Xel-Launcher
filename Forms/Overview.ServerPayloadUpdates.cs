using System;
using System.Threading;
using System.Threading.Tasks;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private CancellationTokenSource _serverPayloadStartupCancellation;
        private ServerPayloadUpdateNotification _serverPayloadUpdateNotification;
        private bool _serverPayloadNotificationDismissed;
        private bool _serverPayloadStartupRunning;

        private async Task RunServerPayloadAutoUpdateOnLaunchAsync()
        {
            if (_serverPayloadStartupRunning || IsDisposed || Disposing)
                return;

            _serverPayloadStartupRunning = true;
            _serverPayloadNotificationDismissed = false;
            _serverPayloadStartupCancellation?.Dispose();
            var operation = new CancellationTokenSource();
            _serverPayloadStartupCancellation = operation;

            var profiles = ServerPayloadUpdater.Profiles;
            var updated = 0;
            var current = 0;
            var succeeded = 0;
            var failed = 0;

            try
            {
                ShowServerPayloadProgress(
                    PayloadText(
                        "App.PayloadUpdate.Notification.UpdatingTitle",
                        "正在更新切服资源"),
                    PayloadText(
                        "App.PayloadUpdate.Checking",
                        "检查清单..."),
                    0F,
                    loading: true);

                for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    operation.Token.ThrowIfCancellationRequested();

                    var profile = profiles[profileIndex];
                    var capturedIndex = profileIndex;
                    var progress = new Progress<ServerPayloadUpdateProgress>(value =>
                    {
                        if (operation.IsCancellationRequested || IsDisposed || Disposing)
                            return;

                        ApplyServerPayloadProgress(
                            value,
                            capturedIndex,
                            profiles.Count);
                    });

                    try
                    {
                        var result = await ServerPayloadUpdater.UpdateAsync(
                            profile,
                            force: false,
                            progress,
                            operation.Token);

                        if (result.AlreadyCurrent)
                            current++;
                        else
                            updated++;
                        succeeded++;
                    }
                    catch (OperationCanceledException) when (operation.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        LogHelper.LogError(
                            ex,
                            $"Server payload startup update failed: {profile.IconName}");
                    }
                }

                if (failed == 0)
                {
                    ShowServerPayloadResult(
                        PayloadText(
                            "App.PayloadUpdate.Notification.SuccessTitle",
                            "切服文件更新完成"),
                        string.Format(
                            PayloadText(
                                "App.PayloadUpdate.Notification.StartupSuccess",
                                "切服资源检查完成：更新 {0} 个，已是最新 {1} 个。"),
                            updated,
                            current),
                        ServerPayloadNotificationState.Success,
                        autoCloseSeconds: 5);
                }
                else
                {
                    var allFailed = succeeded == 0;
                    ShowServerPayloadResult(
                        PayloadText(
                            allFailed
                                ? "App.PayloadUpdate.Notification.FailedTitle"
                                : "App.PayloadUpdate.Notification.PartialTitle",
                            allFailed
                                ? "切服文件更新失败"
                                : "部分切服文件更新失败"),
                        string.Format(
                            PayloadText(
                                "App.PayloadUpdate.Notification.StartupFailure",
                                "切服资源更新完成：成功 {0} 个，失败 {1} 个。"),
                            succeeded,
                            failed),
                        allFailed
                            ? ServerPayloadNotificationState.Error
                            : ServerPayloadNotificationState.Warning,
                        autoCloseSeconds: 8);
                }
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                HideServerPayloadNotification();
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Server payload startup update");
                ShowServerPayloadResult(
                    PayloadText(
                        "App.PayloadUpdate.Notification.FailedTitle",
                        "切服文件更新失败"),
                    ex.Message,
                    ServerPayloadNotificationState.Error,
                    autoCloseSeconds: 8);
            }
            finally
            {
                _serverPayloadStartupRunning = false;
                if (ReferenceEquals(_serverPayloadStartupCancellation, operation))
                    _serverPayloadStartupCancellation = null;
                operation.Dispose();
            }
        }

        private void ApplyServerPayloadProgress(
            ServerPayloadUpdateProgress value,
            int profileIndex,
            int profileCount)
        {
            var profileName = GetServerPayloadProfileDisplayName(value.Profile.IconName);
            var stageProgress = GetServerPayloadStageProgress(value);
            var overallProgress = Math.Clamp(
                (profileIndex + stageProgress) / profileCount,
                0F,
                1F);
            var loading = false;
            string detail;

            switch (value.Stage)
            {
                case ServerPayloadUpdateStage.Checking:
                    loading = true;
                    detail = string.Format(
                        PayloadText(
                            "App.PayloadUpdate.CheckingRegion",
                            "正在检查 {0} 的官方资源清单..."),
                        profileName);
                    break;
                case ServerPayloadUpdateStage.Comparing:
                    detail = FormatServerPayloadFileProgress(
                        profileName,
                        PayloadText(
                            "App.PayloadUpdate.Comparing",
                            "比较文件..."),
                        value);
                    break;
                case ServerPayloadUpdateStage.Downloading:
                    loading = value.TotalBytes <= 0;
                    detail = value.TotalBytes > 0
                        ? string.Format(
                            PayloadText(
                                "App.PayloadUpdate.DownloadProgress",
                                "正在下载 {0}：{1:F1} / {2:F1} MB"),
                            profileName,
                            value.DownloadedBytes / 1048576D,
                            value.TotalBytes / 1048576D)
                        : FormatServerPayloadFileProgress(
                            profileName,
                            PayloadText(
                                "App.PayloadUpdate.Downloading",
                                "下载中..."),
                            value);
                    break;
                case ServerPayloadUpdateStage.Verifying:
                    detail = FormatServerPayloadFileProgress(
                        profileName,
                        PayloadText(
                            "App.PayloadUpdate.Verifying",
                            "校验中..."),
                        value);
                    break;
                case ServerPayloadUpdateStage.Applying:
                    detail = string.Format(
                        PayloadText(
                            "App.PayloadUpdate.ApplyingRegion",
                            "正在应用 {0} 的更新..."),
                        profileName);
                    break;
                default:
                    detail = profileName;
                    break;
            }

            ShowServerPayloadProgress(
                PayloadText(
                    "App.PayloadUpdate.Notification.UpdatingTitle",
                    "正在更新切服资源"),
                detail,
                overallProgress,
                loading);
        }

        private void ShowServerPayloadProgress(
            string title,
            string detail,
            float progress,
            bool loading)
        {
            if (!_serverPayloadStartupRunning) return;

            var notification = EnsureServerPayloadNotification();
            if (notification == null) return;

            notification.ShowProgress(title, detail, progress, loading);
            notification.BringToFront();
        }

        private void ShowServerPayloadResult(
            string title,
            string detail,
            ServerPayloadNotificationState state,
            int autoCloseSeconds)
        {
            if (_serverPayloadNotificationDismissed) return;

            var notification = EnsureServerPayloadNotification();
            if (notification == null) return;

            notification.ShowResult(title, detail, state, autoCloseSeconds);
            notification.BringToFront();
        }

        private ServerPayloadUpdateNotification EnsureServerPayloadNotification()
        {
            if (_serverPayloadNotificationDismissed || IsDisposed || Disposing)
                return null;

            if (_serverPayloadUpdateNotification != null &&
                !_serverPayloadUpdateNotification.IsDisposed)
            {
                return _serverPayloadUpdateNotification;
            }

            var notification = new ServerPayloadUpdateNotification
            {
                Anchor = System.Windows.Forms.AnchorStyles.Top |
                         System.Windows.Forms.AnchorStyles.Right,
            };
            notification.DismissRequested += (_, _) =>
            {
                _serverPayloadNotificationDismissed = true;
                HideServerPayloadNotification();
            };
            notification.AutoCloseElapsed += (_, _) =>
                HideServerPayloadNotification();

            _serverPayloadUpdateNotification = notification;
            Controls.Add(notification);
            PositionServerPayloadNotification();
            notification.BringToFront();
            return notification;
        }

        private void PositionServerPayloadNotification()
        {
            var notification = _serverPayloadUpdateNotification;
            if (notification == null || notification.IsDisposed) return;

            var margin = ScaleForDpi(16);
            notification.Location = new System.Drawing.Point(
                Math.Max(margin, ClientSize.Width - notification.Width - margin),
                windowBar.Bottom + ScaleForDpi(12));
        }

        private void HideServerPayloadNotification()
        {
            var notification = _serverPayloadUpdateNotification;
            _serverPayloadUpdateNotification = null;
            if (notification == null || notification.IsDisposed) return;

            Controls.Remove(notification);
            notification.Dispose();
        }

        private void StopServerPayloadAutoUpdate()
        {
            try
            {
                _serverPayloadStartupCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            HideServerPayloadNotification();
        }

        private static float GetServerPayloadStageProgress(
            ServerPayloadUpdateProgress value)
        {
            return value.Stage switch
            {
                ServerPayloadUpdateStage.Checking => 0.04F,
                ServerPayloadUpdateStage.Comparing =>
                    0.04F + 0.16F * ServerPayloadRatio(value.FileIndex, value.FileCount),
                ServerPayloadUpdateStage.Downloading =>
                    0.20F + 0.58F * ServerPayloadRatio(
                        value.DownloadedBytes,
                        value.TotalBytes),
                ServerPayloadUpdateStage.Verifying =>
                    0.78F + 0.18F * ServerPayloadRatio(value.FileIndex, value.FileCount),
                ServerPayloadUpdateStage.Applying => 0.98F,
                ServerPayloadUpdateStage.Completed => 1F,
                _ => 0F,
            };
        }

        private static float ServerPayloadRatio(long current, long total) =>
            total <= 0
                ? 0F
                : Math.Clamp((float)current / total, 0F, 1F);

        private static string FormatServerPayloadFileProgress(
            string profileName,
            string stage,
            ServerPayloadUpdateProgress value)
        {
            return value.FileCount <= 0
                ? $"{profileName}：{stage}"
                : $"{profileName}：{stage}  {value.FileIndex} / {value.FileCount}";
        }

        private static string GetServerPayloadProfileDisplayName(string iconName)
        {
            return iconName switch
            {
                "Arknights" => PayloadText(
                    "App.PayloadUpdate.Region.Arknights",
                    "明日方舟（官服）"),
                "BiliArknights" => PayloadText(
                    "App.PayloadUpdate.Region.BiliArknights",
                    "明日方舟（B服）"),
                "Endfield" => PayloadText(
                    "App.PayloadUpdate.Region.Endfield",
                    "终末地（官服）"),
                "BiliEndfield" => PayloadText(
                    "App.PayloadUpdate.Region.BiliEndfield",
                    "终末地（B服）"),
                "GlobalEndfield" => PayloadText(
                    "App.PayloadUpdate.Region.GlobalEndfield",
                    "终末地（国际服）"),
                "PlayEndfield" => PayloadText(
                    "App.PayloadUpdate.Region.PlayEndfield",
                    "终末地（Google Play）"),
                _ => iconName,
            };
        }

        private static string PayloadText(string key, string fallback) =>
            AntdUI.Localization.Get(key, fallback);
    }
}
