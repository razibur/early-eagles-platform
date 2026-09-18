using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.6 ★ — the end-to-end isolation proof at the HTTP layer. Two tenants,
// two subdomains, one seeded database: an Alpha-scoped request that addresses
// a Beta member by its stable id must come back as 404, and vice versa. The
// endpoint code is deliberately naive (`FirstOrDefaultAsync(m.ClerkUserId ==
// id)`) so the 404 is provably the work of the global query filter, not a
// hand-rolled tenant check. That's the guarantee the platform relies on.
[Collection(PostgresCollectionDefinition.Name)]
public class CrossTenantEndpointTests
{
    private readonly PostgresFixture fixture;

    public CrossTenantEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Alpha_Requesting_Own_Member_Returns_200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.AlphaSlug}.earlyeagles.club";

        var response = await client.GetAsync(
            new Uri($"/tenant-members/{PostgresFixture.AlphaAdminUserId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantMemberBody>();
        Assert.NotNull(body);
        Assert.Equal(PostgresFixture.AlphaTenantId, body!.TenantId);
        Assert.Equal(PostgresFixture.AlphaAdminUserId, body.ClerkUserId);
        Assert.Equal(TenantMemberRole.TenantAdmin, body.Role);
    }

    [Fact]
    public async Task Alpha_Requesting_Beta_Member_By_Id_Returns_404()
    {
        // Beta's row exists in the same database — the fixture seeds it. Isolation
        // is what turns the cross-tenant read into a miss.
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.AlphaSlug}.earlyeagles.club";

        var response = await client.GetAsync(
            new Uri($"/tenant-members/{PostgresFixture.BetaAdminUserId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Beta_Requesting_Alpha_Member_By_Id_Returns_404()
    {
        // Same guarantee from the other side of the fence.
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.BetaSlug}.earlyeagles.club";

        var response = await client.GetAsync(
            new Uri($"/tenant-members/{PostgresFixture.AlphaMemberUserId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Beta_Requesting_Own_Member_Returns_200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.BetaSlug}.earlyeagles.club";

        var response = await client.GetAsync(
            new Uri($"/tenant-members/{PostgresFixture.BetaAdminUserId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantMemberBody>();
        Assert.NotNull(body);
        Assert.Equal(PostgresFixture.BetaTenantId, body!.TenantId);
    }

    [Fact]
    public async Task Alpha_Requesting_Unknown_Id_Returns_404_Same_As_Cross_Tenant()
    {
        // A truly missing id and a cross-tenant id look identical to the caller —
        // that's the whole point. This test pins the equivalence, so a future
        // change that leaks the difference (e.g. a 403 for cross-tenant, exposing
        // that the row exists elsewhere) fails here.
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{PostgresFixture.AlphaSlug}.earlyeagles.club";

        var response = await client.GetAsync(
            new Uri("/tenant-members/user_does_not_exist_anywhere", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private CrossTenantFactory CreateFactory() => new(fixture.ConnectionString);

    private sealed record TenantMemberBody(Guid TenantId, string ClerkUserId, TenantMemberRole Role);

    // WebApplicationFactory swaps two things the real host would resolve differently:
    //   * ITenantLookup — pointed at the seeded Alpha/Beta pair so subdomain
    //     resolution succeeds in Production.
    //   * DbContextOptions — pointed at the Testcontainers Postgres so the endpoint
    //     reads the same seeded rows the isolation gate tests operate on.
    private sealed class CrossTenantFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public CrossTenantFactory(string connectionString)
        {
            this.connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                var fake = new FakeTenantLookup();
                fake.Register(PostgresFixture.AlphaSlug, PostgresFixture.AlphaTenantId);
                fake.Register(PostgresFixture.BetaSlug, PostgresFixture.BetaTenantId);
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(fake);

                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }

    private sealed class FakeTenantLookup : ITenantLookup
    {
        private readonly Dictionary<string, Guid> known = new(StringComparer.OrdinalIgnoreCase);

        public void Register(string slug, Guid tenantId) => known[slug] = tenantId;

        public Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
        {
            return Task.FromResult(known.TryGetValue(slug, out var id)
                ? new TenantLookupResult(id, slug)
                : null);
        }
    }
}
