using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Which plan a Stripe price sells (P0-045.2). The prices are created in the
/// Stripe dashboard (P0-042.*), so the mapping is configuration.
/// </summary>
public interface IBillingPlanCatalog
{
    /// <summary>The plan <paramref name="priceId"/> (<c>price_…</c>) belongs to, or <c>null</c> when it is not one of ours.</summary>
    TenantPlan? FindPlan(string priceId);
}
