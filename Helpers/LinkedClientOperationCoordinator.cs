using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace XelLauncher.Helpers
{
    public static class LinkedClientOperationCoordinator
    {
        private static readonly object SyncRoot = new();
        private static readonly HashSet<string> HeldKeys =
            new(StringComparer.OrdinalIgnoreCase);

        public static bool TryAcquire(
            string iconName,
            string installPath,
            out IDisposable lease)
        {
            var keys = new List<string> { GetPathKey(installPath) };
            try
            {
                var config = ConfigHelper.Load();
                var entry = LinkedClientPolicy.FindEntry(
                    config, iconName, installPath);
                if (!string.IsNullOrWhiteSpace(entry?.LinkedClientGroupId))
                    keys.Add("group:" + entry.LinkedClientGroupId.Trim());
                var pendingDetach = config.PendingLinkedClientDetach;
                if (pendingDetach != null &&
                    (LinkedClientPolicy.AreSamePath(
                         installPath, pendingDetach.SourcePath) ||
                     LinkedClientPolicy.AreSamePath(
                         installPath, pendingDetach.TargetPath)))
                {
                    keys.Add("group:" + pendingDetach.GroupId.Trim());
                }
            }
            catch
            {
                // The normalized path still protects legacy and unregistered installs.
            }

            return TryAcquireKeys(keys, out lease);
        }

        public static bool TryAcquirePaths(
            IEnumerable<string> installPaths,
            out IDisposable lease) =>
            TryAcquireKeys(installPaths.Select(GetPathKey), out lease);

        private static bool TryAcquireKeys(
            IEnumerable<string> keys,
            out IDisposable lease)
        {
            var normalizedKeys = keys
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            lock (SyncRoot)
            {
                if (normalizedKeys.Any(HeldKeys.Contains))
                {
                    lease = null;
                    return false;
                }

                var lockStreams = new List<FileStream>();
                try
                {
                    var lockDirectory = Path.Combine(
                        Path.GetTempPath(),
                        "XelLauncher",
                        "LinkedClientOperationLocks");
                    Directory.CreateDirectory(lockDirectory);
                    foreach (var key in normalizedKeys)
                    {
                        var hash = Convert.ToHexString(SHA256.HashData(
                            Encoding.UTF8.GetBytes(key.ToUpperInvariant())));
                        lockStreams.Add(new FileStream(
                            Path.Combine(lockDirectory, hash + ".lock"),
                            FileMode.OpenOrCreate,
                            FileAccess.ReadWrite,
                            FileShare.None));
                    }
                }
                catch (IOException)
                {
                    foreach (var stream in lockStreams) stream.Dispose();
                    lease = null;
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    foreach (var stream in lockStreams) stream.Dispose();
                    lease = null;
                    return false;
                }

                foreach (var key in normalizedKeys) HeldKeys.Add(key);
                lease = new OperationLease(normalizedKeys, lockStreams);
                return true;
            }
        }

        private static string GetPathKey(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath)) return "path:<empty>";
            var fullPath = Path.GetFullPath(installPath).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return "path:" + fullPath;
        }

        private sealed class OperationLease : IDisposable
        {
            private string[] _keys;
            private List<FileStream> _lockStreams;

            public OperationLease(string[] keys, List<FileStream> lockStreams)
            {
                _keys = keys;
                _lockStreams = lockStreams;
            }

            public void Dispose()
            {
                var keys = System.Threading.Interlocked.Exchange(ref _keys, null);
                if (keys == null) return;
                var streams = System.Threading.Interlocked.Exchange(
                    ref _lockStreams, null);
                lock (SyncRoot)
                {
                    if (streams != null)
                    {
                        foreach (var stream in streams) stream.Dispose();
                    }
                    foreach (var key in keys) HeldKeys.Remove(key);
                }
            }
        }
    }
}
