using System;
using System.Collections.Generic;
using System.IO;

namespace XelLauncher.Helpers
{
    public static class GameRepairManager
    {
        private static readonly object SyncRoot = new();
        private static readonly HashSet<string> RepairingPaths = new(StringComparer.OrdinalIgnoreCase);

        public static bool TryStart(string installPath)
        {
            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                if (RepairingPaths.Contains(key))
                    return false;

                RepairingPaths.Add(key);
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
                return RepairingPaths.Contains(key);
            }
        }

        public static void Complete(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                return;

            var key = GetKey(installPath);
            lock (SyncRoot)
            {
                RepairingPaths.Remove(key);
            }
        }

        private static string GetKey(string installPath)
        {
            var fullPath = Path.GetFullPath(installPath ?? "");
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
