using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Observability;

// P0-089.2 — the request span carries tenant.id / tenant.slug once the tenant
// resolves, and user.id for an authenticated principal. Listens to the ASP.NET
// Core request activity directly; no exporter involved. Slugs and user ids are
// unique to this class so parallel tests' activities can't be mistaken for ours.
//
// The client gets the response before the server stops the request activity
// (that happens when the HttpContext is disposed), so each test waits for its
// span instead of asserting straight after the call; CI hit that race.
public sealed class TelemetryEnrichmentTests : IDisposable
{
    private const string Slug = "otel-enrich-check";
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);
    private static readonly Guid TenantId = new("e0e0e0e0-0089-0002-0000-000000000001");

    private readonly ConcurrentBag<Activity> stopped = [];
    private readonly ActivityListener listener;

    public TelemetryEnrichmentTests()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
    }

    public void Dispose() => listener.Dispose();

    [Fact]
    public async Task Tenant_Request_Span_Carries_Tenant_Tags_And_No_User()
    {
        using var factory = new EnrichmentFactory(userId: null);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{Slug}.localhost";

        await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        var span = await WaitForSpanAsync(a => (a.GetTagItem("tenant.slug") as string) == Slug);
        Assert.Equal(TenantId.ToString(), span.GetTagItem("tenant.id"));
        Assert.Null(span.GetTagItem("user.id"));
    }

    [Fact]
    public async Task Authenticated_Request_Span_Carries_The_User_Id()
    {
        const string userId = "user_otel_enrich_check";
        using var factory = new EnrichmentFactory(userId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{Slug}.localhost";

        await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        await WaitForSpanAsync(a => (a.GetTagItem("user.id") as string) == userId && (a.GetTagItem("tenant.slug") as string) == Slug);
    }

    [Fact]
    public async Task Request_Without_A_Tenant_Has_No_Tenant_Tags()
    {
        using var factory = new EnrichmentFactory(userId: null);
        var client = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");

        await client.GetAsync(new Uri($"/whoami?marker={marker}", UriKind.Relative));

        var span = await WaitForSpanAsync(a => MatchesMarker(a, marker));
        Assert.Null(span.GetTagItem("tenant.id"));
    }

    // The startup filter records each request's query string on its activity.
    private static bool MatchesMarker(Activity activity, string marker) =>
        activity.GetCustomProperty("test.query") is string query && query.Contains(marker, StringComparison.Ordinal);

    // Polls for the one stopped activity matching the predicate; fails with the
    // usual Assert.Single message if none (or several) arrive in time.
    private async Task<Activity> WaitForSpanAsync(Func<Activity, bool> predicate)
    {
        var deadline = DateTime.UtcNow + SpanTimeout;
        while (DateTime.UtcNow < deadline && !stopped.Any(predicate))
        {
            await Task.Delay(10);
        }

        return Assert.Single(stopped, a => predicate(a));
    }

    private sealed class EnrichmentFactory : WebApplicationFactory<Program>
    {
        private readonly string? userId;

        public EnrichmentFactory(string? userId) => this.userId = userId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(new SingleTenantLookup());
                services.AddSingleton<IStartupFilter>(new TestPrincipalStartupFilter(userId));
            });
        }
    }

    private sealed class SingleTenantLookup : ITenantLookup
    {
        public Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(slug == Slug ? new TenantLookupResult(TenantId, Slug) : null);
    }

    // Stands in for authentication until Clerk (P0-035): signs the request in
    // as `sub = userId` before the app's own middleware runs, and records the
    // query string on the request activity for the no-tenant test.
    private sealed class TestPrincipalStartupFilter : IStartupFilter
    {
        private readonly string? userId;

        public TestPrincipalStartupFilter(string? userId) => this.userId = userId;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                Activity.Current?.SetCustomProperty("test.query", context.Request.QueryString.Value);
                if (userId is not null)
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId)], authenticationType: "Test"));
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
