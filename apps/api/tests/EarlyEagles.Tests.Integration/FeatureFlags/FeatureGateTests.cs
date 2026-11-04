using System.Net;
using EarlyEagles.Api.FeatureFlags;
using EarlyEagles.Application.Abstractions.FeatureFlags;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Integration.FeatureFlags;

// P1-031 — RequireFeature hides an endpoint (404) while its feature is off and
// lets it through when on. Hosted in a minimal app so the filter is tested on
// its own, without any Phase 1 endpoint.
public sealed class FeatureGateTests
{
    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task The_Gate_Follows_The_Flag(bool enabled, HttpStatusCode expected)
    {
        await using var app = await StartAsync(new FixedFlags(enabled));

        var response = await app.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task The_Gate_Asks_For_Its_Own_Feature()
    {
        var flags = new FixedFlags(true);
        await using var app = await StartAsync(flags);

        await app.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

        Assert.Equal(["test-feature"], flags.Asked);
    }

    private static async Task<WebApplication> StartAsync(FixedFlags flags)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IFeatureFlags>(flags);
        var app = builder.Build();
        app.MapGet("/gated", () => Results.Ok("on")).RequireFeature("test-feature");
        await app.StartAsync();
        return app;
    }

    private sealed class FixedFlags(bool enabled) : IFeatureFlags
    {
        public List<string> Asked { get; } = [];

        public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken)
        {
            Asked.Add(feature);
            return ValueTask.FromResult(enabled);
        }
    }
}
