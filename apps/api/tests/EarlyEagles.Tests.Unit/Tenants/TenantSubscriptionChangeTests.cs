using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-043.3 — Stripe subscription events move the tenant between active, past
// due and suspended, and end the subscription. Only the tenant's current
// subscription counts; redeliveries and replaced subscriptions change nothing.
public class TenantSubscriptionChangeTests
{
    private const string Customer = "cus_NffrFeUfNV2Hib";
    private const string Subscription = "sub_1MowQVLkdIwHu7ixeRlqHVzs";
    private const string OldSubscription = "sub_1MowQVLkdIwHu7ixOLDOLDOL";

    [Fact]
    public void A_Past_Due_Tenant_Is_Reactivated_And_Raises_Subscription_Reactivated()
    {
        var tenant = NewSubscribedTenant();
        tenant.MarkPastDue();

        Assert.True(tenant.ReactivateSubscription(Subscription));

        Assert.Equal(TenantStatus.Active, tenant.Status);
        var raised = Assert.IsType<SubscriptionReactivated>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(tenant.Id, raised.TenantId);
        Assert.Equal(Subscription, raised.StripeSubscriptionId);
    }

    [Fact]
    public void A_Suspended_Tenant_Is_Reactivated()
    {
        var tenant = NewSubscribedTenant();
        tenant.Suspend();

        Assert.True(tenant.ReactivateSubscription(Subscription));

        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Reactivating_An_Active_Tenant_Raises_Nothing()
    {
        var tenant = NewSubscribedTenant();

        Assert.False(tenant.ReactivateSubscription(Subscription));

        Assert.Empty(tenant.DomainEvents);
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.PastDue)]
    public void An_Active_Or_Past_Due_Tenant_Is_Suspended_Once(TenantStatus from)
    {
        var tenant = NewSubscribedTenant();
        if (from is TenantStatus.PastDue)
        {
            tenant.MarkPastDue();
        }

        Assert.True(tenant.SuspendSubscription(Subscription));
        Assert.False(tenant.SuspendSubscription(Subscription));

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        var raised = Assert.IsType<SubscriptionSuspended>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(Subscription, raised.StripeSubscriptionId);
    }

    [Fact]
    public void Ending_Drops_The_Plan_Forgets_The_Subscription_And_Keeps_The_Customer()
    {
        var tenant = NewSubscribedTenant();
        tenant.ChangePlan(TenantPlan.Pro);
        tenant.Suspend();

        Assert.True(tenant.EndSubscription(Subscription));

        Assert.Equal(TenantPlan.Free, tenant.Plan);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.StripeSubscriptionId);
        Assert.Equal(Customer, tenant.StripeCustomerId);
        var raised = Assert.IsType<SubscriptionEnded>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(Subscription, raised.StripeSubscriptionId);
    }

    [Fact]
    public void Ending_Twice_Raises_One_Event()
    {
        var tenant = NewSubscribedTenant();

        Assert.True(tenant.EndSubscription(Subscription));
        Assert.False(tenant.EndSubscription(Subscription));

        Assert.Single(tenant.DomainEvents);
    }

    [Fact]
    public void A_Tenant_Can_Subscribe_Again_After_Ending()
    {
        var tenant = NewSubscribedTenant();
        tenant.EndSubscription(Subscription);

        Assert.True(tenant.LinkStripeSubscription(Customer, "sub_again"));

        Assert.Equal("sub_again", tenant.StripeSubscriptionId);
    }

    [Fact]
    public void Events_About_A_Replaced_Subscription_Change_Nothing()
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "replaced-club", "Replaced Club");
        tenant.LinkStripeSubscription(Customer, OldSubscription);
        tenant.LinkStripeSubscription(Customer, Subscription);
        tenant.ChangePlan(TenantPlan.Pro);
        tenant.MarkPastDue();
        tenant.ClearDomainEvents();

        Assert.False(tenant.ReactivateSubscription(OldSubscription));
        Assert.False(tenant.SuspendSubscription(OldSubscription));
        Assert.False(tenant.EndSubscription(OldSubscription));

        Assert.Equal(TenantStatus.PastDue, tenant.Status);
        Assert.Equal(TenantPlan.Pro, tenant.Plan);
        Assert.Equal(Subscription, tenant.StripeSubscriptionId);
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void A_Tenant_That_Never_Subscribed_Ignores_Subscription_Events()
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "free-club", "Free Club");

        Assert.False(tenant.SuspendSubscription(Subscription));
        Assert.False(tenant.EndSubscription(Subscription));

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void A_Deactivated_Tenant_Is_Refused()
    {
        var tenant = NewSubscribedTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.ReactivateSubscription(Subscription));
        Assert.Throws<InvalidOperationException>(() => tenant.SuspendSubscription(Subscription));
        Assert.Throws<InvalidOperationException>(() => tenant.EndSubscription(Subscription));
        Assert.Equal(TenantStatus.Deactivated, tenant.Status);
        Assert.Empty(tenant.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_Blank_Subscription_Id_Is_Refused(string subscriptionId)
    {
        var tenant = NewSubscribedTenant();

        Assert.Throws<ArgumentException>(() => tenant.EndSubscription(subscriptionId));
    }

    private static Tenant NewSubscribedTenant()
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "subscribed-club", "Subscribed Club");
        tenant.LinkStripeSubscription(Customer, Subscription);
        tenant.ClearDomainEvents();
        return tenant;
    }
}
