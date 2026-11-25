using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.Billing;

/// <summary>
/// <see cref="IBillingPlanCatalog"/> over <see cref="StripeOptions.Prices"/>
/// (P0-045.2). Read once: changing the prices means a restart, like every
/// other setting.
/// </summary>
public sealed class StripePlanCatalog : IBillingPlanCatalog
{
    private readonly Dictionary<string, TenantPlan> prices;

    public StripePlanCatalog(IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        prices = new Dictionary<string, TenantPlan>(StringComparer.Ordinal);
        foreach (var (priceId, planName) in options.Value.Prices)
        {
            // Startup validation has refused anything else.
            if (StripeOptions.TryParsePlan(planName, out var plan))
            {
                prices[priceId] = plan;
            }
        }
    }

    public TenantPlan? FindPlan(string priceId) =>
        priceId is not null && prices.TryGetValue(priceId, out var plan) ? plan : null;
}
