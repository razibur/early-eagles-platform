using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyEagles.Tests.Integration.Health;

public class ReadyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ReadyEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_Ready_With_No_Registered_Checks_Returns_200_Healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReadyResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        Assert.NotNull(body.Checks);
        Assert.Empty(body.Checks);
        Assert.True(body.TotalDurationMs >= 0);
    }

    private sealed record ReadyResponseDto(string Status, IReadOnlyList<ReadyCheckDto> Checks, long TotalDurationMs);

    private sealed record ReadyCheckDto(string Name, string Status, string? Description, long DurationMs);
}
