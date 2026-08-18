using System;
using System.Collections.Generic;
using System.IO;

namespace XelLauncher.Helpers
{
    public static class GameRepairManager
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, IDisposable> RepairingPaths =
            new(StringComparer.OrdinalIgnoreCase);

        public static bool TryStart(string iconName, string installPath)
        {
            LinkedClientPolicy.ThrowIfSharedClient(iconName, installPath);
            if (!LinkedClientOperationCoordinator.TryAcquire(
                    iconName, installPath, out var operationLease))
            {
                return false;
            }

            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                if (RepairingPaths.ContainsKey(key))
                {
                    operationLease.Dispose();
                    return false;
                }

                RepairingPaths[key] = operationLease;
                return true;
            }
        }

        public static bool IsRepairing(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                return false;

            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                return RepairingPaths.ContainsKey(key);
            }
        }

        public static void Complete(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                return;

            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                if (RepairingPaths.Remove(key, out var operationLease))
                    operationLease?.Dispose();
            }
        }

        private static string GetKey(string installPath)
        {
            var fullPath = Path.GetFullPath(installPath ?? "");
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
