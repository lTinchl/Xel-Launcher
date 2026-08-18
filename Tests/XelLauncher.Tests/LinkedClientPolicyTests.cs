using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.Tests;

public sealed class LinkedClientPolicyTests
{
    [Fact]
    public void AreSamePath_NormalizesCaseSeparatorsAndDotSegments()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.CreateDirectory("client");
        var equivalent = Path.Combine(path, ".") + Path.DirectorySeparatorChar;

        Assert.True(LinkedClientPolicy.AreSamePath(
            path.ToUpperInvariant(), equivalent));
        Assert.False(LinkedClientPolicy.AreSamePath(path, temp.GetPath("other")));
        Assert.False(LinkedClientPolicy.AreSamePath(path, null));
    }

    [Fact]
    public void UpdatePath_RejectsMutationWhileClientIsShared()
    {
        using var temp = new TemporaryDirectory();
        var config = new AppConfig();
        var entry = config.Games.Single(x => x.IconName == "Arknights");
        entry.RootPath = temp.CreateDirectory("official");
        entry.LinkedClientGroupId = "test-group";

        Assert.Throws<InvalidOperationException>(() =>
            LinkedClientPolicy.UpdatePath(
                config, entry, temp.CreateDirectory("replacement")));
        Assert.EndsWith("official", entry.RootPath);
    }

    [Fact]
    public void UpdatePath_ForIndependentUnsharedClient_ResetsLegacyState()
    {
        using var temp = new TemporaryDirectory();
        var config = new AppConfig();
        var entry = config.Games.Single(x => x.IconName == "Arknights");
        entry.RootPath = temp.CreateDirectory("official");
        entry.IndependentChannelClient = true;
        var replacement = temp.CreateDirectory("replacement");

        LinkedClientPolicy.UpdatePath(config, entry, replacement);

        Assert.True(LinkedClientPolicy.AreSamePath(replacement, entry.RootPath));
        Assert.False(entry.IndependentChannelClient);
        Assert.Equal(string.Empty, entry.LinkedClientGroupId);
    }

    [Fact]
    public void ServerPayloadSwitch_IsSkippedForSharedClient()
    {
        using var temp = new TemporaryDirectory();
        var config = new AppConfig();
        var entry = config.Games.Single(x => x.IconName == "BiliArknights");
        entry.RootPath = temp.CreateDirectory("bilibili");
        entry.LinkedClientGroupId = "test-group";

        Assert.True(LinkedClientPolicy.ShouldSkipServerPayloadSwitch(config, entry));
    }

    [Fact]
    public void ServerPayloadSwitch_IsNotSkippedForUnrelatedChannel()
    {
        var config = new AppConfig();
        var entry = config.Games.Single(x => x.IconName == "Endfield");
        entry.IndependentChannelClient = true;

        Assert.False(LinkedClientPolicy.ShouldSkipServerPayloadSwitch(config, entry));
    }
}
