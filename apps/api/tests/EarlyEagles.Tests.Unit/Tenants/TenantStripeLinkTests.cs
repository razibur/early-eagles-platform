using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-045.1 — a tenant is billed to one Stripe customer; its subscription can
// be replaced, and a redelivered checkout changes nothing.
public class TenantStripeLinkTests
{
    private const string Customer = "cus_NffrFeUfNV2Hib";
    private const string Subscription = "sub_1MowQVLkdIwHu7ixeRlqHVzs";

    [Fact]
    public void A_New_Tenant_Has_No_Stripe_Ids()
    {
        var tenant = NewTenant();

        Assert.Null(tenant.StripeCustomerId);
        Assert.Null(tenant.StripeSubscriptionId);
    }

    [Fact]
    public void Linking_Records_Both_Ids()
    {
        var tenant = NewTenant();

        Assert.True(tenant.LinkStripeSubscription(Customer, Subscription));

        Assert.Equal(Customer, tenant.StripeCustomerId);
        Assert.Equal(Subscription, tenant.StripeSubscriptionId);
    }

    [Fact]
    public void Linking_The_Same_Ids_Again_Is_A_No_Op()
    {
        var tenant = NewTenant();
        tenant.LinkStripeSubscription(Customer, Subscription);

        Assert.False(tenant.LinkStripeSubscription(Customer, Subscription));
    }

    [Fact]
    public void A_New_Subscription_For_The_Same_Customer_Replaces_The_Old_One()
    {
        var tenant = NewTenant();
        tenant.LinkStripeSubscription(Customer, Subscription);

        Assert.True(tenant.LinkStripeSubscription(Customer, "sub_second"));
        Assert.Equal("sub_second", tenant.StripeSubscriptionId);
    }

    [Fact]
    public void Another_Customer_Is_Refused()
    {
        var tenant = NewTenant();
        tenant.LinkStripeSubscription(Customer, Subscription);

        Assert.Throws<InvalidOperationException>(() => tenant.LinkStripeSubscription("cus_someone_else", "sub_second"));
        Assert.Equal(Customer, tenant.StripeCustomerId);
        Assert.Equal(Subscription, tenant.StripeSubscriptionId);
    }

    [Theory]
    [InlineData("", Subscription)]
    [InlineData(" ", Subscription)]
    [InlineData(Customer, "")]
    public void Blank_Ids_Are_Refused(string customer, string subscription)
    {
        Assert.Throws<ArgumentException>(() => NewTenant().LinkStripeSubscription(customer, subscription));
    }

    [Fact]
    public void Ids_Longer_Than_The_Column_Are_Refused()
    {
        var tooLong = "cus_" + new string('x', Tenant.StripeIdMaxLength);

        Assert.Throws<ArgumentException>(() => NewTenant().LinkStripeSubscription(tooLong, Subscription));
        Assert.Throws<ArgumentException>(() => NewTenant().LinkStripeSubscription(Customer, tooLong));
    }

    [Fact]
    public void A_Deactivated_Tenant_Cannot_Be_Linked()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.LinkStripeSubscription(Customer, Subscription));
        Assert.Null(tenant.StripeCustomerId);
    }

    private static Tenant NewTenant() => Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
}
