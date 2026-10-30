using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EarlyEagles.Domain.Auditing;
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

namespace EarlyEagles.Tests.Integration.Auditing;

// P0-048.1 — GET /audit-events against the real query filter and keyset
// paging. Uses its own two tenants (Gamma, Delta) so the rows it seeds don't
// disturb the shared Alpha/Beta fixtures other tests count against.
// P0-048.2 — tenant admins only, in every environment: requests carry a signed
// Clerk session token whose active organization is the tenant's, and the
// tenants resolve through the real EfTenantLookup.
[Collection(PostgresCollectionDefinition.Name)]
public class AuditEventsEndpointTests : IAsyncLifetime
{
    private const string GammaSlug = "gamma-audit";
    private const string DeltaSlug = "delta-audit";
    private const string GammaOrg = "org_gamma_audit";
    private const string DeltaOrg = "org_delta_audit";

    private static readonly Guid GammaTenantId = new("cccccccc-0048-0000-0000-000000000001");
    private static readonly Guid DeltaTenantId = new("dddddddd-0048-0000-0000-000000000001");
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture fixture;

    // Gamma's rows, newest first, as the endpoint must return them.
    private readonly List<Guid> gammaExpectedOrder = [];

    public AuditEventsEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = fixture.CreateContext(new TenantContext());

        // IgnoreQueryFilters: with no tenant resolved the filter hides every
        // row, so a plain lookup would never see an earlier test's seed.
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == GammaTenantId))
        {
            var gammaTenant = Tenant.Create(GammaTenantId, GammaSlug, "Gamma Club");
            gammaTenant.LinkClerkOrganization(GammaOrg);
            var deltaTenant = Tenant.Create(DeltaTenantId, DeltaSlug, "Delta Club");
            deltaTenant.LinkClerkOrganization(DeltaOrg);
            db.Tenants.AddRange(gammaTenant, deltaTenant);

            // Gamma: seven events, three of them sharing one timestamp so the
            // id tie-break is exercised across a page boundary.
            var gamma = new List<AuditEvent>();
            for (var i = 0; i < 4; i++)
            {
                gamma.Add(AuditEvent.Create(Guid.NewGuid(), GammaTenantId, $"GammaAction{i}", true, null, "user_gamma", T0.AddMinutes(-i)));
            }

            for (var i = 0; i < 3; i++)
            {
                gamma.Add(AuditEvent.Create(Guid.NewGuid(), GammaTenantId, $"GammaTied{i}", false, "boom", null, T0.AddMinutes(-10)));
            }

            db.AuditEvents.AddRange(gamma);
            db.AuditEvents.AddRange(
                Enumerable.Range(0, 3).Select(i =>
                    AuditEvent.Create(Guid.NewGuid(), DeltaTenantId, $"DeltaAction{i}", true, null, "user_delta", T0.AddMinutes(-i))));
            await db.SaveChangesAsync();
        }

        // Expected order computed by Postgres itself: uuid ordering differs
        // from .NET's Guid ordering, and the tie-break must follow the database.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM audit_events WHERE tenant_id = @t ORDER BY occurred_at DESC, id DESC", connection);
        command.Parameters.AddWithValue("t", GammaTenantId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            gammaExpectedOrder.Add(reader.GetGuid(0));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Pages_Through_The_Whole_Log_Newest_First_Without_Gaps_Or_Duplicates()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);
        var client = ClientFor(factory, GammaSlug);

        var seen = new List<Guid>();
        var pageSizes = new List<int>();
        string? cursor = null;
        do
        {
            var url = cursor is null ? "/audit-events?limit=3" : $"/audit-events?limit=3&cursor={cursor}";
            var page = await client.GetFromJsonAsync<PageDto>(new Uri(url, UriKind.Relative));
            Assert.NotNull(page);
            pageSizes.Add(page!.Items.Count);
            seen.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal([3, 3, 1], pageSizes);
        Assert.Equal(gammaExpectedOrder, seen);
    }

    [Fact]
    public async Task Returns_Only_The_Callers_Tenant()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);

        var delta = await ClientFor(factory, DeltaSlug).GetFromJsonAsync<PageDto>(new Uri("/audit-events", UriKind.Relative));

        Assert.NotNull(delta);
        Assert.Equal(3, delta!.Items.Count);
        Assert.All(delta.Items, i => Assert.StartsWith("DeltaAction", i.Action, StringComparison.Ordinal));
        Assert.Null(delta.NextCursor);
    }

    [Fact]
    public async Task Another_Tenants_Cursor_Still_Pages_The_Callers_Own_Log()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);
        var gammaPage = await ClientFor(factory, GammaSlug).GetFromJsonAsync<PageDto>(new Uri("/audit-events?limit=1", UriKind.Relative));

        var delta = await ClientFor(factory, DeltaSlug)
            .GetFromJsonAsync<PageDto>(new Uri($"/audit-events?cursor={gammaPage!.NextCursor}", UriKind.Relative));

        Assert.NotNull(delta);
        Assert.All(delta!.Items, i => Assert.StartsWith("DeltaAction", i.Action, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/audit-events?limit=0")]
    [InlineData("/audit-events?limit=101")]
    [InlineData("/audit-events?cursor=not-a-cursor")]
    public async Task Invalid_Paging_Input_Returns_400(string url)
    {
        using var factory = new AuditFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, GammaSlug).GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{GammaSlug}.earlyeagles.club";

        var response = await client.GetAsync(new Uri("/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, GammaSlug, GammaOrg, "member").GetAsync(new Uri("/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_Admin_Of_Another_Club_Gets_403()
    {
        using var factory = new AuditFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, GammaSlug, DeltaOrg).GetAsync(new Uri("/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Tenant_Even_A_Signed_In_Admin_Gets_403()
    {
        // Bare host: no tenant, so no tenant role, whatever the token says.
        using var factory = new AuditFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(GammaOrg, "admin");

        var response = await client.GetAsync(new Uri("/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task In_Production_A_Tenant_Admin_Reads_The_Log()
    {
        using var factory = new AuditFactory(fixture.ConnectionString, environment: "Production");

        var page = await ClientFor(factory, DeltaSlug, DeltaOrg).GetFromJsonAsync<PageDto>(new Uri("/audit-events", UriKind.Relative));

        Assert.NotNull(page);
        Assert.Equal(3, page!.Items.Count);
    }

    // A client on the slug's subdomain, signed in with an organization active
    // (default: the slug's own, as admin).
    private static HttpClient ClientFor(AuditFactory factory, string slug, string? organization = null, string role = "admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";
        client.DefaultRequestHeaders.Authorization = Bearer(organization ?? (slug == GammaSlug ? GammaOrg : DeltaOrg), role);
        return client;
    }

    private static AuthenticationHeaderValue Bearer(string organization, string role) =>
        new("Bearer", ClerkTestTokens.Create("user_audit_reader", "sess_audit_reader", extra: ClerkTestTokens.ActiveOrganization(organization, role)));

    private sealed record PageDto(IReadOnlyList<ItemDto> Items, string? NextCursor);

    private sealed record ItemDto(Guid Id, string Action, bool Success, string? FailureReason, string? ActorClerkUserId, DateTimeOffset OccurredAt);

    private sealed class AuditFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string environment;

        public AuditFactory(string connectionString, string environment = "Development")
        {
            this.connectionString = connectionString;
            this.environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            ClerkTestTokens.UseTestIssuer(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
