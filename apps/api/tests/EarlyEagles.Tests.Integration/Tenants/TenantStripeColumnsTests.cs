using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Tenants;

// P0-045.1 — the Stripe ids survive a round trip through Postgres, and one
// Stripe customer can never pay for two tenants.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantStripeColumnsTests
{
    private readonly PostgresFixture fixture;

    public TenantStripeColumnsTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Stripe_Ids_Round_Trip()
    {
        var id = Guid.CreateVersion7();
        var slug = "stripe-" + id.ToString("N")[^8..];
        await using (var context = fixture.CreateContext(id, slug))
        {
            var tenant = Tenant.Create(id, slug, "Stripe Round Trip");
            tenant.LinkStripeSubscription("cus_round_" + slug, "sub_round_" + slug);
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
        }

        await using var readBack = fixture.CreateContext(id, slug);
        var stored = await readBack.Tenants.SingleAsync(t => t.Id == id);
        Assert.Equal("cus_round_" + slug, stored.StripeCustomerId);
        Assert.Equal("sub_round_" + slug, stored.StripeSubscriptionId);
    }

    [Fact]
    public async Task A_Stripe_Customer_Already_Billing_Another_Tenant_Is_Refused_By_The_Database()
    {
        var customer = "cus_shared_" + Guid.NewGuid().ToString("N")[..8];
        await SaveLinkedTenantAsync(customer, "sub_a_" + customer);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveLinkedTenantAsync(customer, "sub_b_" + customer));

        Assert.Contains("IX_tenants_stripe_customer_id", ex.InnerException?.Message, StringComparison.Ordinal);
    }

    private async Task SaveLinkedTenantAsync(string customer, string subscription)
    {
        var id = Guid.CreateVersion7();
        var slug = "stripe-" + id.ToString("N")[^8..];
        await using var context = fixture.CreateContext(new TenantContext());
        var tenant = Tenant.Create(id, slug, "Stripe Unique");
        tenant.LinkStripeSubscription(customer, subscription);
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
    }
}
