using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyEagles.Tests.Integration.Health;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Health_Returns_200_With_Status_Version_And_Uptime()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
        Assert.True(body.UptimeSeconds >= 0);
    }

    private sealed record HealthResponseDto(string Status, string Version, long UptimeSeconds);
}
