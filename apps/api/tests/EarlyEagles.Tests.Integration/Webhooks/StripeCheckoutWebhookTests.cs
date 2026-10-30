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

// P0-043.2 — a signed checkout.session.completed for a subscription records
// the Stripe ids on the tenant named by client_reference_id, queues one
// SubscriptionStarted, and audits it in that tenant's log; redeliveries and
// sessions that can't be matched to a tenant change nothing.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class StripeCheckoutWebhookTests
{
    private const string Secret = "whsec_checkout_test_secret";

    private static readonly string SubscriptionStartedType = typeof(SubscriptionStarted).FullName!;

    private readonly PostgresFixture fixture;

    public StripeCheckoutWebhookTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Subscription_Checkout_Links_The_Tenant_Queues_The_Event_And_Audits_It()
    {
        var tenantId = await NewTenantAsync();
        var (customer, subscription) = StripeIds();
        using var factory = new CheckoutFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(Checkout(tenantId.ToString(), customer, subscription)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(customer, tenant.StripeCustomerId);
        Assert.Equal(subscription, tenant.StripeSubscriptionId);

        var queued = await QueuedAsync(db, tenantId);
        var payload = Assert.Single(queued);
        Assert.Contains(subscription, payload, StringComparison.Ordinal);
        Assert.Contains(customer, payload, StringComparison.Ordinal);

        Assert.Equal(1, await db.AuditEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == tenantId && e.Action == "StartTenantSubscriptionCommand" && e.Success));
    }

    [Fact]
    public async Task A_Redelivered_Checkout_Queues_Nothing_More()
    {
        var tenantId = await NewTenantAsync();
        var (customer, subscription) = StripeIds();
        var body = Checkout(tenantId.ToString(), customer, subscription);
        using var factory = new CheckoutFactory(fixture);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(body))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(body))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Single(await QueuedAsync(db, tenantId));
    }

    [Fact]
    public async Task A_Tenant_Billed_To_Another_Customer_Is_Left_Alone()
    {
        var tenantId = await NewTenantAsync();
        var (customer, subscription) = StripeIds();
        using var factory = new CheckoutFactory(fixture);
        var client = factory.CreateClient();
        await client.SendAsync(Signed(Checkout(tenantId.ToString(), customer, subscription)));

        var response = await client.SendAsync(Signed(Checkout(tenantId.ToString(), "cus_intruder_" + customer, "sub_intruder")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(customer, tenant.StripeCustomerId);
        Assert.Equal(subscription, tenant.StripeSubscriptionId);
    }

    [Theory]
    [InlineData("not-a-guid", "subscription")]
    [InlineData(null, "subscription")]
    [InlineData("00000000-0000-0000-0000-00000000dead", "subscription")]
    [InlineData("tenant", "payment")]
    public async Task A_Session_Without_A_Known_Tenant_Or_Not_A_Subscription_Is_Acknowledged_And_Changes_Nothing(string? reference, string mode)
    {
        var tenantId = await NewTenantAsync();
        var (customer, subscription) = StripeIds();
        using var factory = new CheckoutFactory(fixture);

        var response = await factory.CreateClient().SendAsync(
            Signed(Checkout(reference == "tenant" ? tenantId.ToString() : reference, customer, subscription, mode)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.StripeCustomerId == customer));
        Assert.Empty(await QueuedAsync(db, tenantId));
    }

    [Fact]
    public async Task A_Deactivated_Tenant_Is_Not_Linked()
    {
        var tenantId = await NewTenantAsync(deactivated: true);
        var (customer, subscription) = StripeIds();
        using var factory = new CheckoutFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(Checkout(tenantId.ToString(), customer, subscription)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Null((await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId)).StripeCustomerId);
    }

    private static async Task<List<string>> QueuedAsync(Infrastructure.Persistence.ApplicationDbContext db, Guid tenantId) =>
        await db.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.EventType == SubscriptionStartedType)
            .Select(m => m.Payload)
            .ToListAsync();

    private static (string Customer, string Subscription) StripeIds()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return ("cus_" + suffix, "sub_" + suffix);
    }

    private static string Checkout(string? clientReferenceId, string customer, string subscription, string mode = "subscription")
    {
        var reference = clientReferenceId is null ? "null" : "\"" + clientReferenceId + "\"";
        return "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"object\":\"event\",\"api_version\":\"" + StripeConfiguration.ApiVersion
            + "\",\"created\":1793361600,\"livemode\":false,\"pending_webhooks\":1,\"type\":\"checkout.session.completed\","
            + "\"data\":{\"object\":{\"id\":\"cs_test_" + subscription + "\",\"object\":\"checkout.session\",\"mode\":\"" + mode
            + "\",\"status\":\"complete\",\"client_reference_id\":" + reference
            + ",\"customer\":\"" + customer + "\",\"subscription\":\"" + subscription + "\"}}}";
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

    private async Task<Guid> NewTenantAsync(bool deactivated = false)
    {
        var id = Guid.CreateVersion7();
        var slug = "checkout-" + id.ToString("N")[^8..];
        await using var context = fixture.CreateContext(new TenantContext());
        var tenant = Tenant.Create(id, slug, "Checkout Club");
        if (deactivated)
        {
            tenant.Deactivate();
        }

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return id;
    }

    private sealed class CheckoutFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public CheckoutFactory(PostgresFixture fixture) => connectionString = fixture.ConnectionString;

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
