using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Billing;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.Billing;

// P0-045.2 — Stripe:Prices says which plan each dashboard price sells.
public class StripePlanCatalogTests
{
    private static readonly StripePlanCatalog Catalog = new(Options.Create(new StripeOptions
    {
        Prices = new(StringComparer.Ordinal)
        {
            ["price_1QPro"] = "Pro",
            ["price_1QEnterprise"] = "enterprise",
        },
    }));

    [Theory]
    [InlineData("price_1QPro", TenantPlan.Pro)]
    [InlineData("price_1QEnterprise", TenantPlan.Enterprise)]
    public void A_Configured_Price_Names_Its_Plan(string priceId, TenantPlan expected) =>
        Assert.Equal(expected, Catalog.FindPlan(priceId));

    [Theory]
    [InlineData("price_1QUnknown")]
    [InlineData("PRICE_1QPRO")]
    [InlineData("")]
    public void Any_Other_Price_Has_No_Plan(string priceId) => Assert.Null(Catalog.FindPlan(priceId));

    [Fact]
    public void No_Configured_Prices_Means_No_Plans() =>
        Assert.Null(new StripePlanCatalog(Options.Create(new StripeOptions())).FindPlan("price_1QPro"));
}
