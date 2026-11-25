using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-045.2 — the current subscription's price moves the tenant to its plan and
// raises PlanChanged once; replaced subscriptions and redeliveries change nothing.
public class TenantPlanChangeTests
{
    private const string Customer = "cus_NffrFeUfNV2Hib";
    private const string Subscription = "sub_1MowQVLkdIwHu7ixeRlqHVzs";

    [Fact]
    public void The_Current_Subscription_Moves_The_Tenant_To_Its_Plan()
    {
        var tenant = NewSubscribedTenant();

        Assert.True(tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));

        Assert.Equal(TenantPlan.Pro, tenant.Plan);
        var raised = Assert.IsType<PlanChanged>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(tenant.Id, raised.TenantId);
        Assert.Equal(Subscription, raised.StripeSubscriptionId);
        Assert.Equal(TenantPlan.Free, raised.From);
        Assert.Equal(TenantPlan.Pro, raised.To);
    }

    [Fact]
    public void The_Same_Plan_Again_Raises_Nothing_More()
    {
        var tenant = NewSubscribedTenant();

        Assert.True(tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));
        Assert.False(tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));

        Assert.Single(tenant.DomainEvents);
    }

    [Fact]
    public void A_Downgrade_Is_A_Change_Too()
    {
        var tenant = NewSubscribedTenant();
        tenant.ChangePlan(TenantPlan.Enterprise);

        Assert.True(tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));

        var raised = Assert.IsType<PlanChanged>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(TenantPlan.Enterprise, raised.From);
    }

    [Fact]
    public void A_Replaced_Subscription_Changes_Nothing()
    {
        var tenant = NewSubscribedTenant();

        Assert.False(tenant.ChangeSubscriptionPlan("sub_replaced", TenantPlan.Pro));

        Assert.Equal(TenantPlan.Free, tenant.Plan);
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void An_Ended_Subscription_Changes_Nothing()
    {
        var tenant = NewSubscribedTenant();
        tenant.EndSubscription(Subscription);
        tenant.ClearDomainEvents();

        Assert.False(tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));

        Assert.Equal(TenantPlan.Free, tenant.Plan);
    }

    [Fact]
    public void A_Deactivated_Tenant_Is_Refused()
    {
        var tenant = NewSubscribedTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.ChangeSubscriptionPlan(Subscription, TenantPlan.Pro));
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void An_Undefined_Plan_Is_Refused()
    {
        var tenant = NewSubscribedTenant();

        Assert.Throws<ArgumentException>(() => tenant.ChangeSubscriptionPlan(Subscription, (TenantPlan)7));
    }

    private static Tenant NewSubscribedTenant()
    {
        var tenant = Tenant.Create(Guid.CreateVersion7(), "plan-club", "Plan Club");
        tenant.LinkStripeSubscription(Customer, Subscription);
        tenant.ClearDomainEvents();
        return tenant;
    }
}
