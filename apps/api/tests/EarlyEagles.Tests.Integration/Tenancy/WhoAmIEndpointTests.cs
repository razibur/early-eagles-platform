using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-031.4 — the end-to-end proof that subdomain → tenant resolution works:
// a request to <slug>.earlyeagles.club/whoami passes through the full Production
// pipeline (extractor + lookup + middleware) and the endpoint echoes the tenant
// the middleware resolved. Runs against the real Program.cs, in Production so the
// resolution gate is fully active.
public class WhoAmIEndpointTests
{
    private const string KnownSlug = "early-eagles";
    private static readonly Guid KnownTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Production_Whoami_On_Tenant_Subdomain_Returns_The_Resolved_Tenant()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{KnownSlug}.earlyeagles.club";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIBody>();
        Assert.NotNull(body);
        Assert.Equal(KnownTenantId, body!.TenantId);
        Assert.Equal(KnownSlug, body.Slug);
    }

    private sealed record WhoAmIBody(Guid TenantId, string Slug);

    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                var fake = new FakeTenantLookup();
                fake.Register(KnownSlug, KnownTenantId);
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(fake);
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
