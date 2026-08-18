using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class OperationCoordinatorTests
{
    [Fact]
    public void SamePath_CannotBeAcquiredUntilLeaseIsReleased()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.CreateDirectory("client");

        Assert.True(LinkedClientOperationCoordinator.TryAcquirePaths(
            new[] { path }, out var firstLease));
        try
        {
            Assert.False(LinkedClientOperationCoordinator.TryAcquirePaths(
                new[] { Path.Combine(path, ".") }, out var blockedLease));
            Assert.Null(blockedLease);
        }
        finally
        {
            firstLease.Dispose();
        }

        Assert.True(LinkedClientOperationCoordinator.TryAcquirePaths(
            new[] { path }, out var secondLease));
        secondLease.Dispose();
    }

    [Fact]
    public void OverlappingPathSets_AreMutuallyExclusive()
    {
        using var temp = new TemporaryDirectory();
        var official = temp.CreateDirectory("official");
        var bilibili = temp.CreateDirectory("bilibili");

        Assert.True(LinkedClientOperationCoordinator.TryAcquirePaths(
            new[] { official, bilibili }, out var pairLease));
        try
        {
            Assert.False(LinkedClientOperationCoordinator.TryAcquirePaths(
                new[] { bilibili }, out var blockedLease));
            Assert.Null(blockedLease);
        }
        finally
        {
            pairLease.Dispose();
        }
    }
}
