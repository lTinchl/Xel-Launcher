using XelLauncher.Helpers;
using System.Text.Json;

namespace XelLauncher.Tests;

public sealed class LinkedRuntimePlanTests
{
    [Fact]
    public void ArknightsPlan_LinksOnlyImmutableCommonData()
    {
        var common = File("Arknights_Data/resources/common.bundle", 100, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var cache = File("Arknights_Data/Cache/cache.bin", 20, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var config = File("Arknights_Data/config/settings.dat", 10, "cccccccccccccccccccccccccccccccc");
        var sdk = File("U8SDK.dll", 30, "dddddddddddddddddddddddddddddddd");
        var changedBase = File("Arknights_Data/resources/channel.bundle", 40, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        var changedTarget = File("Arknights_Data/resources/channel.bundle", 40, "ffffffffffffffffffffffffffffffff");

        var plan = LinkedRuntimeService.BuildPlan(
            GameChannelCatalog.Get("BiliArknights"),
            Manifest("Arknights", common, cache, config, sdk, changedBase),
            Manifest("BiliArknights", common, cache, config, sdk, changedTarget));

        Assert.Equal(LinkedRuntimeStorageKind.HardLink,
            Find(plan, common.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, cache.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, config.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, sdk.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, changedTarget.RelativePath).StorageKind);
    }

    [Fact]
    public void EndfieldPlan_KeepsAllServerPayloadPathsIndependent()
    {
        var common = File("Endfield_Data/StreamingAssets/bundle.bin", 100, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var database = File("eld_Endfield.db", 10, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var u8Config = File("U8Data/config/config.bin", 10, "cccccccccccccccccccccccccccccccc");

        var plan = LinkedRuntimeService.BuildPlan(
            GameChannelCatalog.Get("GlobalEndfield"),
            Manifest("Endfield", common, database, u8Config),
            Manifest("GlobalEndfield", common, database, u8Config));

        Assert.Equal(LinkedRuntimeStorageKind.HardLink,
            Find(plan, common.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, database.RelativePath).StorageKind);
        Assert.Equal(LinkedRuntimeStorageKind.Independent,
            Find(plan, u8Config.RelativePath).StorageKind);
        Assert.True(ServerPayloadDeployment.IsManagedPath(
            GameFamily.Endfield, u8Config.RelativePath));
    }

    [Fact]
    public void CompatibilityAndRuntimeCapability_AreSeparateQueries()
    {
        var endfieldChannels = new[]
        {
            "Endfield", "BiliEndfield", "GlobalEndfield", "PlayEndfield"
        };
        foreach (var source in endfieldChannels)
        foreach (var target in endfieldChannels)
        {
            Assert.True(GameChannelCatalog.CanSwitchChannel(source, target));
            Assert.Equal(
                !string.Equals(source, target,
                    StringComparison.OrdinalIgnoreCase),
                GameChannelCatalog.CanCreateLinkedRuntime(source, target));
        }

        Assert.True(GameChannelCatalog.CanSwitchChannel(
            "Arknights", "BiliArknights"));
        Assert.False(GameChannelCatalog.CanSwitchChannel(
            "Arknights", "ArknightsGlobal"));
        Assert.False(GameChannelCatalog.CanCreateLinkedRuntime(
            "Arknights", "ArknightsJp"));
    }

    [Theory]
    [InlineData("Arknights_Data/Cache/cache.bin")]
    [InlineData("Arknights_Data/Logs/latest.bin")]
    [InlineData("Arknights_Data/Temp/generated.bin")]
    [InlineData("Arknights_Data/Saves/slot.bin")]
    [InlineData("Arknights_Data/data/runtime.sqlite")]
    [InlineData("Arknights_Data/data/client.log")]
    [InlineData("Arknights_Data/data/account.bin")]
    [InlineData("Arknights_Data/data/settings.json")]
    [InlineData("Endfield_Data/app.info")]
    public void MutableRuntimeFiles_AreNeverHardLinkCandidates(
        string relativePath)
    {
        var iconName = relativePath.StartsWith(
            "Endfield_Data/", StringComparison.OrdinalIgnoreCase)
            ? "Endfield"
            : "Arknights";
        Assert.False(LinkedRuntimeService.IsSafeSharedPath(
            GameChannelCatalog.Get(iconName), relativePath));
    }

    [Fact]
    public void ImmutableDataBundle_IsAHardLinkCandidate()
    {
        Assert.True(LinkedRuntimeService.IsSafeSharedPath(
            GameChannelCatalog.Get("Arknights"),
            "Arknights_Data/StreamingAssets/content.bundle"));
    }

    [Fact]
    public void IndependentSource_WithWrongSize_IsNotReusable()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "xel-linked-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "libcrypto-1_1-x64.dll");

        try
        {
            System.IO.File.WriteAllBytes(
                sourcePath, new byte[] { 1, 2, 3 });
            var manifestFile = File(
                "libcrypto-1_1-x64.dll",
                size: 4,
                md5: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.False(LinkedRuntimeService.IsUsableManifestSource(
                sourcePath, manifestFile));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void IndependentSource_WithExpectedSize_IsReusable()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "xel-linked-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "channel-file.dll");

        try
        {
            System.IO.File.WriteAllBytes(
                sourcePath, new byte[] { 1, 2, 3 });
            var manifestFile = File(
                "channel-file.dll",
                size: 3,
                md5: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.True(LinkedRuntimeService.IsUsableManifestSource(
                sourcePath, manifestFile));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void HardLinkCandidate_WithInvalidSource_DowngradesToIndependent()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "xel-linked-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var relativePath = "Endfield_Data/StreamingAssets/common.bundle";
        var sourcePath = Path.Combine(
            tempDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

        try
        {
            System.IO.File.WriteAllBytes(
                sourcePath, new byte[] { 1, 2, 3 });
            var manifestFile = File(
                relativePath,
                size: 4,
                md5: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var planned = new LinkedRuntimePlanEntry
            {
                BaseFile = manifestFile,
                TargetFile = manifestFile,
                StorageKind = LinkedRuntimeStorageKind.HardLink
            };

            Assert.Equal(
                LinkedRuntimeStorageKind.Independent,
                LinkedRuntimeService.ResolveStorageKindForSource(
                    tempDirectory, planned));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ArknightsPersistentResources_UseBothInternalManifests()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "xel-linked-runtime-tests", Guid.NewGuid().ToString("N"));
        var bundlesDirectory = Path.Combine(
            tempDirectory, "Arknights_Data", "PersistentData", "Bundles");
        Directory.CreateDirectory(Path.Combine(bundlesDirectory, "audio"));
        var resourcePath = Path.Combine(
            bundlesDirectory, "audio", "shared.ab");

        try
        {
            System.IO.File.WriteAllBytes(
                resourcePath, new byte[] { 1, 2, 3 });
            var entry = new
            {
                name = "audio/shared.ab",
                md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                abSize = 3
            };
            System.IO.File.WriteAllText(
                Path.Combine(bundlesDirectory, "hot_update_list.json"),
                JsonSerializer.Serialize(new
                {
                    manifestName = "manifest.idx",
                    abInfos = new[] { entry }
                }));
            System.IO.File.WriteAllText(
                Path.Combine(bundlesDirectory, "persistent_res_list.json"),
                JsonSerializer.Serialize(new
                {
                    manifestName = "manifest.idx",
                    abInfos = new[] { entry }
                }));

            var plan = LinkedRuntimeService
                .BuildArknightsPersistentResourcePlan(
                    GameChannelCatalog.Get("Arknights"),
                    tempDirectory);

            var planned = Assert.Single(plan);
            Assert.Equal(
                "Arknights_Data/PersistentData/Bundles/audio/shared.ab",
                planned.TargetFile.RelativePath);
            Assert.Equal(LinkedRuntimeStorageKind.HardLink,
                planned.StorageKind);
            Assert.True(planned.IsAuxiliary);
            Assert.True(planned.IsLocalOnly);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ArknightsPersistentResources_RejectManifestDisagreement()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "xel-linked-runtime-tests", Guid.NewGuid().ToString("N"));
        var bundlesDirectory = Path.Combine(
            tempDirectory, "Arknights_Data", "PersistentData", "Bundles");
        Directory.CreateDirectory(Path.Combine(bundlesDirectory, "audio"));

        try
        {
            System.IO.File.WriteAllBytes(
                Path.Combine(bundlesDirectory, "audio", "shared.ab"),
                new byte[] { 1, 2, 3 });
            System.IO.File.WriteAllText(
                Path.Combine(bundlesDirectory, "hot_update_list.json"),
                JsonSerializer.Serialize(new
                {
                    abInfos = new[]
                    {
                        new
                        {
                            name = "audio/shared.ab",
                            md5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            abSize = 3
                        }
                    }
                }));
            System.IO.File.WriteAllText(
                Path.Combine(bundlesDirectory, "persistent_res_list.json"),
                JsonSerializer.Serialize(new
                {
                    abInfos = new[]
                    {
                        new
                        {
                            name = "audio/shared.ab",
                            md5 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                            abSize = 3
                        }
                    }
                }));

            var plan = LinkedRuntimeService
                .BuildArknightsPersistentResourcePlan(
                    GameChannelCatalog.Get("Arknights"),
                    tempDirectory);

            Assert.Empty(plan);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static LinkedRuntimePlanEntry Find(
        LinkedRuntimePlan plan,
        string relativePath) =>
        plan.Files.Single(file => string.Equals(
            file.TargetFile.RelativePath,
            relativePath,
            StringComparison.OrdinalIgnoreCase));

    private static ServerGameManifest Manifest(
        string iconName,
        params ServerGameManifestFile[] files) => new()
    {
        Profile = ServerPayloadUpdater.GetProfile(iconName),
        Version = "1.0.0",
        ManifestSha256 = iconName + "-manifest",
        EncryptedManifest = new byte[] { 1, 2, 3 },
        Files = files
    };

    private static ServerGameManifestFile File(
        string relativePath,
        long size,
        string md5) => new()
    {
        RelativePath = relativePath,
        UrlPath = relativePath.Replace('\\', '/'),
        Size = size,
        Md5 = md5
    };
}
