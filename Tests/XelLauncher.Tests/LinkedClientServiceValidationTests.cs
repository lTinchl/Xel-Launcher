using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class LinkedClientServiceValidationTests
{
    [Fact]
    public async Task Create_RejectsIncompleteOfficialClient()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("official");
        var target = temp.GetPath("bilibili");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            ArknightsLinkedClientService.CreateBilibiliClientAsync(
                source, target));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task Create_RejectsTargetNestedInsideOfficialClient()
    {
        using var temp = new TemporaryDirectory();
        var source = CreateMinimalOfficialClient(temp);
        var target = Path.Combine(source, "nested-bilibili");

        await Assert.ThrowsAsync<IOException>(() =>
            ArknightsLinkedClientService.CreateBilibiliClientAsync(
                source, target));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task Create_RejectsNonEmptyTargetWithoutChangingIt()
    {
        using var temp = new TemporaryDirectory();
        var source = CreateMinimalOfficialClient(temp);
        var sentinel = temp.WriteFile("bilibili/keep.txt", "keep me");

        await Assert.ThrowsAsync<IOException>(() =>
            ArknightsLinkedClientService.CreateBilibiliClientAsync(
                source, Path.GetDirectoryName(sentinel)!));

        Assert.Equal("keep me", File.ReadAllText(sentinel));
    }

    [Fact]
    public async Task Detach_RejectsMissingClientWithoutCreatingDirectory()
    {
        using var temp = new TemporaryDirectory();
        var missing = temp.GetPath("missing-client");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            ArknightsLinkedClientService.DetachSharedFilesAsync(missing));

        Assert.False(Directory.Exists(missing));
    }

    private static string CreateMinimalOfficialClient(TemporaryDirectory temp)
    {
        var source = temp.CreateDirectory("official");
        temp.WriteFile("official/Arknights.exe", "test executable placeholder");
        temp.WriteFile("official/config.ini", "test config");
        temp.CreateDirectory("official/Arknights_Data");
        return source;
    }
}
