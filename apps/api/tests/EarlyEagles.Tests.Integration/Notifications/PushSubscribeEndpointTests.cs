using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EarlyEagles.Api.Notifications;
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

namespace EarlyEagles.Tests.Integration.Notifications;

// P0-068.3.2 + P0-068.4 — a member posts the browser's PushSubscription.toJSON()
// to POST /api/push/subscribe on their club's subdomain and a push_subscriptions
// row appears for them in that club, audited. Uses its own tenants (Epsilon,
// Zeta) so it doesn't disturb the shared Alpha/Beta fixtures.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class PushSubscribeEndpointTests : IAsyncLifetime
{
    private const string EpsilonSlug = "epsilon-push";
    private const string ZetaSlug = "zeta-push";
    private const string EpsilonOrg = "org_epsilon_push";
    private const string ZetaOrg = "org_zeta_push";
    private const string P256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM";
    private const string Auth = "tBHItJI5svbpez7KI4CCXg";

    private static readonly Guid EpsilonTenantId = new("eeeeeeee-0068-0000-0000-000000000001");
    private static readonly Guid ZetaTenantId = new("ffffffff-0068-0000-0000-000000000001");

    private readonly PostgresFixture fixture;

    public PushSubscribeEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = fixture.CreateContext(new TenantContext());
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == EpsilonTenantId))
        {
            var epsilon = Tenant.Create(EpsilonTenantId, EpsilonSlug, "Epsilon Club");
            epsilon.LinkClerkOrganization(EpsilonOrg);
            var zeta = Tenant.Create(ZetaTenantId, ZetaSlug, "Zeta Club");
            zeta.LinkClerkOrganization(ZetaOrg);
            db.Tenants.AddRange(epsilon, zeta);
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Subscribes_And_A_Row_Is_Stored_For_Them_In_Their_Club(string environment)
    {
        var endpoint = NewEndpoint();
        using var factory = new PushFactory(fixture.ConnectionString, environment);

        var response = await ClientFor(factory, EpsilonSlug, "user_push_1").PostAsJsonAsync(PushEndpoints.SubscribeRoute, Body(endpoint));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<PushSubscribeResponse>())!.Id;

        await using var db = fixture.CreateContext(new TenantContext());
        var row = await db.PushSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.Endpoint == endpoint);
        Assert.Equal(id, row.Id);
        Assert.Equal(EpsilonTenantId, row.TenantId);
        Assert.Equal("user_push_1", row.ClerkUserId);
        Assert.Equal(P256dh, row.P256dh);
        Assert.Equal(Auth, row.Auth);
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == EpsilonTenantId && e.Action == "SubscribeToPushCommand" && e.Success));
    }

    [Fact]
    public async Task The_Same_Browser_Subscribing_Again_Renews_Its_One_Row()
    {
        var endpoint = NewEndpoint();
        using var factory = new PushFactory(fixture.ConnectionString);

        var first = await ClientFor(factory, EpsilonSlug, "user_push_1").PostAsJsonAsync(PushEndpoints.SubscribeRoute, Body(endpoint));
        var second = await ClientFor(factory, EpsilonSlug, "user_push_2")
            .PostAsJsonAsync(PushEndpoints.SubscribeRoute, Body(endpoint, auth: "bmV3IGF1dGggc2VjcmV0"));

        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<PushSubscribeResponse>())!.Id,
            (await second.Content.ReadFromJsonAsync<PushSubscribeResponse>())!.Id);
        await using var db = fixture.CreateContext(new TenantContext());
        var row = await db.PushSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.Endpoint == endpoint);
        Assert.Equal("user_push_2", row.ClerkUserId);
        Assert.Equal("bmV3IGF1dGggc2VjcmV0", row.Auth);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var factory = new PushFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{EpsilonSlug}.earlyeagles.club";

        var response = await client.PostAsJsonAsync(PushEndpoints.SubscribeRoute, Body(NewEndpoint()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403_And_Stores_Nothing()
    {
        var endpoint = NewEndpoint();
        using var factory = new PushFactory(fixture.ConnectionString);

        var response = await ClientFor(factory, EpsilonSlug, "user_push_1", organization: ZetaOrg)
            .PostAsJsonAsync(PushEndpoints.SubscribeRoute, Body(endpoint));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.PushSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.Endpoint == endpoint));
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/plain-http", P256dh, Auth)]
    [InlineData("https://10.0.0.5/push", P256dh, Auth)]
    [InlineData("https://fcm.googleapis.com/fcm/send/bad-key", "not base64url!", Auth)]
    [InlineData("https://fcm.googleapis.com/fcm/send/no-auth", P256dh, null)]
    [InlineData(null, P256dh, Auth)]
    public async Task A_Malformed_Subscription_Returns_400_And_Stores_Nothing(string? endpoint, string p256dh, string? auth)
    {
        using var factory = new PushFactory(fixture.ConnectionString);
        var client = ClientFor(factory, EpsilonSlug, "user_push_bad");

        var response = await client.PostAsJsonAsync(PushEndpoints.SubscribeRoute, new { endpoint, expirationTime = (long?)null, keys = new { p256dh, auth } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.PushSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.ClerkUserId == "user_push_bad"));
    }

    private static string NewEndpoint() => "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N");

    // The shape the browser's PushSubscription.toJSON() produces.
    private static object Body(string endpoint, string auth = Auth) =>
        new { endpoint, expirationTime = (long?)null, keys = new { p256dh = P256dh, auth } };

    private static HttpClient ClientFor(PushFactory factory, string slug, string userId, string? organization = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";
        var token = ClerkTestTokens.Create(
            userId,
            "sess_" + userId,
            extra: ClerkTestTokens.ActiveOrganization(organization ?? (slug == EpsilonSlug ? EpsilonOrg : ZetaOrg), "member"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class PushFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string environment;

        public PushFactory(string connectionString, string environment = "Development")
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
