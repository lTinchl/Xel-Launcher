using System;
using System.IO;
using System.Linq;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class LinkedClientPolicy
    {
        public static bool IsArknightsChannel(string iconName) =>
            GameChannelCatalog.Get(iconName)?.SupportsLegacyLinkedClient == true;

        public static bool IsSharedClient(string iconName, string installPath)
        {
            if (!IsArknightsChannel(iconName)) return false;
            var config = ConfigHelper.Load();
            var entry = FindEntry(config, iconName, installPath);
            return !string.IsNullOrWhiteSpace(entry?.LinkedClientGroupId) ||
                   ArknightsLinkedClientService.IsPendingClient(
                       config, iconName, installPath) ||
                   ArknightsLinkedClientService.IsPendingDetachClient(
                       config, iconName, installPath) ||
                   ArknightsLinkedClientService.HasLinkedClientMarker(installPath);
        }

        public static bool ShouldSkipServerPayloadSwitch(
            AppConfig config,
            GameEntry entry)
        {
            if (entry != null && IsArknightsChannel(entry.IconName) &&
                (ArknightsLinkedClientService.IsPendingClient(
                     config, entry.IconName, entry.RootPath) ||
                 ArknightsLinkedClientService.IsPendingDetachClient(
                     config, entry.IconName, entry.RootPath) ||
                 ArknightsLinkedClientService.HasLinkedClientMarker(
                     entry.RootPath)))
            {
                return true;
            }

            if (entry != null && IsArknightsChannel(entry.IconName) &&
                !string.IsNullOrWhiteSpace(entry.LinkedClientGroupId))
            {
                return true;
            }

            if (entry?.IndependentChannelClient != true ||
                !IsArknightsChannel(entry.IconName))
            {
                return false;
            }

            var counterpartIcon = string.Equals(
                entry.IconName, "Arknights", StringComparison.OrdinalIgnoreCase)
                ? "BiliArknights"
                : "Arknights";
            var counterpart = config.Games.FirstOrDefault(g =>
                string.Equals(g.IconName, counterpartIcon,
                    StringComparison.OrdinalIgnoreCase));

            return counterpart == null ||
                   !AreSamePath(entry.RootPath, counterpart.RootPath);
        }

        public static bool ShouldSkipServerPayloadSwitch(
            string iconName,
            string installPath)
        {
            var config = ConfigHelper.Load();
            var entry = FindEntry(config, iconName, installPath);
            return ShouldSkipServerPayloadSwitch(config, entry);
        }

        public static void ThrowIfSharedClient(string iconName, string installPath)
        {
            if (IsArknightsChannel(iconName) &&
                ArknightsLinkedClientService.IsOperationActive)
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.GroupBusy",
                    "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
            }

            if (!IsSharedClient(iconName, installPath)) return;
            throw new InvalidOperationException(AntdUI.Localization.Get(
                "App.LinkedClient.Error.MutationBlocked",
                "该客户端仍与另一渠道共享硬链接文件。请先在B服设置中解除共享，再更新、预下载或修复。"));
        }

        public static void UpdatePath(
            AppConfig config,
            GameEntry entry,
            string newPath)
        {
            if (entry == null) return;
            if (AreSamePath(entry.RootPath, newPath))
            {
                entry.RootPath = newPath ?? "";
                return;
            }

            if (!string.IsNullOrWhiteSpace(entry.LinkedClientGroupId))
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.MutationBlocked",
                    "该客户端仍与另一渠道共享硬链接文件。请先在B服设置中解除共享，再更改路径。"));
            }

            if (IsArknightsChannel(entry.IconName) &&
                ArknightsLinkedClientService.IsOperationActive)
            {
                throw GroupBusy();
            }

            if (IsArknightsChannel(entry.IconName))
            {
                if (!LinkedClientOperationCoordinator.TryAcquirePaths(
                        new[] { entry.RootPath, newPath }, out var pathLease))
                {
                    throw GroupBusy();
                }
                pathLease.Dispose();
            }

            if (ArknightsLinkedClientService.IsPendingClient(
                    config, entry.IconName, entry.RootPath) ||
                ArknightsLinkedClientService.IsPendingDetachClient(
                    config, entry.IconName, entry.RootPath) ||
                ArknightsLinkedClientService.HasLinkedClientMarker(
                    entry.RootPath) ||
                ArknightsLinkedClientService.IsPendingClient(
                    config, entry.IconName, newPath) ||
                ArknightsLinkedClientService.IsPendingDetachClient(
                    config, entry.IconName, newPath) ||
                ArknightsLinkedClientService.HasLinkedClientMarker(newPath))
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.MutationBlocked",
                    "该客户端仍与另一渠道共享硬链接文件。请先在B服设置中解除共享，再更改路径。"));
            }

            entry.RootPath = newPath ?? "";
            entry.IndependentChannelClient = false;
            entry.LinkedClientGroupId = "";
        }

        public static void ClearLegacyPairState(AppConfig config)
        {
            if (ArknightsLinkedClientService.IsOperationActive)
                throw GroupBusy();

            if (config.PendingLinkedClient != null ||
                config.PendingLinkedClientDetach != null ||
                config.Games.Any(g =>
                    IsArknightsChannel(g.IconName) &&
                    !string.IsNullOrWhiteSpace(g.LinkedClientGroupId)) ||
                config.Games.Any(g =>
                    IsArknightsChannel(g.IconName) &&
                    ArknightsLinkedClientService.HasLinkedClientMarker(g.RootPath)))
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.MutationBlocked",
                    "该客户端仍与另一渠道共享硬链接文件。请先在B服设置中解除共享，再切换为旧模式。"));
            }

            foreach (var entry in config.Games.Where(g => IsArknightsChannel(g.IconName)))
            {
                entry.IndependentChannelClient = false;
                entry.LinkedClientGroupId = "";
            }
        }

        public static void CompleteDetach(AppConfig config, string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId)) return;
            if (string.Equals(
                    config.PendingLinkedClientDetach?.GroupId,
                    groupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.GroupBusy",
                    "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
            }
            if (config.Games.Any(g =>
                    string.Equals(g.LinkedClientGroupId, groupId,
                        StringComparison.OrdinalIgnoreCase) &&
                    ArknightsLinkedClientService.HasLinkedClientMarker(
                        g.RootPath)))
            {
                throw new InvalidOperationException(AntdUI.Localization.Get(
                    "App.LinkedClient.Error.MutationBlocked",
                    "仍检测到硬链接客户端标记，不能清除共享保护。"));
            }

            foreach (var entry in config.Games.Where(g =>
                         string.Equals(g.LinkedClientGroupId, groupId,
                             StringComparison.OrdinalIgnoreCase)))
            {
                entry.LinkedClientGroupId = "";
            }
        }

        public static GameEntry FindEntry(
            AppConfig config,
            string iconName,
            string installPath = null)
        {
            var candidates = config.Games.Where(g =>
                string.Equals(g.IconName, iconName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(installPath))
            {
                return candidates.FirstOrDefault(g =>
                    AreSamePath(g.RootPath, installPath));
            }

            return candidates.FirstOrDefault();
        }

        public static bool AreSamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(
                    NormalizePath(left), NormalizePath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim(), right.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizePath(string path) =>
            Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static InvalidOperationException GroupBusy() =>
            new(AntdUI.Localization.Get(
                "App.LinkedClient.Error.GroupBusy",
                "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
    }
}
