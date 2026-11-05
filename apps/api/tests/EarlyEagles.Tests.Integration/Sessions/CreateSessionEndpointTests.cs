using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EarlyEagles.Api.Sessions;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Authentication;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-006 — a tenant admin posts a session on their club's subdomain and a row
// appears in that club, audited. Uses its own tenants (Eta, Theta) so it doesn't
// disturb the shared Alpha/Beta fixtures. Eta is the only club with the sessions
// feature on in Production; Theta shows it staying hidden for everyone else.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CreateSessionEndpointTests : IAsyncLifetime
{
    private const string EtaSlug = "eta-sessions";
    private const string ThetaSlug = "theta-sessions";
    private const string EtaOrg = "org_eta_sessions";
    private const string ThetaOrg = "org_theta_sessions";

    private static readonly Guid EtaTenantId = new("aaaaaaaa-1006-0000-0000-000000000001");
    private static readonly Guid ThetaTenantId = new("bbbbbbbb-1006-0000-0000-000000000001");

    private readonly PostgresFixture fixture;

    public CreateSessionEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = fixture.CreateContext(new TenantContext());
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == EtaTenantId))
        {
            var eta = Tenant.Create(EtaTenantId, EtaSlug, "Eta Club");
            eta.LinkClerkOrganization(EtaOrg);
            var theta = Tenant.Create(ThetaTenantId, ThetaSlug, "Theta Club");
            theta.LinkClerkOrganization(ThetaOrg);
            db.Tenants.AddRange(eta, theta);
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Creates_A_Session_In_Their_Club(string environment)
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString, environment);

        var response = await ClientFor(factory, EtaSlug).PostAsJsonAsync(
            SessionEndpoints.Route,
            new { date = "2026-11-08", startTime = "07:15", venue, feeCents = 2000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreateSessionResponse>())!.Id;
        Assert.Equal($"/sessions/{id}", response.Headers.Location?.ToString());

        await using var db = fixture.CreateContext(new TenantContext());
        var row = await db.Sessions.IgnoreQueryFilters().SingleAsync(s => s.Venue == venue);
        Assert.Equal(id, row.Id);
        Assert.Equal(EtaTenantId, row.TenantId);
        Assert.Equal(new DateOnly(2026, 11, 8), row.Date);
        Assert.Equal(new TimeOnly(7, 15), row.StartTime);
        Assert.Equal(2000, row.FeeCents);
        Assert.Equal(SessionStatus.Scheduled, row.Status);
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == EtaTenantId && e.Action == "CreateSessionCommand" && e.Success));
    }

    [Fact]
    public async Task Without_A_Fee_The_Session_Costs_The_Default()
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString);

        await ClientFor(factory, EtaSlug).PostAsJsonAsync(SessionEndpoints.Route, new { date = "2026-11-15", startTime = "07:00", venue });

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(Session.DefaultFeeCents, (await db.Sessions.IgnoreQueryFilters().SingleAsync(s => s.Venue == venue)).FeeCents);
    }

    [Fact]
    public async Task A_Free_Session_Stays_Free()
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString);

        await ClientFor(factory, EtaSlug).PostAsJsonAsync(SessionEndpoints.Route, new { date = "2026-11-22", startTime = "07:00", venue, feeCents = 0 });

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(0, (await db.Sessions.IgnoreQueryFilters().SingleAsync(s => s.Venue == venue)).FeeCents);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var factory = new SessionFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{EtaSlug}.earlyeagles.club";

        var response = await client.PostAsJsonAsync(SessionEndpoints.Route, Body(NewVenue()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403_And_Stores_Nothing()
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, EtaSlug, role: "member").PostAsJsonAsync(SessionEndpoints.Route, Body(venue));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNotStored(venue);
    }

    [Fact]
    public async Task An_Admin_Of_Another_Club_Gets_403_And_Stores_Nothing()
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, EtaSlug, organization: ThetaOrg).PostAsJsonAsync(SessionEndpoints.Route, Body(venue));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNotStored(venue);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404_And_Stores_Nothing()
    {
        var venue = NewVenue();
        using var factory = new SessionFactory(fixture.ConnectionString, "Production");

        var response = await ClientFor(factory, ThetaSlug).PostAsJsonAsync(SessionEndpoints.Route, Body(venue));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotStored(venue);
    }

    [Theory]
    [InlineData(null, "07:00", "Hall", 1500, "Date")]
    [InlineData("2026-11-08", null, "Hall", 1500, "StartTime")]
    [InlineData("2026-11-08", "07:00", null, 1500, "Venue")]
    [InlineData("2026-11-08", "07:00", "   ", 1500, "Venue")]
    [InlineData("2026-11-08", "07:00", "Hall", -1, "FeeCents")]
    [InlineData("2026-11-08", "07:00", "Hall", 100_001, "FeeCents")]
    public async Task An_Invalid_Session_Returns_400_Naming_The_Field_And_Stores_Nothing(
        string? date, string? startTime, string? venue, int feeCents, string field)
    {
        using var factory = new SessionFactory(fixture.ConnectionString);
        var before = await CountEtaSessions();

        var response = await ClientFor(factory, EtaSlug).PostAsJsonAsync(SessionEndpoints.Route, new { date, startTime, venue, feeCents });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(before, await CountEtaSessions());
    }

    [Fact]
    public async Task A_Venue_Too_Long_Returns_400()
    {
        using var factory = new SessionFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, EtaSlug).PostAsJsonAsync(
            SessionEndpoints.Route,
            new { date = "2026-11-08", startTime = "07:00", venue = new string('x', Session.VenueMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Development", """{"date":"next sunday","startTime":"07:00","venue":"Hall"}""")]
    [InlineData("Production", """{"date":"next sunday","startTime":"07:00","venue":"Hall"}""")]
    [InlineData("Development", """{"date":"2026-02-30","startTime":"07:00","venue":"Hall"}""")]
    [InlineData("Development", """{"date":"2026-11-08","startTime":"25:00","venue":"Hall"}""")]
    [InlineData("Development", """{"date":"2026-11-08","startTime":"07:00","venue":"Hall","feeCents":"lots"}""")]
    [InlineData("Development", "not json")]
    public async Task A_Body_That_Does_Not_Parse_Returns_400_In_Every_Environment(string environment, string json)
    {
        using var factory = new SessionFactory(fixture.ConnectionString, environment);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await ClientFor(factory, EtaSlug).PostAsync(new Uri(SessionEndpoints.Route, UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string NewVenue() => "Hall " + Guid.NewGuid().ToString("N");

    private static object Body(string venue) => new { date = "2026-11-08", startTime = "07:00", venue };

    private static HttpClient ClientFor(SessionFactory factory, string slug, string? organization = null, string role = "admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";
        var token = ClerkTestTokens.Create(
            "user_sessions_admin",
            "sess_sessions_admin",
            extra: ClerkTestTokens.ActiveOrganization(organization ?? (slug == EtaSlug ? EtaOrg : ThetaOrg), role));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task AssertNotStored(string venue)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.Sessions.IgnoreQueryFilters().AnyAsync(s => s.Venue == venue));
    }

    private async Task<int> CountEtaSessions()
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.Sessions.IgnoreQueryFilters().CountAsync(s => s.TenantId == EtaTenantId);
    }

    private sealed class SessionFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string environment;

        public SessionFactory(string connectionString, string environment = "Development")
        {
            this.connectionString = connectionString;
            this.environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("FeatureFlags:Features:sessions:Tenants:0", EtaSlug);
            ClerkTestTokens.UseTestIssuer(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
