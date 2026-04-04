using System;
using System.IO;
using System.Text.Json;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class ConfigHelper
    {
        public static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "XelLauncher");

        public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

        public static readonly string AccountBackupDir = Path.Combine(ConfigDir, "AccountBackups");

        public static readonly string EndAccountBackupDir = Path.Combine(ConfigDir, "EndAccountBackups");

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigFile)) return new AppConfig();
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile))
                       ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig cfg)
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigFile,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
