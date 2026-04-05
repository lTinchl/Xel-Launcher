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
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            lock (_entries) _entries.Add(line);
            WriteToFile(line);
            OnLog?.Invoke();
        }

        public static void LogError(Exception ex, string context = null)
        {
            string header = context != null
                ? $"[ERROR] {context}: {ex.Message}"
                : $"[ERROR] {ex.Message}";
            string fullLine = $"[{DateTime.Now:HH:mm:ss}] {header}{Environment.NewLine}{ex}";
            lock (_entries) _entries.Add($"[{DateTime.Now:HH:mm:ss}] {header}");
            WriteToFile(fullLine);
            OnLog?.Invoke();
        }

        public static string GetAll()
        {
            lock (_entries) return string.Join(Environment.NewLine, _entries);
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
    }
}
