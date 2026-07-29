using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private const int ServerPayloadAutoCheckIntervalHours = 4;

        private CancellationTokenSource _serverPayloadStartupCancellation;
        private System.Windows.Forms.Timer _serverPayloadAutoCheckTimer;
        private ServerPayloadUpdateNotification _serverPayloadUpdateNotification;
        private string _serverPayloadLastAttemptedCheckSlot = "";
        private bool _serverPayloadAutoCheckStopped;
        private bool _serverPayloadNotificationDismissed;
        private bool _serverPayloadStartupRunning;
        private bool _serverPayloadAnyDownloadStarted;
        private bool _serverPayloadCurrentProfileDownloadStarted;

        private async Task RunServerPayloadAutoUpdateOnLaunchAsync()
        {
            var slotBeforeRun = GetServerPayloadAutoCheckSlotKey(DateTime.Now);
            try
            {
                await RunServerPayloadAutoUpdateIfDueAsync();
                if (!_serverPayloadAutoCheckStopped &&
                    !IsDisposed &&
                    !Disposing &&
                    !string.Equals(
                        slotBeforeRun,
                        GetServerPayloadAutoCheckSlotKey(DateTime.Now),
                        StringComparison.Ordinal))
                {
                    await RunServerPayloadAutoUpdateIfDueAsync();
                }
            }
            finally
            {
                ScheduleNextServerPayloadAutoCheck();
            }
        }

        private async Task RunServerPayloadAutoUpdateIfDueAsync()
        {
            if (_serverPayloadStartupRunning || IsDisposed || Disposing)
                return;

            var config = ConfigHelper.Load();
            var enabledProfiles = config.ServerPayloadAutoUpdateProfiles;
            var profiles = ServerPayloadUpdater.Profiles
                .Where(profile => enabledProfiles.Contains(
                    profile.IconName,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (profiles.Count == 0)
                return;

            var checkSlot = GetServerPayloadAutoCheckSlotKey(DateTime.Now);
            if (string.Equals(
                    _serverPayloadLastAttemptedCheckSlot,
                    checkSlot,
                    StringComparison.Ordinal) ||
                string.Equals(
                    config.ServerPayloadLastAutoCheckSlotLocal,
                    checkSlot,
                    StringComparison.Ordinal))
            {
                return;
            }

            _serverPayloadLastAttemptedCheckSlot = checkSlot;
            try
            {
                config.ServerPayloadLastAutoCheckSlotLocal = checkSlot;
                ConfigHelper.Save(config);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(
                    ex,
                    $"Server payload check slot save failed: {checkSlot}");
            }

            _serverPayloadStartupRunning = true;
            _serverPayloadNotificationDismissed = false;
            _serverPayloadAnyDownloadStarted = false;
            _serverPayloadCurrentProfileDownloadStarted = false;
            _serverPayloadStartupCancellation?.Dispose();
            var operation = new CancellationTokenSource();
            _serverPayloadStartupCancellation = operation;

            var updated = 0;
            var current = 0;
            var succeeded = 0;
            var failed = 0;

            try
            {
                ShowServerPayloadProgress(
                    PayloadText(
                        "App.PayloadUpdate.Notification.CheckingTitle",
                        "正在更新切服资源"),
                    PayloadText(
                        "App.PayloadUpdate.CheckingVersion",
                        "检查清单..."),
                    0F,
                    loading: true);

                for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    _serverPayloadCurrentProfileDownloadStarted = false;

                    var profile = profiles[profileIndex];
                    var capturedIndex = profileIndex;
                    var profileName = GetServerPayloadProfileDisplayName(profile.IconName);
                    ShowServerPayloadProgress(
                        PayloadText(
                            "App.PayloadUpdate.Notification.CheckingTitle",
                            "正在检查切服资源"),
                        string.Format(
                            PayloadText(
                                "App.PayloadUpdate.CheckingVersionRegion",
                                "正在检查 {0} 的最新资源版本..."),
                            profileName),
                        (float)profileIndex / profiles.Count,
                        loading: true);

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
                        var result = await ServerPayloadUpdater.UpdateIfOutdatedAsync(
                            profile,
                            progress,
                            operation.Token);

                        if (result.AlreadyCurrent)
                        {
                            current++;
                            ShowServerPayloadProgress(
                                PayloadText(
                                    "App.PayloadUpdate.Notification.CheckingTitle",
                                    "正在检查切服资源"),
                                string.Format(
                                    PayloadText(
                                        "App.PayloadUpdate.VersionCurrentRegion",
                                        "{0} 已是最新版本"),
                                    profileName),
                                (float)(profileIndex + 1) / profiles.Count,
                                loading: false);
                        }
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
                            $"Server payload scheduled check/update failed: {profile.IconName}");
                    }
                }

                if (failed == 0)
                {
                    if (updated == 0)
                    {
                        ShowServerPayloadResult(
                            PayloadText(
                                "App.PayloadUpdate.Notification.LatestTitle",
                                "切服资源已是最新"),
                            string.Format(
                                PayloadText(
                                    "App.PayloadUpdate.Notification.StartupLatest",
                                    "版本检查完成：{0} 个服区均为最新版本。"),
                                current),
                            ServerPayloadNotificationState.Success,
                            autoCloseSeconds: 5);
                    }
                    else
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
                LogHelper.LogError(ex, "Server payload scheduled check/update");
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

        private async void ServerPayloadAutoCheckTimer_Tick(
            object sender,
            EventArgs e)
        {
            _serverPayloadAutoCheckTimer?.Stop();
            try
            {
                await RunServerPayloadAutoUpdateOnLaunchAsync();
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Server payload scheduled timer");
            }
        }

        private void ScheduleNextServerPayloadAutoCheck()
        {
            if (_serverPayloadAutoCheckStopped || IsDisposed || Disposing)
                return;

            var now = DateTime.Now;
            var nextCheck = GetServerPayloadAutoCheckSlot(now)
                .AddHours(ServerPayloadAutoCheckIntervalHours);
            var delayMilliseconds = Math.Clamp(
                Math.Ceiling((nextCheck - now).TotalMilliseconds),
                1000D,
                int.MaxValue);

            if (_serverPayloadAutoCheckTimer == null)
            {
                _serverPayloadAutoCheckTimer = new System.Windows.Forms.Timer();
                _serverPayloadAutoCheckTimer.Tick +=
                    ServerPayloadAutoCheckTimer_Tick;
            }

            _serverPayloadAutoCheckTimer.Stop();
            _serverPayloadAutoCheckTimer.Interval = (int)delayMilliseconds;
            _serverPayloadAutoCheckTimer.Start();
        }

        private static DateTime GetServerPayloadAutoCheckSlot(
            DateTime localTime)
        {
            var slotHour = localTime.Hour -
                           localTime.Hour % ServerPayloadAutoCheckIntervalHours;
            return localTime.Date.AddHours(slotHour);
        }

        private static string GetServerPayloadAutoCheckSlotKey(
            DateTime localTime)
        {
            return GetServerPayloadAutoCheckSlot(localTime).ToString(
                "yyyy-MM-dd'T'HH:mm",
                CultureInfo.InvariantCulture);
        }

        private void ApplyServerPayloadProgress(
            ServerPayloadUpdateProgress value,
            int profileIndex,
            int profileCount)
        {
            if (value.Stage == ServerPayloadUpdateStage.Downloading)
            {
                _serverPayloadAnyDownloadStarted = true;
                _serverPayloadCurrentProfileDownloadStarted = true;
            }

            if (!_serverPayloadCurrentProfileDownloadStarted)
                return;

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
            if (!_serverPayloadStartupRunning ||
                !_serverPayloadCurrentProfileDownloadStarted)
            {
                return;
            }

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
            if (state == ServerPayloadNotificationState.Success &&
                !_serverPayloadAnyDownloadStarted)
            {
                return;
            }

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
                RehostServerPayloadNotification();
                return _serverPayloadUpdateNotification;
            }

            var notification = new ServerPayloadUpdateNotification
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            notification.DismissRequested += (_, _) =>
            {
                _serverPayloadNotificationDismissed = true;
                HideServerPayloadNotification();
            };
            notification.AutoCloseElapsed += (_, _) =>
                HideServerPayloadNotification();

            _serverPayloadUpdateNotification = notification;
            RehostServerPayloadNotification();
            return notification;
        }

        private Control GetServerPayloadNotificationHost()
        {
            var host = _currentGamePage?.GetServerPayloadNotificationHost();
            return host != null && !host.IsDisposed ? host : this;
        }

        private void RehostServerPayloadNotification()
        {
            var notification = _serverPayloadUpdateNotification;
            if (notification == null || notification.IsDisposed) return;

            var host = GetServerPayloadNotificationHost();
            if (!ReferenceEquals(notification.Parent, host))
            {
                notification.Parent?.Controls.Remove(notification);
                host.Controls.Add(notification);
            }

            PositionServerPayloadNotification();
            notification.BringToFront();
        }

        private void PositionServerPayloadNotification()
        {
            var notification = _serverPayloadUpdateNotification;
            if (notification == null || notification.IsDisposed) return;

            var host = notification.Parent ?? this;
            var margin = ScaleForDpi(16);
            var top = ReferenceEquals(host, this)
                ? windowBar.Bottom + ScaleForDpi(12)
                : ScaleForDpi(12);
            notification.Location = new System.Drawing.Point(
                Math.Max(margin, host.ClientSize.Width - notification.Width - margin),
                top);
        }

        private void HideServerPayloadNotification()
        {
            var notification = _serverPayloadUpdateNotification;
            _serverPayloadUpdateNotification = null;
            if (notification == null || notification.IsDisposed) return;

            notification.Parent?.Controls.Remove(notification);
            notification.Dispose();
        }

        private void StopServerPayloadAutoUpdate()
        {
            _serverPayloadAutoCheckStopped = true;
            if (_serverPayloadAutoCheckTimer != null)
            {
                _serverPayloadAutoCheckTimer.Stop();
                _serverPayloadAutoCheckTimer.Tick -=
                    ServerPayloadAutoCheckTimer_Tick;
                _serverPayloadAutoCheckTimer.Dispose();
                _serverPayloadAutoCheckTimer = null;
            }

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
