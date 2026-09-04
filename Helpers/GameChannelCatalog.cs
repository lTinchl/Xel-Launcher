using System;
using System.Collections.Generic;
using System.Linq;
using Hi3Helper.Plugin.Arknights.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Endfield.Management.PresetConfig;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public enum GameFamily
    {
        Arknights,
        Endfield
    }

    public sealed class GameChannelDefinition
    {
        private readonly Func<PluginPresetConfigBase> _presetFactory;
        private readonly Func<bool> _availability;

        internal GameChannelDefinition(
            string iconName,
            string storeName,
            string labelZh,
            string labelEn,
            GameFamily family,
            string channel,
            string clientCompatibilityGroup,
            string iconFileName,
            bool roundIconCorners,
            bool supportsLegacyLinkedClient,
            bool supportsAccountSwitch,
            string channelLabel,
            ServerPayloadProfile payloadProfile,
            Func<PluginPresetConfigBase> presetFactory,
            bool showByDefault = true,
            Func<bool> availability = null,
            params string[] aliases)
        {
            IconName = iconName;
            StoreName = storeName;
            LabelZh = labelZh;
            LabelEn = labelEn;
            Family = family;
            GameId = family.ToString();
            Channel = channel;
            ClientCompatibilityGroup = clientCompatibilityGroup ?? "";
            IconFileName = iconFileName;
            RoundIconCorners = roundIconCorners;
            SupportsLegacyLinkedClient = supportsLegacyLinkedClient;
            SupportsAccountSwitch = supportsAccountSwitch;
            ChannelLabel = channelLabel;
            PayloadProfile = payloadProfile;
            ShowByDefault = showByDefault;
            _presetFactory = presetFactory;
            _availability = availability ?? (() => true);
            Aliases = Array.AsReadOnly(
                aliases?.ToArray() ?? Array.Empty<string>());
        }

        public string IconName { get; }
        public string StoreName { get; }
        public string LabelZh { get; }
        public string LabelEn { get; }
        public GameFamily Family { get; }
        public string GameId { get; }
        public string Channel { get; }
        public string ClientCompatibilityGroup { get; }
        public string IconFileName { get; }
        public bool RoundIconCorners { get; }
        public bool SupportsLegacyLinkedClient { get; }
        public bool SupportsAccountSwitch { get; }
        public string ChannelLabel { get; }
        public ServerPayloadProfile PayloadProfile { get; }
        public bool ShowByDefault { get; }
        public IReadOnlyList<string> Aliases { get; }
        public bool IsAvailable => _availability();

        public bool Matches(string iconName)
        {
            if (string.Equals(IconName, iconName, StringComparison.OrdinalIgnoreCase))
                return true;

            return Aliases.Any(alias =>
                string.Equals(alias, iconName, StringComparison.OrdinalIgnoreCase));
        }

        public string GetDisplayName(bool english) => english ? LabelEn : LabelZh;

        internal PluginPresetConfigBase CreatePreset()
        {
            if (!IsAvailable)
                throw new NotSupportedException(
                    $"The installed Hypergryph plugin does not support {IconName}.");

            return _presetFactory();
        }
    }

    /// <summary>
    /// Single source of truth for channel identity, plugin presets and the legacy
    /// Hypergryph launcher payload protocol. Yostar channels deliberately have no
    /// ServerPayloadProfile because the updated plugin manages them via its own
    /// JSON/CRC64 protocol instead of the encrypted game_files payload protocol.
    /// </summary>
    public static class GameChannelCatalog
    {
        private const string DomesticApiUrl =
            "https://launcher.hypergryph.com/api/proxy/batch_proxy";
        private const string GlobalApiUrl =
            "https://launcher.gryphline.com/api/proxy/batch_proxy";
        private const string DomesticLauncherAppCode = "abYeZZ16BPluCFyT";
        private const string EndfieldGlobalAppCode = "YDUTE5gscDZ229CW";
        private const long MiB = 1024L * 1024L;

        private static readonly string[] CommonRootFiles =
        {
            "U8CoreUI.dll", "U8SDK.dll", "u8_channel.dll"
        };

        private static readonly string[] CommonRequiredFiles =
        {
            "U8CoreUI.dll",
            "U8SDK.dll",
            "u8_channel.dll",
            "U8Data/config/config.bin",
            "U8Data/config/config.gryph",
            "U8Data/config/u8ExtraConfig.bin"
        };

        private static readonly ServerPayloadProfile ArknightsPayload = CreatePayload(
            "Arknights", "ArkOfficial", GameFamily.Arknights,
            DomesticApiUrl, "GzD1CpaWgmSq1wew", DomesticLauncherAppCode,
            "1", "1", "5",
            new[] { "hgsdk.dll", "PlatformProcess.dll", "PlatformProcess.exe", "webviewsdk.dll" },
            new[] { "sdkdata", "U8Data/config" },
            new[] { "hgsdk.dll", "PlatformProcess.dll", "PlatformProcess.exe", "webviewsdk.dll" });

        private static readonly ServerPayloadProfile BiliArknightsPayload = CreatePayload(
            "BiliArknights", "ArkBilibili", GameFamily.Arknights,
            DomesticApiUrl, "GzD1CpaWgmSq1wew", DomesticLauncherAppCode,
            "2", "2", "5",
            new[] { "PCGameSDK.dll", "PlatformProcess.dll", "PlatformProcess.exe", "webviewsdk.dll" },
            new[] { "BLPlatform64", "U8Data/config" },
            new[]
            {
                "PCGameSDK.dll", "PlatformProcess.dll", "PlatformProcess.exe",
                "webviewsdk.dll", "BLPlatform64/PCGamePlatform.exe"
            },
            bilibiliLimits: true);

        private static readonly ServerPayloadProfile EndfieldPayload = CreatePayload(
            "Endfield", "EndOfficial", GameFamily.Endfield,
            DomesticApiUrl, "6LL0KJuqHBVz33WK", DomesticLauncherAppCode,
            "1", "1", "5",
            new[] { "eld_Endfield.db", "hgsdk.dll" },
            new[] { "sdkdata", "U8Data/config" },
            new[] { "hgsdk.dll" });

        private static readonly ServerPayloadProfile BiliEndfieldPayload = CreatePayload(
            "BiliEndfield", "EndBilibili", GameFamily.Endfield,
            DomesticApiUrl, "6LL0KJuqHBVz33WK", DomesticLauncherAppCode,
            "2", "2", "5",
            new[] { "eld_Endfield.db", "PCGameSDK.dll" },
            new[] { "BLPlatform64", "U8Data/config" },
            new[] { "PCGameSDK.dll", "BLPlatform64/PCGamePlatform.exe" },
            bilibiliLimits: true);

        private static readonly ServerPayloadProfile GlobalEndfieldPayload = CreatePayload(
            "GlobalEndfield", "EndGlobal", GameFamily.Endfield,
            GlobalApiUrl, EndfieldGlobalAppCode, EndfieldGlobalAppCode,
            "6", "6", "3",
            new[] { "gfsdk.dll", "glfoundation.dll" },
            new[] { "sdkdata", "U8Data/config" },
            new[] { "gfsdk.dll", "glfoundation.dll" });

        private static readonly ServerPayloadProfile PlayEndfieldPayload = CreatePayload(
            "PlayEndfield", "EndPlay", GameFamily.Endfield,
            GlobalApiUrl, EndfieldGlobalAppCode, EndfieldGlobalAppCode,
            "6", "802", "3",
            new[]
            {
                "gfsdk.dll", "glextra.dll", "glfoundation.dll", "manifest.xml",
                "play_pc_sdk.dll"
            },
            new[] { "sdkdata", "U8Data/config" },
            new[]
            {
                "gfsdk.dll", "glextra.dll", "glfoundation.dll", "manifest.xml",
                "play_pc_sdk.dll"
            });

        private static readonly string[] GlobalArknightsPresetTypes =
        {
            "Hi3Helper.Plugin.Arknights.Management.PresetConfig.ArknightsGlobalPresetConfig",
            "Hi3Helper.Plugin.Arknights.Management.PresetConfig.ArknightsEnPresetConfig"
        };

        private static readonly string[] JapanArknightsPresetTypes =
        {
            "Hi3Helper.Plugin.Arknights.Management.PresetConfig.ArknightsJpPresetConfig"
        };

        private static readonly string[] KoreaArknightsPresetTypes =
        {
            "Hi3Helper.Plugin.Arknights.Management.PresetConfig.ArknightsKrPresetConfig"
        };

        private static readonly GameChannelDefinition[] ChannelList =
        {
            new(
                "Arknights", "明日方舟", "明日方舟（官服）", "Arknights (Official)",
                GameFamily.Arknights, "Official", "Arknights-cn",
                "Arknights.ico", false, true, true, "Official",
                ArknightsPayload, () => new ArknightsCnPresetConfig()),
            new(
                "BiliArknights", "明日方舟(B服)", "明日方舟（B服）", "Arknights (Bilibili)",
                GameFamily.Arknights, "Bilibili", "Arknights-cn",
                "BiliArknights.ico", false, true, false, "Bilibili",
                BiliArknightsPayload, () => new ArknightsBiliPresetConfig()),
            new(
                "ArknightsGlobal", "明日方舟(国际服)", "明日方舟（国际服）", "Arknights (Global)",
                GameFamily.Arknights, "Global", "",
                "Arknights.ico", false, false, false, "Global",
                null,
                () => CreateReflectedPreset(GlobalArknightsPresetTypes),
                showByDefault: false,
                availability: () =>
                    FindArknightsPresetType(GlobalArknightsPresetTypes) != null,
                aliases: new[] { "ArknightsEn" }),
            new(
                "ArknightsJp", "明日方舟(日服)", "明日方舟（日服）", "Arknights (Japan)",
                GameFamily.Arknights, "Japan", "",
                "Arknights.ico", false, false, false, "Japan",
                null,
                () => CreateReflectedPreset(JapanArknightsPresetTypes),
                showByDefault: false,
                availability: () =>
                    FindArknightsPresetType(JapanArknightsPresetTypes) != null),
            new(
                "ArknightsKr", "明日方舟(韩服)", "明日方舟（韩服）", "Arknights (Korea)",
                GameFamily.Arknights, "Korea", "",
                "Arknights.ico", false, false, false, "Korea",
                null,
                () => CreateReflectedPreset(KoreaArknightsPresetTypes),
                showByDefault: false,
                availability: () =>
                    FindArknightsPresetType(KoreaArknightsPresetTypes) != null),
            new(
                "Endfield", "终末地", "明日方舟：终末地（官服）", "Endfield (Official)",
                GameFamily.Endfield, "Official", "Endfield",
                "Endfield.ico", false, false, true, "Official",
                EndfieldPayload, () => new EndfieldCnPresetConfig()),
            new(
                "BiliEndfield", "终末地(B服)", "明日方舟：终末地（B服）", "Endfield (Bilibili)",
                GameFamily.Endfield, "Bilibili", "Endfield",
                "BiliEndfield.ico", false, false, false, "Bilibili",
                BiliEndfieldPayload, () => new EndfieldBiliPresetConfig()),
            new(
                "GlobalEndfield", "终末地(国际服)", "明日方舟：终末地（国际服）", "Endfield (Global)",
                GameFamily.Endfield, "Global", "Endfield",
                "GlobalEndfield.ico", true, false, true, "Global",
                GlobalEndfieldPayload, () => new EndfieldGlobalPresetConfig()),
            new(
                "PlayEndfield", "终末地(GooglePlay)", "明日方舟：终末地（GooglePlay）",
                "Endfield (Google Play)", GameFamily.Endfield, "GooglePlay", "Endfield",
                "PlayEndfield.ico", true,
                false, false, "Google Play",
                PlayEndfieldPayload, () => new EndfieldGooglePlayPresetConfig())
        };

        private static readonly IReadOnlyList<GameChannelDefinition> ReadOnlyChannels =
            Array.AsReadOnly(ChannelList);

        private static readonly IReadOnlyList<ServerPayloadProfile> PayloadProfiles =
            Array.AsReadOnly(ChannelList
                .Select(channel => channel.PayloadProfile)
                .Where(profile => profile != null)
                .ToArray());

        public static IReadOnlyList<GameChannelDefinition> Channels => ReadOnlyChannels;

        public static IReadOnlyList<ServerPayloadProfile> ServerPayloadProfiles =>
            PayloadProfiles;

        public static GameChannelDefinition Get(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName)) return null;
            return ChannelList.FirstOrDefault(channel => channel.Matches(iconName));
        }

        public static PluginPresetConfigBase CreatePreset(string iconName)
        {
            var channel = Get(iconName) ??
                          throw new ArgumentException(
                              $"Unknown game channel: {iconName}", nameof(iconName));
            return channel.CreatePreset();
        }

        public static bool IsFamily(string iconName, GameFamily family) =>
            Get(iconName)?.Family == family;

        public static GameChannelDefinition GetByGameAndChannel(
            string gameId,
            string channel) =>
            ChannelList.FirstOrDefault(definition =>
                string.Equals(definition.GameId, gameId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(definition.Channel, channel,
                    StringComparison.OrdinalIgnoreCase));

        public static bool CanSwitchChannel(string sourceIconName, string targetIconName)
        {
            var source = Get(sourceIconName);
            var target = Get(targetIconName);
            return source != null && target != null &&
                   source.PayloadProfile != null && target.PayloadProfile != null &&
                   !string.IsNullOrWhiteSpace(source.ClientCompatibilityGroup) &&
                   string.Equals(source.GameId, target.GameId,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(source.ClientCompatibilityGroup,
                       target.ClientCompatibilityGroup,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanCreateLinkedRuntime(
            string sourceIconName,
            string targetIconName) =>
            !string.Equals(sourceIconName, targetIconName,
                StringComparison.OrdinalIgnoreCase) &&
            CanSwitchChannel(sourceIconName, targetIconName);

        public static List<GameEntry> CreateDefaultGameEntries() =>
            ChannelList
                .Where(channel => channel.ShowByDefault && channel.IsAvailable)
                .Select(channel => new GameEntry
                {
                    Name = channel.StoreName,
                    IconName = channel.IconName
                })
                .ToList();

        public static List<string> CreateDefaultServerPayloadProfileIds() =>
            ServerPayloadProfiles.Select(profile => profile.IconName).ToList();

        private static ServerPayloadProfile CreatePayload(
            string iconName,
            string payloadDirectoryName,
            GameFamily family,
            string apiUrl,
            string appCode,
            string launcherAppCode,
            string channel,
            string subChannel,
            string sequence,
            IReadOnlyList<string> extraRootFiles,
            IReadOnlyList<string> directoryPrefixes,
            IReadOnlyList<string> extraRequiredFiles,
            bool bilibiliLimits = false)
        {
            return new ServerPayloadProfile(
                iconName,
                payloadDirectoryName,
                family,
                apiUrl,
                appCode,
                launcherAppCode,
                channel,
                subChannel,
                sequence,
                Array.AsReadOnly(
                    CommonRootFiles.Concat(extraRootFiles).ToArray()),
                Array.AsReadOnly(directoryPrefixes.ToArray()),
                Array.AsReadOnly(
                    CommonRequiredFiles.Concat(extraRequiredFiles).ToArray()),
                bilibiliLimits ? 1000 : 128,
                bilibiliLimits ? 512 * MiB : 128 * MiB,
                bilibiliLimits ? 256 * MiB : 64 * MiB);
        }

        private static Type FindArknightsPresetType(IEnumerable<string> typeNames)
        {
            var assembly = typeof(ArknightsCnPresetConfig).Assembly;
            return typeNames
                .Select(assembly.GetType)
                .FirstOrDefault(type =>
                    type != null &&
                    typeof(PluginPresetConfigBase).IsAssignableFrom(type) &&
                    !type.IsAbstract);
        }

        private static PluginPresetConfigBase CreateReflectedPreset(
            IEnumerable<string> typeNames)
        {
            var type = FindArknightsPresetType(typeNames) ??
                       throw new NotSupportedException(
                           "The installed Hypergryph plugin does not contain the requested Yostar preset.");
            return (PluginPresetConfigBase)Activator.CreateInstance(type);
        }
    }
}
