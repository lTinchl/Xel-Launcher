using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XelLauncher.Helpers
{
    public static class LogHelper
    {
        private static readonly List<string> _entries = new();
        public static event Action OnLog;

        private static readonly string LogDir  = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)!, "logs");
        private static readonly string LogFile = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)!, "logs", "app.log");
        private const long MaxLogSize = 5 * 1024 * 1024; // 5 MB
        private static readonly object _fileLock = new();

        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            lock (_entries) _entries.Add(line);
            WriteToFile(line);
            NotifyChanged();
        }

        public static void LogError(Exception ex, string context = null)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string header = context != null
                ? $"[ERROR] {context}: {ex.Message}"
                : $"[ERROR] {ex.Message}";
            string fullLine = $"[{timestamp}] {header}{Environment.NewLine}{ex}";
            lock (_entries) _entries.Add($"[{timestamp}] {header}");
            WriteToFile(fullLine);
            NotifyChanged();
        }

        public static string GetAll()
        {
            lock (_entries)
            {
                if (_entries.Count > 0)
                    return string.Join(Environment.NewLine, _entries);
            }

            try
            {
                return File.Exists(LogFile) ? File.ReadAllText(LogFile, Encoding.UTF8) : "";
            }
            catch
            {
                return "";
            }
        }

        private static void WriteToFile(string line)
        {
            try
            {
                lock (_fileLock)
                {
                    Directory.CreateDirectory(LogDir);
                    if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxLogSize)
                        File.Delete(LogFile);
                    File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* 文件写入失败不抛异常，避免死循环 */ }
        }

        private static void NotifyChanged()
        {
            var handlers = OnLog;
            if (handlers == null) return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
            }
        }
    }
}
