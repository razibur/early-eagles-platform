using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Tests.Integration.Messaging;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Health;

// P0-026.2 — /ready flips with the database. "Toggle the DB connection" is two
// hosts here: one pointed at the Testcontainers Postgres, one at a port where
// nothing listens.
[Collection(PostgresCollectionDefinition.Name)]
public class ReadyEndpointTests
{
    // Nothing listens on port 1; the connect is refused immediately.
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=earlyeagles;Username=postgres;Password=postgres;Timeout=3";

    private readonly PostgresFixture fixture;

    public ReadyEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Get_Ready_With_Reachable_Database_Returns_200_Healthy()
    {
        using var factory = new DatabaseFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadyResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        var check = Assert.Single(body.Checks);
        Assert.Equal("postgres", check.Name);
        Assert.Equal("Healthy", check.Status);
        Assert.True(body.TotalDurationMs >= 0);
    }

    [Fact]
    public async Task Get_Ready_With_Unreachable_Database_Returns_503_Unhealthy_Within_The_Check_Timeout()
    {
        using var factory = new DatabaseFactory(UnreachableConnectionString);
        var client = factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadyResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Unhealthy", body!.Status);
        var check = Assert.Single(body.Checks);
        Assert.Equal("postgres", check.Name);
        Assert.Equal("Unhealthy", check.Status);

        // Fixed text only: /ready is unauthenticated, so no host, port or
        // exception message may reach the response.
        Assert.Equal("Database unreachable.", check.Description);
        Assert.DoesNotContain("127.0.0.1", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The registration's 5 s timeout bounds the probe.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task Get_Ready_With_A_Configured_But_Unreachable_Bus_Returns_503_On_The_Bus_Check()
    {
        // P0-026.2.2 — nothing listens on port 1, so the AMQP link never opens.
        using var factory = new DatabaseFactory(
            fixture.ConnectionString,
            "Endpoint=sb://localhost:1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
        var client = factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        var response = await client.GetAsync(new Uri("/ready", UriKind.Relative));

        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadyResponseDto>();
        Assert.NotNull(body);
        Assert.Contains(body!.Checks, c => c.Name == "postgres" && c.Status == "Healthy");
        var bus = Assert.Single(body.Checks, c => c.Name == "servicebus");
        Assert.Equal("Unhealthy", bus.Status);
        Assert.Equal("Message bus unreachable.", bus.Description);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }

    [ServiceBusEmulatorFact]
    public async Task Get_Ready_With_The_Emulator_Reports_The_Bus_Healthy()
    {
        using var factory = new DatabaseFactory(fixture.ConnectionString, ServiceBusEmulatorFactAttribute.ConnectionString);

        var response = await factory.CreateClient().GetAsync(new Uri("/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadyResponseDto>();
        Assert.Contains(body!.Checks, c => c.Name == "servicebus" && c.Status == "Healthy");
    }

    private sealed record ReadyResponseDto(string Status, IReadOnlyList<ReadyCheckDto> Checks, long TotalDurationMs);

    private sealed record ReadyCheckDto(string Name, string Status, string? Description, long DurationMs);

    // Swaps the data source the health check (and EF) resolves from DI.
    private sealed class DatabaseFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string? serviceBusConnectionString;

        public DatabaseFactory(string connectionString, string? serviceBusConnectionString = null)
        {
            this.connectionString = connectionString;
            this.serviceBusConnectionString = serviceBusConnectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (serviceBusConnectionString is not null)
            {
                builder.UseSetting("ConnectionStrings:ServiceBus", serviceBusConnectionString);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
