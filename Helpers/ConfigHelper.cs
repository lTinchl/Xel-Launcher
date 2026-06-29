using System;
using System.IO;
using System.Text.Json;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class ConfigHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "XelLauncher");

        public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

        public static readonly string AccountBackupDir = Path.Combine(ConfigDir, "AccountBackups");

        public static readonly string EndAccountBackupDir = Path.Combine(ConfigDir, "EndAccountBackups");

        public static readonly string GlobalEndAccountBackupDir = Path.Combine(ConfigDir, "GlobalEndAccountBackups");

        public static readonly string CustomToolIconDir = Path.Combine(ConfigDir, "CustomToolIcons");

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigFile)) return new AppConfig();
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile))
                          ?? new AppConfig();
                cfg.UpdateState ??= new AppUpdateState();
                cfg.GameStatusCache ??= new();
                cfg.CustomToolLinks ??= new();
                cfg.NoticePanelCollapsed ??= new();
                MigrateLegacySecrets(cfg);
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
            SkylandTokenStorage.NormalizeBeforeSave(cfg);
            SkportTokenStorage.NormalizeBeforeSave(cfg);
            File.WriteAllText(ConfigFile,
                JsonSerializer.Serialize(cfg, JsonOptions));
        }

        private static void MigrateLegacySecrets(AppConfig cfg)
        {
            if (cfg.SkylandTokens == null || cfg.SkylandTokens.Count == 0) return;

            try
            {
                Directory.CreateDirectory(ConfigDir);
                SkylandTokenStorage.NormalizeBeforeSave(cfg);
            SkportTokenStorage.NormalizeBeforeSave(cfg);
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, JsonOptions));
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigSecretMigration");
            }
        }
    }
}
