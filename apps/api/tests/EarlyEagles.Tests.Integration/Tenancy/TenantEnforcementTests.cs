using System.Net;
using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-031.3 — in Production the tenant-resolution middleware enforces a
// resolvable tenant for tenant-scoped requests: missing subdomain -> 400,
// unknown slug -> 404 (RFC 7807 ProblemDetails). Infrastructure paths
// (/health, /ready) stay tenant-agnostic; outside Production everything is
// lenient. Each scenario drives the real Program.cs pipeline.
public class TenantEnforcementTests
{
    private const string KnownSlug = "early-eagles";
    private static readonly Guid KnownTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Production_Missing_Subdomain_On_Tenant_Path_Returns_400()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "localhost";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Tenant not specified", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Production_Unknown_Slug_On_Tenant_Path_Returns_404()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "mystery.localhost";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Tenant not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Production_Known_Slug_Passes_Through_Middleware_Resolved()
    {
        using var factory = new ProductionFactory();
        var capture = factory.Services.GetRequiredService<CapturedSnapshots>();
        capture.Reset();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{KnownSlug}.localhost";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        // The request passed the gate resolved (not the middleware's "Tenant not
        // found" problem); the resolved-tenant response body is asserted in the
        // P0-031.4 E2E test, so here we only check the context reached the handler.
        var snapshot = Assert.Single(capture.Snapshot());
        Assert.True(snapshot.IsResolved);
        Assert.Equal(KnownTenantId, snapshot.TenantId);
        Assert.DoesNotContain("Tenant not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Production_Health_Endpoint_Is_Reachable_Without_A_Tenant()
    {
        // Uptime probes hit /health on the bare domain; it must never be gated.
        using var factory = new ProductionFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "localhost";

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonProduction_Missing_Subdomain_On_Tenant_Path_Is_Lenient()
    {
        // Development host tolerates missing tenants (dev via localhost / IP):
        // the middleware passes the request through to the handler instead of
        // short-circuiting. The /whoami endpoint then reports its own unresolved
        // state ("Tenant not resolved") — crucially NOT the middleware's gate
        // ("Tenant not specified"), proving the middleware stayed lenient.
        using var factory = new DevelopmentFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "localhost";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant not specified", body);
        Assert.Contains("Tenant not resolved", body);
    }

    private static void ConfigureFakeLookupAndCapture(IServiceCollection services)
    {
        services.AddSingleton<CapturedSnapshots>();

        var fake = new FakeTenantLookup();
        fake.Register(KnownSlug, KnownTenantId);
        services.RemoveAll<ITenantLookup>();
        services.AddSingleton<ITenantLookup>(fake);

        services.AddTransient<IStartupFilter>(_ => new CaptureStartupFilter());
    }

    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(ConfigureFakeLookupAndCapture);
        }
    }

    private sealed class DevelopmentFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(ConfigureFakeLookupAndCapture);
        }
    }

    private sealed class CapturedSnapshots
    {
        private readonly List<Snapshot> snapshots = [];
        private readonly Lock @lock = new();

        public void Add(Snapshot snapshot)
        {
            lock (@lock)
            {
                snapshots.Add(snapshot);
            }
        }

        public IReadOnlyList<Snapshot> Snapshot()
        {
            lock (@lock)
            {
                return [.. snapshots];
            }
        }

        public void Reset()
        {
            lock (@lock)
            {
                snapshots.Clear();
            }
        }
    }

    private sealed record Snapshot(bool IsResolved, Guid? TenantId, string? Slug);

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

    private sealed class CaptureStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (httpContext, nextDelegate) =>
                {
                    await nextDelegate();
                    var reader = httpContext.RequestServices.GetRequiredService<ITenantContext>();
                    var snapshot = reader.IsResolved
                        ? new Snapshot(true, reader.TenantId, reader.Slug)
                        : new Snapshot(false, null, null);
                    httpContext.RequestServices.GetRequiredService<CapturedSnapshots>().Add(snapshot);
                });
                next(app);
            };
    }
}
