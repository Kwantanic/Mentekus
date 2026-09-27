using System.Net;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class HealthEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }
}
