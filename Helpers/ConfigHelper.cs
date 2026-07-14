using System;
using System.IO;
using System.Text.Json;
using XelLauncher.Models;

namespace XelLauncher.Helpers;

public static class ConfigHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XelLauncher");

    public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigFile))
        {
            return new AppConfig();
        }

        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile)) ?? new AppConfig();
            cfg.Games ??= [];
            cfg.GameStatusCache ??= [];
            cfg.LauncherNoticeCache ??= [];
            cfg.UpdateState ??= new AppUpdateState();
            return cfg;
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, "ConfigLoad");
            return new AppConfig();
        }
    }

    public static void Save(AppConfig cfg)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, JsonOptions));
    }
}
