using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class ConfigHelper
    {
        private const string ConfigDirOverrideEnvironmentVariable =
            "XELLAUNCHER_CONFIG_DIR";
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly object SaveLock = new object();

        public static readonly string ConfigDir = ResolveConfigDirectory();

        public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

        public static readonly string ConfigBackupFile = ConfigFile + ".bak";

        public static readonly string AccountBackupDir = Path.Combine(ConfigDir, "AccountBackups");

        public static readonly string EndAccountBackupDir = Path.Combine(ConfigDir, "EndAccountBackups");

        public static readonly string GlobalEndAccountBackupDir = Path.Combine(ConfigDir, "GlobalEndAccountBackups");

        public static readonly string CustomToolIconDir = Path.Combine(ConfigDir, "CustomToolIcons");

        public static AppConfig Load()
        {
            try
            {
                var cfg = ReadConfigWithFallback(
                    out var loadedFromBackup,
                    out var primaryReadError);
                InitializeConfig(cfg);
                if (loadedFromBackup)
                {
                    if (primaryReadError != null)
                        LogHelper.LogError(primaryReadError, "ConfigLoadPrimary");
                    cfg = RestorePrimaryFromBackup(cfg);
                    InitializeConfig(cfg);
                }
                MigrateLegacySecrets(cfg);
                cfg = ArknightsLinkedClientService.TryRecoverPendingRegistration(cfg);
                return cfg;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigLoad");
                return new AppConfig();
            }
        }

        public static void Save(
            AppConfig cfg,
            bool allowLinkedClientStateChange = false)
        {
            lock (SaveLock)
            {
                Directory.CreateDirectory(ConfigDir);
                using var crossProcessLock = AcquireCrossProcessSaveLock();
                InitializeConfig(cfg);
                if (!allowLinkedClientStateChange)
                    PreserveLinkedClientSafetyState(cfg);
                PreserveSharedRootState(cfg);
                SkylandTokenStorage.NormalizeBeforeSave(cfg);
                SkportTokenStorage.NormalizeBeforeSave(cfg);
                WriteConfigAtomic(cfg);
            }
        }

        public static AppConfig Update(
            Action<AppConfig> update,
            bool allowLinkedClientStateChange = false)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            lock (SaveLock)
            {
                Directory.CreateDirectory(ConfigDir);
                using var crossProcessLock = AcquireCrossProcessSaveLock();
                var config = ReadConfigWithFallback(out _, out _);
                InitializeConfig(config);
                update(config);
                if (!allowLinkedClientStateChange)
                    PreserveLinkedClientSafetyState(config);
                SkylandTokenStorage.NormalizeBeforeSave(config);
                SkportTokenStorage.NormalizeBeforeSave(config);
                WriteConfigAtomic(config);
                return config;
            }
        }

        private static void WriteConfigAtomic(AppConfig config)
        {
            var tempFile = Path.Combine(
                ConfigDir,
                $".{Path.GetFileName(ConfigFile)}.{Guid.NewGuid():N}.tmp");
            var backupTempFile = Path.Combine(
                ConfigDir,
                $".{Path.GetFileName(ConfigBackupFile)}.{Guid.NewGuid():N}.tmp");

            try
            {
                WriteBytesDurable(
                    tempFile,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                        .GetBytes(JsonSerializer.Serialize(config, JsonOptions)));

                if (File.Exists(ConfigFile))
                {
                    if (TryReadConfigFile(ConfigFile, out var previousConfig))
                    {
                        InitializeConfig(previousConfig);
                        SkylandTokenStorage.NormalizeBeforeSave(previousConfig);
                        SkportTokenStorage.NormalizeBeforeSave(previousConfig);
                        WriteBytesDurable(
                            backupTempFile,
                            new UTF8Encoding(
                                    encoderShouldEmitUTF8Identifier: false)
                                .GetBytes(JsonSerializer.Serialize(
                                    previousConfig, JsonOptions)));
                        File.Move(
                            backupTempFile,
                            ConfigBackupFile,
                            overwrite: true);
                    }

                    File.Replace(
                        tempFile,
                        ConfigFile,
                        destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempFile, ConfigFile, overwrite: false);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "ConfigSaveTempCleanup");
                    }
                }

                if (File.Exists(backupTempFile))
                {
                    try
                    {
                        File.Delete(backupTempFile);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex, "ConfigBackupTempCleanup");
                    }
                }
            }
        }

        private static void WriteBytesDurable(string path, byte[] content)
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough);
            stream.Write(content, 0, content.Length);
            stream.Flush(flushToDisk: true);
        }

        private static AppConfig ReadConfigWithFallback(
            out bool loadedFromBackup,
            out Exception primaryReadError)
        {
            loadedFromBackup = false;
            primaryReadError = null;

            if (File.Exists(ConfigFile))
            {
                try
                {
                    return ReadConfigFile(ConfigFile);
                }
                catch (Exception ex)
                {
                    primaryReadError = ex;
                }
            }

            if (File.Exists(ConfigBackupFile))
            {
                try
                {
                    loadedFromBackup = true;
                    return ReadConfigFile(ConfigBackupFile);
                }
                catch (Exception backupError)
                {
                    if (primaryReadError != null)
                    {
                        throw new AggregateException(
                            "Both the primary and backup configuration files are invalid.",
                            primaryReadError,
                            backupError);
                    }
                    throw;
                }
            }

            if (primaryReadError != null) throw primaryReadError;
            return new AppConfig();
        }

        private static AppConfig ReadConfigFile(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<AppConfig>(stream) ??
                   throw new InvalidDataException(
                       $"Configuration is empty: {path}");
        }

        private static bool TryReadConfigFile(
            string path,
            out AppConfig config)
        {
            try
            {
                config = ReadConfigFile(path);
                return true;
            }
            catch
            {
                config = null;
                return false;
            }
        }

        private static AppConfig RestorePrimaryFromBackup(AppConfig config)
        {
            try
            {
                lock (SaveLock)
                {
                    Directory.CreateDirectory(ConfigDir);
                    using var crossProcessLock = AcquireCrossProcessSaveLock();
                    if (TryReadConfigFile(ConfigFile, out var current))
                    {
                        InitializeConfig(current);
                        return current;
                    }
                    SkylandTokenStorage.NormalizeBeforeSave(config);
                    SkportTokenStorage.NormalizeBeforeSave(config);
                    WriteConfigAtomic(config);
                    return config;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigRestoreFromBackup");
                return config;
            }
        }

        private static string ResolveConfigDirectory()
        {
            var overridePath = Environment.GetEnvironmentVariable(
                ConfigDirOverrideEnvironmentVariable);
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "XelLauncher")
                : Path.GetFullPath(overridePath);
        }

        private static void InitializeConfig(AppConfig cfg)
        {
            cfg.Games ??= GameChannelCatalog.CreateDefaultGameEntries();
            foreach (var game in cfg.Games)
            {
                if (GameChannelCatalog.Get(game?.IconName)?.ShowByDefault == false &&
                    !string.IsNullOrWhiteSpace(game.RootPath))
                {
                    game.AddedManually = true;
                }
            }
            cfg.Games.RemoveAll(game =>
            {
                var channel = GameChannelCatalog.Get(game?.IconName);
                return channel?.ShowByDefault == false &&
                       game.AddedManually == false &&
                       string.IsNullOrWhiteSpace(game.RootPath);
            });
            cfg.UpdateState ??= new AppUpdateState();
            cfg.GameStatusCache ??= new();
            cfg.SharedRoots ??= new();
            cfg.SharedRoots.RemoveAll(sharedRoot => sharedRoot == null);
            foreach (var sharedRoot in cfg.SharedRoots)
            {
                sharedRoot.GameId ??= "";
                sharedRoot.RootPath ??= "";
                sharedRoot.BaseChannel ??= "Unknown";
                sharedRoot.BaseVersion ??= "";
                sharedRoot.LinkedRuntimes ??= new();
                sharedRoot.LinkedRuntimes.RemoveAll(runtime => runtime == null);
                foreach (var runtime in sharedRoot.LinkedRuntimes)
                {
                    runtime.Channel ??= "";
                    runtime.RuntimePath ??= "";
                    runtime.CompatibilityGroup ??= "";
                    runtime.BaseChannel ??= "";
                    runtime.BaseManifestSha256 ??= "";
                    runtime.TargetManifestSha256 ??= "";
                    runtime.BaseVersion ??= "";
                    runtime.TargetVersion ??= "";
                    runtime.PayloadManifestSha256 ??= "";
                    runtime.Health ??= "Invalid";
                }
            }
            cfg.CustomToolLinks ??= new();
            cfg.NoticePanelCollapsed ??= new();
            if (cfg.PendingLinkedClientDetach != null)
                cfg.PendingLinkedClientDetach.LinkedFiles ??= new();
            cfg.ServerPayloadAutoUpdateProfiles ??=
                GameChannelCatalog.CreateDefaultServerPayloadProfileIds();
        }

        private static FileStream AcquireCrossProcessSaveLock()
        {
            var lockPath = Path.Combine(ConfigDir, ".config-write.lock");
            Exception lastError = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    return new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    Thread.Sleep(20);
                }
            }

            throw new IOException(
                "Timed out waiting for the configuration write lock.",
                lastError);
        }

        private static void PreserveLinkedClientSafetyState(AppConfig config)
        {
            if (!File.Exists(ConfigFile) && !File.Exists(ConfigBackupFile)) return;

            try
            {
                var current = ReadConfigWithFallback(out _, out _);
                InitializeConfig(current);

                var safetyStateExists = current.PendingLinkedClient != null ||
                                        config.PendingLinkedClient != null ||
                                        current.PendingLinkedClientDetach != null ||
                                        config.PendingLinkedClientDetach != null ||
                                        current.Games.Exists(game =>
                                            LinkedClientPolicy.IsArknightsChannel(
                                                game.IconName) &&
                                            !string.IsNullOrWhiteSpace(
                                                game.LinkedClientGroupId)) ||
                                        config.Games.Exists(game =>
                                            LinkedClientPolicy.IsArknightsChannel(
                                                game.IconName) &&
                                            !string.IsNullOrWhiteSpace(
                                                game.LinkedClientGroupId)) ||
                                        current.Games.Exists(game =>
                                            LinkedClientPolicy.IsArknightsChannel(
                                                game.IconName) &&
                                            ArknightsLinkedClientService
                                                .HasLinkedClientMarker(
                                                    game.RootPath)) ||
                                        config.Games.Exists(game =>
                                            LinkedClientPolicy.IsArknightsChannel(
                                                game.IconName) &&
                                            ArknightsLinkedClientService
                                                .HasLinkedClientMarker(
                                                    game.RootPath));
                if (!safetyStateExists) return;

                config.PendingLinkedClient = current.PendingLinkedClient;
                config.PendingLinkedClientDetach =
                    current.PendingLinkedClientDetach;
                foreach (var currentEntry in current.Games)
                {
                    if (!LinkedClientPolicy.IsArknightsChannel(
                            currentEntry.IconName))
                    {
                        continue;
                    }

                    var targetEntry = config.Games.Find(game =>
                        string.Equals(game.IconName, currentEntry.IconName,
                            StringComparison.OrdinalIgnoreCase));
                    if (targetEntry == null)
                    {
                        throw new InvalidDataException(
                            "A protected linked-client configuration entry is missing.");
                    }
                    targetEntry.RootPath = currentEntry.RootPath;
                    targetEntry.IndependentChannelClient =
                        currentEntry.IndependentChannelClient;
                    targetEntry.LinkedClientGroupId =
                        currentEntry.LinkedClientGroupId;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigLinkedClientStateMerge");
                throw;
            }
        }

        private static void PreserveSharedRootState(AppConfig config)
        {
            if (!File.Exists(ConfigFile) && !File.Exists(ConfigBackupFile)) return;
            try
            {
                var current = ReadConfigWithFallback(out _, out _);
                InitializeConfig(current);
                if (current.SharedRoots.Count == 0 &&
                    (config.SharedRoots?.Count ?? 0) == 0)
                {
                    return;
                }

                // Shared-root facts and runtime metadata are changed through
                // ConfigHelper.Update. A UI save based on an older snapshot must
                // not resurrect an old BaseChannel or clear a Stale marker.
                config.SharedRoots = current.SharedRoots;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigSharedRootStateMerge");
                throw;
            }
        }

        private static void MigrateLegacySecrets(AppConfig cfg)
        {
            if (cfg.SkylandTokens == null || cfg.SkylandTokens.Count == 0) return;

            try
            {
                Save(cfg);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "ConfigSecretMigration");
            }
        }
    }
}
