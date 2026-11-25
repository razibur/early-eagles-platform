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

// P0-045.3 — a signed customer.subscription.created / updated moves the tenant
// to the plan its price sells (Stripe:Prices), queuing one PlanChanged and
// auditing it in the tenant's log. Unknown prices, redeliveries, deletions and
// a subscription checkout hasn't linked yet leave the plan alone.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class StripePlanWebhookTests
{
    private const string Secret = "whsec_plan_test_secret";
    private const string ProPrice = "price_test_pro";
    private const string EnterprisePrice = "price_test_enterprise";

    private static readonly string PlanChangedType = typeof(PlanChanged).FullName!;

    private readonly PostgresFixture fixture;

    public StripePlanWebhookTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task An_Updated_Subscription_Moves_The_Tenant_To_Its_Price_Plan_Queues_The_Event_And_Audits_It()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", billed, "active", ProPrice)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Pro, (await TenantAsync(db, billed)).Plan);
        var payload = Assert.Single(await QueuedAsync(db, billed));
        Assert.Contains(billed.Subscription, payload, StringComparison.Ordinal);
        Assert.Contains("\"to\":", payload, StringComparison.Ordinal);
        Assert.Equal(1, await db.AuditEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == billed.TenantId && e.Action == "ChangeTenantPlanCommand" && e.Success));
    }

    [Fact]
    public async Task A_Created_Subscription_Sets_The_Plan_Once_Checkout_Has_Linked_It()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new PlanFactory(fixture);
        var created = SubscriptionEvent("customer.subscription.created", billed, "active", EnterprisePrice);

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().SendAsync(Signed(created))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().SendAsync(Signed(created))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Enterprise, (await TenantAsync(db, billed)).Plan);
        Assert.Single(await QueuedAsync(db, billed));
    }

    [Fact]
    public async Task A_Created_Subscription_Before_Checkout_Links_It_Changes_Nothing()
    {
        var unlinked = await NewBilledTenantAsync(link: false);
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.created", unlinked, "active", ProPrice)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Free, (await TenantAsync(db, unlinked)).Plan);
    }

    [Fact]
    public async Task The_Highest_Plan_Among_The_Items_Wins()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(
            Signed(SubscriptionEvent("customer.subscription.updated", billed, "active", ProPrice, "price_unknown_addon", EnterprisePrice)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Enterprise, (await TenantAsync(db, billed)).Plan);
    }

    [Fact]
    public async Task An_Unknown_Price_Leaves_The_Plan_Alone()
    {
        var billed = await NewBilledTenantAsync(t => t.ChangePlan(TenantPlan.Pro));
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", billed, "active", "price_not_configured")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Pro, (await TenantAsync(db, billed)).Plan);
        Assert.Empty(await QueuedAsync(db, billed));
    }

    [Fact]
    public async Task One_Event_Can_Change_Both_The_Plan_And_The_Status()
    {
        var billed = await NewBilledTenantAsync();
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.updated", billed, "unpaid", ProPrice)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await TenantAsync(db, billed);
        Assert.Equal(TenantPlan.Pro, tenant.Plan);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
    }

    [Fact]
    public async Task A_Deleted_Subscription_Ends_On_Free_Whatever_Its_Price()
    {
        // Still on Free (the plan was never set), so applying the price first
        // would show up as a PlanChanged.
        var billed = await NewBilledTenantAsync();
        using var factory = new PlanFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(SubscriptionEvent("customer.subscription.deleted", billed, "canceled", ProPrice)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantPlan.Free, (await TenantAsync(db, billed)).Plan);
        Assert.Empty(await QueuedAsync(db, billed));
    }

    private static Task<Tenant> TenantAsync(Infrastructure.Persistence.ApplicationDbContext db, Billed billed) =>
        db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == billed.TenantId);

    private static async Task<List<string>> QueuedAsync(Infrastructure.Persistence.ApplicationDbContext db, Billed billed) =>
        await db.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == billed.TenantId && m.EventType == PlanChangedType)
            .Select(m => m.Payload)
            .ToListAsync();

    private static string SubscriptionEvent(string type, Billed billed, string status, params string[] priceIds)
    {
        var items = string.Join(",", priceIds.Select((price, i) =>
            "{\"id\":\"si_" + i + "\",\"object\":\"subscription_item\",\"price\":{\"id\":\"" + price + "\",\"object\":\"price\"}}"));
        return "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"object\":\"event\",\"api_version\":\"" + StripeConfiguration.ApiVersion
            + "\",\"created\":1793361600,\"livemode\":false,\"pending_webhooks\":1,\"type\":\"" + type + "\","
            + "\"data\":{\"object\":{\"id\":\"" + billed.Subscription + "\",\"object\":\"subscription\",\"status\":\"" + status
            + "\",\"customer\":\"" + billed.Customer + "\",\"items\":{\"object\":\"list\",\"data\":[" + items + "]}}}}";
    }

    private static HttpRequestMessage Signed(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StripeWebhookEndpoints.Route)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(StripeWebhookEndpoints.SignatureHeader, EventUtility.GenerateSignatureHeader(payload, Secret));
        return request;
    }

    private async Task<Billed> NewBilledTenantAsync(Action<Tenant>? arrange = null, bool link = true)
    {
        var id = Guid.CreateVersion7();
        var suffix = id.ToString("N")[^12..];
        var billed = new Billed(id, "cus_" + suffix, "sub_" + suffix);
        await using var context = fixture.CreateContext(new TenantContext());
        var tenant = Tenant.Create(id, "plan-" + suffix, "Plan Club");
        if (link)
        {
            tenant.LinkStripeSubscription(billed.Customer, billed.Subscription);
        }

        arrange?.Invoke(tenant);
        tenant.ClearDomainEvents();

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return billed;
    }

    private sealed record Billed(Guid TenantId, string Customer, string Subscription);

    private sealed class PlanFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public PlanFactory(PostgresFixture fixture) => connectionString = fixture.ConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Stripe:WebhookSigningSecret", Secret);
            builder.UseSetting("Stripe:Prices:" + ProPrice, "Pro");
            builder.UseSetting("Stripe:Prices:" + EnterprisePrice, "Enterprise");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
