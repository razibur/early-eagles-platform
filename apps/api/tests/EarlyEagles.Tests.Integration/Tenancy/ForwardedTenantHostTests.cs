using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Api.Tenancy;
using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyEagles.Tests.Integration.Tenancy;

// P0-071.4 / ADR-0016 — the web app forwards /api/* to the API under the API's
// own host name, saying which club address the browser used in
// X-Forwarded-Host. The full Production pipeline resolves that club only when
// the request carries the shared proxy key; otherwise the forwarded host is
// ignored and the tenant gate answers 400, as for any request without a club.
public class ForwardedTenantHostTests
{
    private const string KnownSlug = "early-eagles";
    private const string ProxyKey = "test-proxy-key-0123456789abcdef-xyz";

    // A Container Apps default name: not under a club domain, so it carries no club.
    // (api.earlyeagles.club would itself be read as a club called "api".)
    private const string ApiHost = "ee-api.happyfield-1234.eastus.azurecontainerapps.io";
    private static readonly Guid KnownTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task With_The_Key_The_Forwarded_Club_Is_Resolved()
    {
        using var factory = new ProxyFactory(ProxyKey);

        var response = await SendAsync(factory, key: ProxyKey, forwardedHost: $"{KnownSlug}.earlyeagles.club");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIBody>();
        Assert.Equal(KnownTenantId, body!.TenantId);
        Assert.Equal(KnownSlug, body.Slug);
    }

    [Fact]
    public async Task A_Dev_Host_With_A_Port_Is_Resolved_Too()
    {
        using var factory = new ProxyFactory(ProxyKey);

        var response = await SendAsync(factory, key: ProxyKey, forwardedHost: $"{KnownSlug}.localhost:3002");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-0123456789abcdef-0123456789")]
    [InlineData("test-proxy-key-0123456789abcdef-xy")]
    [InlineData("")]
    public async Task Without_The_Right_Key_The_Forwarded_Host_Is_Ignored(string? key)
    {
        using var factory = new ProxyFactory(ProxyKey);

        var response = await SendAsync(factory, key: key, forwardedHost: $"{KnownSlug}.earlyeagles.club");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task With_No_Key_Configured_Nothing_Is_Forwarded()
    {
        using var factory = new ProxyFactory(key: null);

        var response = await SendAsync(factory, key: ProxyKey, forwardedHost: $"{KnownSlug}.earlyeagles.club");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("early-eagles.earlyeagles.club, other.earlyeagles.club")]
    [InlineData("10.0.0.5")]
    public async Task A_Malformed_Forwarded_Host_Is_Ignored_Even_With_The_Key(string forwardedHost)
    {
        using var factory = new ProxyFactory(ProxyKey);

        var response = await SendAsync(factory, key: ProxyKey, forwardedHost: forwardedHost);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Direct_Request_On_The_Club_Host_Still_Works()
    {
        using var factory = new ProxyFactory(ProxyKey);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{KnownSlug}.earlyeagles.club";

        var response = await client.GetAsync(new Uri("/whoami", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_Key_Never_Reaches_The_Handlers()
    {
        using var factory = new ProxyFactory(ProxyKey);

        var response = await SendAsync(factory, key: ProxyKey, forwardedHost: $"{KnownSlug}.earlyeagles.club", path: "/__test/headers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seen = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TrustedProxyOptions.KeyHeader, seen, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProxyKey, seen, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("has whitespace 0123456789abcdef0123456789")]
    public void A_Malformed_Key_Fails_At_Startup(string key)
    {
        using var factory = new ProxyFactory(key);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("TrustedProxy:Key", error.ToString(), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendAsync(ProxyFactory factory, string? key, string forwardedHost, string path = "/whoami")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = ApiHost;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation(TrustedProxyOptions.ForwardedHostHeader, forwardedHost);
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(TrustedProxyOptions.KeyHeader, key);
        }

        return await client.SendAsync(request);
    }

    private sealed record WhoAmIBody(Guid TenantId, string Slug);

    private sealed class ProxyFactory : WebApplicationFactory<Program>
    {
        private readonly string? key;

        public ProxyFactory(string? key) => this.key = key;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            if (key is not null)
            {
                builder.UseSetting($"{TrustedProxyOptions.SectionName}:{nameof(TrustedProxyOptions.Key)}", key);
            }

            builder.ConfigureServices(services =>
            {
                var fake = new FakeTenantLookup();
                fake.Register(KnownSlug, KnownTenantId);
                services.RemoveAll<ITenantLookup>();
                services.AddSingleton<ITenantLookup>(fake);

                // A probe after the whole pipeline: a path no endpoint matches
                // falls through to it, and it echoes the headers a handler sees.
                services.AddSingleton<IStartupFilter, HeaderProbe>();
            });
        }
    }

    private sealed class HeaderProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Run(context => context.Request.Path == "/__test/headers"
                ? context.Response.WriteAsync(string.Join('\n', context.Request.Headers.Select(h => $"{h.Key}: {h.Value}")))
                : Task.CompletedTask);
        };
    }

    private sealed class FakeTenantLookup : ITenantLookup
    {
        private readonly Dictionary<string, Guid> known = new(StringComparer.OrdinalIgnoreCase);

        public void Register(string slug, Guid tenantId) => known[slug] = tenantId;

        public Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(known.TryGetValue(slug, out var id) ? new TenantLookupResult(id, slug, null) : null);
    }
}
