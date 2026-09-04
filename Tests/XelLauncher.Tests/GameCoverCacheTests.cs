using System.Reflection;
using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class GameCoverCacheTests
{
    [Theory]
    [InlineData("Arknights", "BiliArknights")]
    [InlineData("Endfield", "BiliEndfield")]
    [InlineData("GlobalEndfield", "PlayEndfield")]
    public void CacheDirectory_IsSeparatedPerChannel(string firstIconName, string secondIconName)
    {
        var method = typeof(GameCoverCache).GetMethod(
            "GetGameCoverDir",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var firstPath = Assert.IsType<string>(method.Invoke(null, [firstIconName]));
        var secondPath = Assert.IsType<string>(method.Invoke(null, [secondIconName]));

        Assert.False(string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(firstIconName, Path.GetFileName(firstPath));
        Assert.Equal(secondIconName, Path.GetFileName(secondPath));
    }
}
