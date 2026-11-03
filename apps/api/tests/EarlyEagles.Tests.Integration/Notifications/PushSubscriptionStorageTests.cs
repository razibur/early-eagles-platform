using EarlyEagles.Domain.Notifications;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Notifications;

// P0-068.3 — push_subscriptions round-trips, is invisible to other tenants,
// and holds one row per browser per tenant.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class PushSubscriptionStorageTests
{
    private const string P256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM";
    private const string Auth = "tBHItJI5svbpez7KI4CCXg";

    private readonly PostgresFixture fixture;

    public PushSubscriptionStorageTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Subscription_Round_Trips_And_Is_Visible_To_Its_Tenant_Only()
    {
        var endpoint = NewEndpoint();
        await SaveAsAlphaAsync(endpoint);

        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var saved = await alpha.PushSubscriptions.SingleAsync(s => s.Endpoint == endpoint);
        Assert.Equal(PostgresFixture.AlphaTenantId, saved.TenantId);
        Assert.Equal("user_alpha", saved.ClerkUserId);
        Assert.Equal(P256dh, saved.P256dh);
        Assert.Equal(Auth, saved.Auth);

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint));
    }

    [Fact]
    public async Task The_Same_Browser_Twice_In_One_Tenant_Is_Refused_By_The_Database()
    {
        var endpoint = NewEndpoint();
        await SaveAsAlphaAsync(endpoint);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsAlphaAsync(endpoint));

        Assert.Contains("IX_push_subscriptions_tenant_id_endpoint", exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Same_Browser_May_Subscribe_In_Two_Tenants()
    {
        var endpoint = NewEndpoint();
        await SaveAsAlphaAsync(endpoint);

        await using (var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug))
        {
            beta.PushSubscriptions.Add(PushSubscription.Create(
                Guid.CreateVersion7(), PostgresFixture.BetaTenantId, "user_alpha", endpoint, P256dh, Auth, DateTimeOffset.UtcNow));
            await beta.SaveChangesAsync();
        }

        await using var both = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        Assert.Equal(2, await both.PushSubscriptions.IgnoreQueryFilters().CountAsync(s => s.Endpoint == endpoint));
    }

    private static string NewEndpoint() => "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N");

    private async Task SaveAsAlphaAsync(string endpoint)
    {
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        alpha.PushSubscriptions.Add(PushSubscription.Create(
            Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, "user_alpha", endpoint, P256dh, Auth, DateTimeOffset.UtcNow));
        await alpha.SaveChangesAsync();
    }
}
