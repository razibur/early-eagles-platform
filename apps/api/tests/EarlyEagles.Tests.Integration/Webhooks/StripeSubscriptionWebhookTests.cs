using System.Net;
using System.Text;
using EarlyEagles.Api.Webhooks;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Stripe;

namespace EarlyEagles.Tests.Integration.Webhooks;

// P0-043.3 — signed customer.subscription.* events find the tenant by its
// Stripe customer and suspend, reactivate or end its subscription, queuing one
// event and auditing it in that tenant's log. Redeliveries, other statuses,
// replaced subscriptions and unknown customers change nothing.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class StripeSubscriptionWebhookTests
{
    private const string Secret = "whsec_subscription_test_secret";

    private readonly PostgresFixture fixture;

    public StripeSubscriptionWebhookTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task An_Unpaid_Subscription_Suspends_The_Tenant_Queues_The_Event_And_Audits_It()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new SubscriptionFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", billed, "unpaid")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Suspended, (await TenantAsync(db, billed)).Status);
        Assert.Contains(billed.Subscription, Assert.Single(await QueuedAsync<SubscriptionSuspended>(db, billed)), StringComparison.Ordinal);
        Assert.Equal(1, await db.AuditEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == billed.TenantId && e.Action == "ChangeTenantSubscriptionCommand" && e.Success));
    }

    [Fact]
    public async Task A_Paused_Subscription_Suspends_The_Tenant()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new SubscriptionFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.paused", billed, "paused")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Suspended, (await TenantAsync(db, billed)).Status);
    }

    [Fact]
    public async Task An_Active_Subscription_Reactivates_A_Past_Due_Tenant_Once()
    {
        var billed = await NewBilledTenantAsync(t => t.MarkPastDue());
        using var factory = new SubscriptionFactory(fixture);
        var client = factory.CreateClient();
        var active = SubscriptionEvent("customer.subscription.updated", billed, "active");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(active))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(active))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Active, (await TenantAsync(db, billed)).Status);
        Assert.Single(await QueuedAsync<SubscriptionReactivated>(db, billed));
    }

    [Fact]
    public async Task A_Deleted_Subscription_Ends_It_Once()
    {
        var billed = await NewBilledTenantAsync(t => t.ChangePlan(TenantPlan.Pro));
        using var factory = new SubscriptionFactory(fixture);
        var client = factory.CreateClient();
        var deleted = SubscriptionEvent("customer.subscription.deleted", billed, "canceled");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(deleted))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(deleted))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await TenantAsync(db, billed);
        Assert.Equal(TenantPlan.Free, tenant.Plan);
        Assert.Null(tenant.StripeSubscriptionId);
        Assert.Equal(billed.Customer, tenant.StripeCustomerId);
        Assert.Single(await QueuedAsync<SubscriptionEnded>(db, billed));
        Assert.Equal(1, await db.AuditEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == billed.TenantId && e.Action == "ChangeTenantSubscriptionCommand"));
    }

    [Theory]
    [InlineData("past_due")]
    [InlineData("incomplete")]
    public async Task Statuses_Handled_Elsewhere_Change_Nothing(string status)
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new SubscriptionFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", billed, status)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Active, (await TenantAsync(db, billed)).Status);
        Assert.Empty(await db.AuditEvents.IgnoreQueryFilters().Where(e => e.TenantId == billed.TenantId).ToListAsync());
    }

    [Fact]
    public async Task An_Event_About_A_Replaced_Subscription_Changes_Nothing()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new SubscriptionFactory(fixture);
        var old = billed with { Subscription = "sub_old_" + billed.Customer };

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.deleted", old, "canceled")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(billed.Subscription, (await TenantAsync(db, billed)).StripeSubscriptionId);
        Assert.Empty(await QueuedAsync<SubscriptionEnded>(db, billed));
    }

    [Fact]
    public async Task An_Unknown_Customer_Is_Acknowledged_And_Changes_Nothing()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new SubscriptionFactory(fixture);
        var stranger = billed with { Customer = "cus_stranger_" + billed.Customer };

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", stranger, "unpaid")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Active, (await TenantAsync(db, billed)).Status);
        Assert.Empty(await QueuedAsync<SubscriptionSuspended>(db, billed));
    }

    private static Task<Tenant> TenantAsync(Infrastructure.Persistence.ApplicationDbContext db, Billed billed) =>
        db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == billed.TenantId);

    private static async Task<List<string>> QueuedAsync<TEvent>(Infrastructure.Persistence.ApplicationDbContext db, Billed billed)
    {
        var type = typeof(TEvent).FullName!;
        return await db.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == billed.TenantId && m.EventType == type)
            .Select(m => m.Payload)
            .ToListAsync();
    }

    private static string SubscriptionEvent(string type, Billed billed, string status) =>
        "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"object\":\"event\",\"api_version\":\"" + StripeConfiguration.ApiVersion
        + "\",\"created\":1793361600,\"livemode\":false,\"pending_webhooks\":1,\"type\":\"" + type + "\","
        + "\"data\":{\"object\":{\"id\":\"" + billed.Subscription + "\",\"object\":\"subscription\",\"status\":\"" + status
        + "\",\"customer\":\"" + billed.Customer + "\"}}}";

    private static HttpRequestMessage Signed(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StripeWebhookEndpoints.Route)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(StripeWebhookEndpoints.SignatureHeader, EventUtility.GenerateSignatureHeader(payload, Secret));
        return request;
    }

    private async Task<Billed> NewBilledTenantAsync(Action<Tenant>? arrange = null)
    {
        var id = Guid.CreateVersion7();
        var suffix = id.ToString("N")[^12..];
        var billed = new Billed(id, "cus_" + suffix, "sub_" + suffix);
        await using var context = fixture.CreateContext(new TenantContext());
        var tenant = Tenant.Create(id, "subscription-" + suffix, "Subscription Club");
        tenant.LinkStripeSubscription(billed.Customer, billed.Subscription);
        arrange?.Invoke(tenant);
        tenant.ClearDomainEvents();

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return billed;
    }

    private sealed record Billed(Guid TenantId, string Customer, string Subscription);

    private sealed class SubscriptionFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public SubscriptionFactory(PostgresFixture fixture) => connectionString = fixture.ConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Stripe:WebhookSigningSecret", Secret);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
