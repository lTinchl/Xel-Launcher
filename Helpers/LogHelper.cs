using System;
using System.IO;

namespace XelLauncher.Helpers;

public static class LogHelper
{
    private static readonly string LogFile = Path.Combine(ConfigHelper.ConfigDir, "avalonia.log");

    public static void LogError(Exception ex, string scope)
    {
        try
        {
            Directory.CreateDirectory(ConfigHelper.ConfigDir);
            File.AppendAllText(LogFile, $"[{DateTimeOffset.Now:O}] {scope}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
