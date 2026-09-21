using EarlyEagles.Infrastructure.Outbox;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Outbox;

// P0-050.1 — the acceptance criterion is literally "service registered +
// running in app". A drain that exists but was never added to the host is the
// exact failure this pins: everything still compiles, every other test passes,
// and no event is ever delivered.
public class OutboxProcessorRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public OutboxProcessorRegistrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void The_Api_Host_Runs_The_Outbox_Processor()
    {
        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is OutboxProcessor);
    }

    [Fact]
    public void The_Outbox_Options_Bind_With_Working_Defaults()
    {
        // Bound and validated at startup (ValidateOnStart), so a bad interval
        // fails the deploy rather than surfacing ten seconds in as a background
        // exception nobody is watching.
        var options = factory.Services.GetRequiredService<IOptions<OutboxOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.True(options.PollInterval > TimeSpan.Zero);
        Assert.InRange(options.BatchSize, 1, OutboxOptions.MaxBatchSize);
    }
}
