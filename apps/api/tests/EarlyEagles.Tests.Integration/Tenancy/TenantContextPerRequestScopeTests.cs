using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-030.2 — proves the tenant context is genuinely per-HTTP-request when wired
// through the real WebApplicationFactory pipeline. The unit test in P0-030.1
// already covers the DI shape using a synthetic ServiceCollection; this test
// pins the behaviour against the actual app boot path.
public class TenantContextPerRequestScopeTests : IClassFixture<TenantContextPerRequestScopeTests.Factory>
{
    private readonly Factory factory;

    public TenantContextPerRequestScopeTests(Factory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Two_HTTP_Requests_Get_Distinct_TenantContext_Instances()
    {
        var capture = factory.Services.GetRequiredService<CapturedScopes>();
        capture.Reset();
        var client = factory.CreateClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));
        await client.GetAsync(new Uri("/health", UriKind.Relative));

        var snapshot = capture.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.NotSame(snapshot[0].Reader, snapshot[1].Reader);
        Assert.True(snapshot[0].Reader.IsResolved);
        Assert.True(snapshot[1].Reader.IsResolved);
    }

    [Fact]
    public async Task Within_One_Request_Reader_And_Setter_Resolve_To_The_Same_Instance()
    {
        var capture = factory.Services.GetRequiredService<CapturedScopes>();
        capture.Reset();
        var client = factory.CreateClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        var snapshot = capture.Snapshot();
        var only = Assert.Single(snapshot);
        Assert.Same(only.Reader, only.Setter);
        Assert.True(only.Reader.IsResolved);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<CapturedScopes>();

                // IStartupFilter lets us prepend a middleware without
                // overwriting Program.cs's pipeline configuration.
                services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter>(
                    _ => new CaptureStartupFilter());
            });
        }
    }

    public sealed class CapturedScopes
    {
        private readonly List<CapturedScope> scopes = [];
        private readonly Lock @lock = new();

        public void Add(ITenantContext reader, ITenantContextInitializer setter)
        {
            lock (@lock)
            {
                scopes.Add(new CapturedScope(reader, setter));
            }
        }

        public IReadOnlyList<CapturedScope> Snapshot()
        {
            lock (@lock)
            {
                return [.. scopes];
            }
        }

        public void Reset()
        {
            lock (@lock)
            {
                scopes.Clear();
            }
        }
    }

    public sealed record CapturedScope(ITenantContext Reader, ITenantContextInitializer Setter);

    private sealed class CaptureStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (httpContext, nextDelegate) =>
                {
                    var setter = httpContext.RequestServices.GetRequiredService<ITenantContextInitializer>();
                    var reader = httpContext.RequestServices.GetRequiredService<ITenantContext>();
                    setter.Initialize(Guid.NewGuid(), "test-tenant");
                    var capture = httpContext.RequestServices.GetRequiredService<CapturedScopes>();
                    capture.Add(reader, setter);
                    await nextDelegate();
                });
                next(app);
            };
    }
}
