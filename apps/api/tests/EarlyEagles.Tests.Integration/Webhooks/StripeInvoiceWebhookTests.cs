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

// P0-043.4 — a signed invoice.payment_failed finds the tenant by its Stripe
// customer, marks it past due, queues one PaymentFailed, and audits it in that
// tenant's log; retries, unknown customers and suspended tenants change nothing.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class StripeInvoiceWebhookTests
{
    private const string Secret = "whsec_invoice_test_secret";

    private static readonly string PaymentFailedType = typeof(PaymentFailed).FullName!;

    private readonly PostgresFixture fixture;

    public StripeInvoiceWebhookTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Failed_Payment_Marks_The_Tenant_Past_Due_Queues_The_Event_And_Audits_It()
    {
        var (tenantId, customer) = await NewBilledTenantAsync();
        var invoice = "in_" + Guid.NewGuid().ToString("N")[..12];
        using var factory = new InvoiceFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(PaymentFailedEvent(invoice, customer)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.PastDue, (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId)).Status);
        Assert.Contains(invoice, Assert.Single(await QueuedAsync(db, tenantId)), StringComparison.Ordinal);
        Assert.Equal(1, await db.AuditEvents.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == tenantId && e.Action == "RecordTenantPaymentFailureCommand" && e.Success));
    }

    [Fact]
    public async Task Retried_And_Redelivered_Failures_Queue_Nothing_More()
    {
        var (tenantId, customer) = await NewBilledTenantAsync();
        using var factory = new InvoiceFactory(fixture);
        var client = factory.CreateClient();
        var first = PaymentFailedEvent("in_first_" + customer, customer);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(first))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(first))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Signed(PaymentFailedEvent("in_retry_" + customer, customer)))).StatusCode);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Single(await QueuedAsync(db, tenantId));
    }

    [Fact]
    public async Task An_Unknown_Customer_Is_Acknowledged_And_Changes_Nothing()
    {
        var (tenantId, customer) = await NewBilledTenantAsync();
        using var factory = new InvoiceFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(PaymentFailedEvent("in_stranger", "cus_stranger_" + customer)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Active, (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId)).Status);
        Assert.Empty(await QueuedAsync(db, tenantId));
    }

    [Fact]
    public async Task A_Suspended_Tenant_Stays_Suspended()
    {
        var (tenantId, customer) = await NewBilledTenantAsync(t => t.Suspend());
        using var factory = new InvoiceFactory(fixture);

        var response = await factory.CreateClient().SendAsync(Signed(PaymentFailedEvent("in_suspended_" + customer, customer)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.Equal(TenantStatus.Suspended, (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId)).Status);
        Assert.Empty(await QueuedAsync(db, tenantId));
    }

    private static async Task<List<string>> QueuedAsync(Infrastructure.Persistence.ApplicationDbContext db, Guid tenantId) =>
        await db.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.EventType == PaymentFailedType)
            .Select(m => m.Payload)
            .ToListAsync();

    private static string PaymentFailedEvent(string invoice, string customer) =>
        "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"object\":\"event\",\"api_version\":\"" + StripeConfiguration.ApiVersion
        + "\",\"created\":1793361600,\"livemode\":false,\"pending_webhooks\":1,\"type\":\"invoice.payment_failed\","
        + "\"data\":{\"object\":{\"id\":\"" + invoice + "\",\"object\":\"invoice\",\"status\":\"open\",\"customer\":\"" + customer + "\"}}}";

    private static HttpRequestMessage Signed(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StripeWebhookEndpoints.Route)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(StripeWebhookEndpoints.SignatureHeader, EventUtility.GenerateSignatureHeader(payload, Secret));
        return request;
    }

    private async Task<(Guid TenantId, string Customer)> NewBilledTenantAsync(Action<Tenant>? arrange = null)
    {
        var id = Guid.CreateVersion7();
        var suffix = id.ToString("N")[^12..];
        var customer = "cus_" + suffix;
        await using var context = fixture.CreateContext(new TenantContext());
        var tenant = Tenant.Create(id, "invoice-" + suffix, "Invoice Club");
        tenant.LinkStripeSubscription(customer, "sub_" + suffix);
        arrange?.Invoke(tenant);
        tenant.ClearDomainEvents();

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return (id, customer);
    }

    private sealed class InvoiceFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public InvoiceFactory(PostgresFixture fixture) => connectionString = fixture.ConnectionString;

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
