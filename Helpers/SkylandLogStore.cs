using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace XelLauncher.Helpers
{
    public static class SkylandLogStore
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan Retention = TimeSpan.FromDays(3);
        private static readonly string LogFile = Path.Combine(ConfigHelper.ConfigDir, "SkylandSign.log");
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

        public static string[] LoadRecent()
        {
            lock (SyncRoot)
            {
                TrimLocked();
                return File.Exists(LogFile) ? File.ReadAllLines(LogFile) : Array.Empty<string>();
            }
        }

        public static string Append(string message)
        {
            var line = $"[{DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}] {message}";
            lock (SyncRoot)
            {
                Directory.CreateDirectory(ConfigHelper.ConfigDir);
                TrimLocked();
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }

            return line;
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                if (File.Exists(LogFile))
                    File.Delete(LogFile);
            }
        }

        private static void TrimLocked()
        {
            if (!File.Exists(LogFile)) return;

            var cutoff = DateTime.Now - Retention;
            var retained = File.ReadAllLines(LogFile)
                .Where(line => TryReadTimestamp(line, out var timestamp) && timestamp >= cutoff)
                .ToArray();

            if (retained.Length == 0)
            {
                File.Delete(LogFile);
                return;
            }

            File.WriteAllLines(LogFile, retained);
        }

        private static bool TryReadTimestamp(string line, out DateTime timestamp)
        {
            timestamp = default;
            if (string.IsNullOrWhiteSpace(line) || line.Length < TimestampFormat.Length + 2)
                return false;
            if (line[0] != '[' || line[TimestampFormat.Length + 1] != ']')
                return false;

            var text = line.Substring(1, TimestampFormat.Length);
            return DateTime.TryParseExact(text, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
        }
    }
}
