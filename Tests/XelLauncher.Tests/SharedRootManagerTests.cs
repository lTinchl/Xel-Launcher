using XelLauncher.Helpers;
using XelLauncher.Models;
using System.Text.Json;

namespace XelLauncher.Tests;

public sealed class SharedRootManagerTests
{
    [Fact]
    public void SameCompatibleRoot_CreatesOnePhysicalSharedRootWithoutChangingPaths()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Arknights");
        var config = new AppConfig();
        var official = config.Games.Single(game => game.IconName == "Arknights");
        var bilibili = config.Games.Single(game => game.IconName == "BiliArknights");
        official.RootPath = root;
        bilibili.RootPath = Path.Combine(root, ".");

        var resolution = SharedRootManager.Resolve(
            config, "Arknights", root,
            detectBaseChannel: false, out var changed);

        Assert.True(changed);
        Assert.Equal(SharedRootMode.Shared, resolution.Mode);
        Assert.Equal("Arknights", resolution.GameId);
        Assert.Equal("Arknights-cn", resolution.Target.ClientCompatibilityGroup);
        Assert.Single(config.SharedRoots);
        Assert.True(SharedRootManager.PathsEqual(
            root, config.SharedRoots[0].RootPath));
        Assert.Equal(root, official.RootPath);
        Assert.Equal(Path.Combine(root, "."), bilibili.RootPath);
    }

    [Fact]
    public void DifferentRoots_RemainIndependentAndDoNotCreateState()
    {
        using var temp = new TemporaryDirectory();
        var config = new AppConfig();
        config.Games.Single(game => game.IconName == "Arknights").RootPath =
            temp.CreateDirectory("Official");
        config.Games.Single(game => game.IconName == "BiliArknights").RootPath =
            temp.CreateDirectory("Bilibili");

        var resolution = SharedRootManager.Resolve(
            config,
            "Arknights",
            config.Games.Single(game => game.IconName == "Arknights").RootPath,
            detectBaseChannel: false,
            out var changed);

        Assert.False(changed);
        Assert.Equal(SharedRootMode.Independent, resolution.Mode);
        Assert.Empty(config.SharedRoots);
    }

    [Fact]
    public void OneGame_CanPersistMultipleSharedRoots()
    {
        using var temp = new TemporaryDirectory();
        var rootA = temp.CreateDirectory("Endfield-A");
        var rootB = temp.CreateDirectory("Endfield-B");
        var config = new AppConfig();
        config.Games.Single(game => game.IconName == "Endfield").RootPath = rootA;
        config.Games.Single(game => game.IconName == "BiliEndfield").RootPath = rootA;
        config.Games.Single(game => game.IconName == "GlobalEndfield").RootPath = rootB;
        config.Games.Single(game => game.IconName == "PlayEndfield").RootPath = rootB;

        _ = SharedRootManager.Resolve(
            config, "Endfield", rootA, false, out _);
        _ = SharedRootManager.Resolve(
            config, "GlobalEndfield", rootB, false, out _);

        Assert.Equal(2, config.SharedRoots.Count);
        Assert.All(config.SharedRoots,
            state => Assert.Equal("Endfield", state.GameId));
        Assert.NotEqual(
            SharedRootManager.GetSharedRootId("Endfield", rootA),
            SharedRootManager.GetSharedRootId("Endfield", rootB));
    }

    [Fact]
    public void IncompatibleChannelsOnSamePhysicalRoot_AreReportedAsConflict()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Arknights");
        var config = new AppConfig();
        config.Games.Single(game => game.IconName == "Arknights").RootPath = root;
        config.Games.Add(new GameEntry
        {
            Name = "Global",
            IconName = "ArknightsGlobal",
            RootPath = root,
            AddedManually = true
        });

        var resolution = SharedRootManager.Resolve(
            config, "Arknights", root, false, out _);

        Assert.Equal(SharedRootMode.Conflict, resolution.Mode);
        Assert.Empty(config.SharedRoots);
    }

    [Fact]
    public void ExistingBaseChannel_IsResolvedByGameAndChannel()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Arknights");
        var config = new AppConfig();
        config.Games.Single(game => game.IconName == "Arknights").RootPath = root;
        config.Games.Single(game => game.IconName == "BiliArknights").RootPath = root;
        config.SharedRoots.Add(new SharedRootState
        {
            GameId = "Arknights",
            RootPath = root,
            BaseChannel = "Bilibili"
        });

        var resolution = SharedRootManager.Resolve(
            config, "Arknights", root, false, out var changed);

        Assert.False(changed);
        Assert.Equal("BiliArknights", resolution.Base.IconName);
        Assert.Equal("Bilibili", resolution.State.BaseChannel);
    }

    [Fact]
    public void PersistedBaseOutsideCompatibilityGroup_IsNotTrusted()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Arknights");
        var config = new AppConfig();
        config.Games.Single(game => game.IconName == "Arknights").RootPath = root;
        config.Games.Single(game => game.IconName == "BiliArknights").RootPath = root;
        config.SharedRoots.Add(new SharedRootState
        {
            GameId = "Arknights",
            RootPath = root,
            BaseChannel = "Global"
        });

        var resolution = SharedRootManager.Resolve(
            config, "Arknights", root, false, out _);

        Assert.Equal(SharedRootMode.Shared, resolution.Mode);
        Assert.Null(resolution.Base);
    }

    [Fact]
    public void RuntimePath_IsDeterministicSiblingStorageOnSameVolume()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Games/Arknights");

        var first = LinkedRuntimeService.GetRuntimePath(
            "Arknights", root, "Official");
        var second = LinkedRuntimeService.GetRuntimePath(
            "Arknights", Path.Combine(root, "."), "Official");

        Assert.Equal(first, second, ignoreCase: true);
        Assert.Equal(Path.GetPathRoot(root), Path.GetPathRoot(first),
            ignoreCase: true);
        Assert.Contains(".xel-linked-runtime", first,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(first.StartsWith(
            SharedRootManager.NormalizeRootPath(root) +
            Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Arknights", "Official")]
    [InlineData("BiliArknights", "Bilibili")]
    [InlineData("Endfield", "Official")]
    [InlineData("BiliEndfield", "Bilibili")]
    [InlineData("GlobalEndfield", "Global")]
    [InlineData("PlayEndfield", "GooglePlay")]
    public void ExistingClientSignature_DetectsSupportedBaseChannel(
        string iconName,
        string expectedChannel)
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory(iconName);
        var definition = GameChannelCatalog.Get(iconName);
        Assert.NotNull(definition);
        Assert.NotNull(definition.PayloadProfile);

        foreach (var relativePath in definition.PayloadProfile.RequiredFiles)
        {
            temp.WriteFile(
                $"{iconName}/{relativePath.Replace('\\', '/')}",
                iconName);
        }

        Assert.Equal(
            expectedChannel,
            SharedRootManager.DetectBaseChannel(
                definition.GameId,
                definition.ClientCompatibilityGroup,
                root));
    }

    [Fact]
    public void PartialChannelPayload_IsNotAcceptedAsBaseChannel()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("Endfield");
        var definition = GameChannelCatalog.Get("PlayEndfield");
        Assert.NotNull(definition);
        Assert.NotNull(definition.PayloadProfile);

        // Simulate a process crash after a distinctive target SDK was moved,
        // but before the complete Google Play payload was committed.
        temp.WriteFile("Endfield/play_pc_sdk.dll", "partial switch");

        Assert.Equal(
            SharedRootManager.UnknownChannel,
            SharedRootManager.DetectBaseChannel(
                definition.GameId,
                definition.ClientCompatibilityGroup,
                root));
    }

    [Fact]
    public void LegacyConfigWithoutSharedRoots_UsesSafeDefaults()
    {
        var config = JsonSerializer.Deserialize<AppConfig>(
            "{\"Games\":[]}");

        Assert.NotNull(config);
        Assert.NotNull(config.SharedRoots);
        Assert.Empty(config.SharedRoots);
    }
}
