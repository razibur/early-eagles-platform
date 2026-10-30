namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Platform-level tenant reads for Stripe webhooks (P0-043.2). They run on the
/// bare API host before any tenant is resolved, so they see every tenant.
/// </summary>
public interface IBillingTenantDirectory
{
    /// <summary>The tenant with <paramref name="tenantId"/>, or <c>null</c> when there is none.</summary>
    Task<BillingTenant?> FindAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant billed to Stripe customer <paramref name="stripeCustomerId"/>
    /// (P0-043.4), or <c>null</c> when none is. Invoice and subscription events
    /// name the customer, not the tenant.
    /// </summary>
    Task<BillingTenant?> FindByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken);
}
