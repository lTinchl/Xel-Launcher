using System.Net.Http;
using System.Reflection;
using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class SkylandServiceTests
{
    [Fact]
    public void SignedRequestContainsSingleDeviceIdHeader()
    {
        var method = typeof(SkylandService).GetMethod(
            "CreateSignedRequest",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var session = new SkylandSession("cred", "sign-token", "B-device-id");
        using var request = Assert.IsType<HttpRequestMessage>(method.Invoke(
            new SkylandService(),
            new object[]
            {
                HttpMethod.Get,
                "https://zonai.skland.com/api/v1/game/player/binding",
                null,
                session
            }));

        Assert.True(request.Headers.TryGetValues("dId", out var values));
        Assert.Equal(new[] { session.DeviceId }, values);
    }
}
