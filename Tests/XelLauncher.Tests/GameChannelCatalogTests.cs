using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class GameChannelCatalogTests
{
    [Fact]
    public void GlobalArknightsAlias_ResolvesToCanonicalChannel()
    {
        var canonical = GameChannelCatalog.Get("ArknightsGlobal");
        var legacyAlias = GameChannelCatalog.Get("ArknightsEn");

        Assert.NotNull(canonical);
        Assert.Same(canonical, legacyAlias);
        Assert.Equal(GameFamily.Arknights, canonical.Family);
        Assert.Null(canonical.PayloadProfile);
        Assert.False(canonical.SupportsLegacyLinkedClient);
        Assert.False(canonical.SupportsAccountSwitch);
        Assert.Equal("Global", canonical.ChannelLabel);
        Assert.Equal("Global", canonical.Channel);
        Assert.Equal("Arknights", canonical.GameId);
        Assert.Equal("", canonical.ClientCompatibilityGroup);
        Assert.False(LinkedClientPolicy.IsArknightsChannel("ArknightsEn"));
    }

    [Fact]
    public void FirstPhaseChannels_HaveExpectedCompatibilityGroups()
    {
        Assert.All(new[] { "Arknights", "BiliArknights" }, iconName =>
            Assert.Equal("Arknights-cn",
                GameChannelCatalog.Get(iconName).ClientCompatibilityGroup));
        Assert.All(new[]
        {
            "Endfield", "BiliEndfield", "GlobalEndfield", "PlayEndfield"
        }, iconName =>
            Assert.Equal("Endfield",
                GameChannelCatalog.Get(iconName).ClientCompatibilityGroup));
    }

    [Fact]
    public void InstalledPlugin_ExposesAllYostarPresets()
    {
        foreach (var iconName in new[]
                 {
                     "ArknightsGlobal", "ArknightsJp", "ArknightsKr"
                 })
        {
            var channel = GameChannelCatalog.Get(iconName);
            Assert.NotNull(channel);
            Assert.True(channel.IsAvailable, iconName);
            Assert.False(channel.ShowByDefault, iconName);
            using var service = new EndfieldService(iconName);
        }
    }

    [Fact]
    public void LegacyPayloadProfiles_AreLimitedToSupportedProtocolChannels()
    {
        var expected = new[]
        {
            "Arknights",
            "BiliArknights",
            "Endfield",
            "BiliEndfield",
            "GlobalEndfield",
            "PlayEndfield"
        };

        Assert.Equal(
            expected,
            GameChannelCatalog.ServerPayloadProfiles.Select(x => x.IconName));
        Assert.Equal(
            expected,
            GameChannelCatalog.CreateDefaultServerPayloadProfileIds());
        Assert.DoesNotContain(
            GameChannelCatalog.ServerPayloadProfiles,
            profile => profile.IconName.StartsWith(
                "ArknightsGlobal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultEntries_ExcludeOptionalYostarChannels()
    {
        var iconNames = GameChannelCatalog.CreateDefaultGameEntries()
            .Select(entry => entry.IconName)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Arknights",
                "BiliArknights",
                "Endfield",
                "BiliEndfield",
                "GlobalEndfield",
                "PlayEndfield"
            },
            iconNames);
        Assert.DoesNotContain("ArknightsGlobal", iconNames);
        Assert.DoesNotContain("ArknightsJp", iconNames);
        Assert.DoesNotContain("ArknightsKr", iconNames);
        Assert.DoesNotContain("ArknightsEn", iconNames);
    }
}
