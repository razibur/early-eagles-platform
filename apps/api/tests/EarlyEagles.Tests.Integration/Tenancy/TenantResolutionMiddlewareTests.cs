using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-031.2 — drives the real Program.cs pipeline (extractor + lookup +
// initializer) end-to-end and asserts the tenant context arrives at the
// handler in the right state. Captures via an IStartupFilter that runs the
// capture step AFTER next() so it sees the post-resolution state.
public class TenantResolutionMiddlewareTests : IClassFixture<TenantResolutionMiddlewareTests.Factory>
{
    private const string KnownSlug = "early-eagles";
    private static readonly Guid KnownTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Factory factory;

    public TenantResolutionMiddlewareTests(Factory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Known_Tenant_Slug_Populates_Context_For_Handler()
    {
        var capture = factory.Services.GetRequiredService<CapturedSnapshots>();
        capture.Reset();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{KnownSlug}.localhost";

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        var snapshot = Assert.Single(capture.Snapshot());
        Assert.True(snapshot.IsResolved);
        Assert.Equal(KnownTenantId, snapshot.TenantId);
        Assert.Equal(KnownSlug, snapshot.Slug);
    }

    [Fact]
    public async Task Missing_Subdomain_Leaves_Context_Unresolved()
    {
        var capture = factory.Services.GetRequiredService<CapturedSnapshots>();
        capture.Reset();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "localhost";

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        var snapshot = Assert.Single(capture.Snapshot());
        Assert.False(snapshot.IsResolved);
    }

    [Fact]
    public async Task Unknown_Tenant_Slug_Leaves_Context_Unresolved_Without_Erroring()
    {
        // The 404 for unknown tenants is layered on by P0-031.3; here we only
        // verify that the middleware does not crash and that the context
        // remains unresolved when the lookup misses.
        var capture = factory.Services.GetRequiredService<CapturedSnapshots>();
        capture.Reset();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "mystery.localhost";

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.True(response.IsSuccessStatusCode);
        var snapshot = Assert.Single(capture.Snapshot());
        Assert.False(snapshot.IsResolved);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<CapturedSnapshots>();

                // Replace the NullTenantLookup wired in AddInfrastructure with
                // a fake that knows about "early-eagles".
                var fake = new FakeTenantLookup();
                fake.Register(KnownSlug, KnownTenantId);
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(fake);

                services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter>(
                    _ => new CaptureStartupFilter());
            });
        }
    }

    public sealed class CapturedSnapshots
    {
        private readonly List<TenantContextSnapshot> snapshots = [];
        private readonly Lock @lock = new();

        public void Add(TenantContextSnapshot snapshot)
        {
            lock (@lock)
            {
                snapshots.Add(snapshot);
            }
        }

        public IReadOnlyList<TenantContextSnapshot> Snapshot()
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

    public sealed record TenantContextSnapshot(bool IsResolved, Guid? TenantId, string? Slug);

    public sealed class FakeTenantLookup : ITenantLookup
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

    private sealed class CaptureStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (httpContext, nextDelegate) =>
                {
                    // Run the rest of the pipeline first so resolution has
                    // a chance to populate the context; then read it.
                    await nextDelegate();
                    var reader = httpContext.RequestServices.GetRequiredService<ITenantContext>();
                    var snapshot = reader.IsResolved
                        ? new TenantContextSnapshot(true, reader.TenantId, reader.Slug)
                        : new TenantContextSnapshot(false, null, null);
                    httpContext.RequestServices.GetRequiredService<CapturedSnapshots>().Add(snapshot);
                });
                next(app);
            };
    }
}
