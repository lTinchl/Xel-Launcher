using System.Net;
using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class PluginDnsResolverTests
{
    [Fact]
    public void HypergryphCdnAddresses_RotateFirstCandidateAcrossConnections()
    {
        var hostname = $"{Guid.NewGuid():N}.hg-cdn.com";
        var addresses = new[]
        {
            IPAddress.Parse("192.0.2.1"),
            IPAddress.Parse("192.0.2.2"),
            IPAddress.Parse("192.0.2.3"),
            IPAddress.Parse("192.0.2.4")
        };

        var first = PluginDnsResolver.OrderAddresses(hostname, addresses);
        var second = PluginDnsResolver.OrderAddresses(hostname, addresses);

        Assert.Equal(addresses[1], first[0]);
        Assert.Equal(addresses[2], second[0]);
        Assert.Equal(addresses.OrderBy(item => item.ToString()),
            first.OrderBy(item => item.ToString()));
        Assert.Equal(addresses.OrderBy(item => item.ToString()),
            second.OrderBy(item => item.ToString()));
    }

    [Fact]
    public void NonHypergryphHosts_PreserveResolverOrder()
    {
        var addresses = new[]
        {
            IPAddress.Parse("192.0.2.10"),
            IPAddress.Parse("192.0.2.11")
        };

        var ordered = PluginDnsResolver.OrderAddresses(
            "example.com", addresses);

        Assert.Equal(addresses, ordered);
    }
}
