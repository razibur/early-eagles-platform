using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Infrastructure.Tenants;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-031.5 — the real slug → tenant lookup against Postgres, both directly and
// through the resolution middleware with nothing faked but the data source.
// Tenants here use slugs unique to this class; the fixture's database is shared.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class EfTenantLookupTests
{
    private const string ActiveSlug = "lookup-active";
    private const string SuspendedSlug = "lookup-suspended";
    private const string DeactivatedSlug = "lookup-gone";

    private static readonly Guid ActiveId = new("31531531-0000-0000-0000-000000000001");
    private static readonly Guid SuspendedId = new("31531531-0000-0000-0000-000000000002");
    private static readonly Guid DeactivatedId = new("31531531-0000-0000-0000-000000000003");

    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    private readonly PostgresFixture fixture;

    public EfTenantLookupTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task An_Active_Tenant_Resolves_By_Slug()
    {
        var lookup = await LookupAsync();

        var result = await lookup.FindBySlugAsync(ActiveSlug, CancellationToken.None);

        Assert.Equal(ActiveId, result?.TenantId);
        Assert.Equal(ActiveSlug, result?.Slug);
    }

    [Fact]
    public async Task The_Slug_Is_Matched_Case_Insensitively_And_The_Stored_Slug_Returned()
    {
        var lookup = await LookupAsync();

        var result = await lookup.FindBySlugAsync("Lookup-ACTIVE", CancellationToken.None);

        Assert.Equal(ActiveSlug, result?.Slug);
    }

    [Fact]
    public async Task A_Suspended_Tenant_Still_Resolves()
    {
        var lookup = await LookupAsync();

        var result = await lookup.FindBySlugAsync(SuspendedSlug, CancellationToken.None);

        Assert.Equal(SuspendedId, result?.TenantId);
    }

    [Fact]
    public async Task A_Deactivated_Tenant_Does_Not_Resolve()
    {
        var lookup = await LookupAsync();

        Assert.Null(await lookup.FindBySlugAsync(DeactivatedSlug, CancellationToken.None));
    }

    [Fact]
    public async Task An_Unknown_Slug_Does_Not_Resolve()
    {
        var lookup = await LookupAsync();

        Assert.Null(await lookup.FindBySlugAsync("lookup-nobody", CancellationToken.None));
    }

    [Theory]
    [InlineData(ActiveSlug, HttpStatusCode.OK)]
    [InlineData(DeactivatedSlug, HttpStatusCode.NotFound)]
    [InlineData("lookup-nobody", HttpStatusCode.NotFound)]
    public async Task In_Production_A_Subdomain_Resolves_Through_The_Database(string slug, HttpStatusCode expected)
    {
        await SeedOnceAsync();
        using var factory = new LookupFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<WhoAmIBody>();
            Assert.Equal(new WhoAmIBody(ActiveId, ActiveSlug), body);
        }
    }

    private async Task<EfTenantLookup> LookupAsync()
    {
        await SeedOnceAsync();
        return new EfTenantLookup(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options);
    }

    private async Task SeedOnceAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            await using var context = fixture.CreateContext(new TenantContext());
            if (await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == ActiveId))
            {
                return;
            }

            var suspended = Tenant.Create(SuspendedId, SuspendedSlug, "Suspended Club");
            suspended.Suspend();
            var deactivated = Tenant.Create(DeactivatedId, DeactivatedSlug, "Gone Club");
            deactivated.Deactivate();
            context.Tenants.AddRange(Tenant.Create(ActiveId, ActiveSlug, "Active Club"), suspended, deactivated);
            await context.SaveChangesAsync();
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private sealed record WhoAmIBody(Guid TenantId, string Slug);

    private sealed class LookupFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public LookupFactory(string connectionString) => this.connectionString = connectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
