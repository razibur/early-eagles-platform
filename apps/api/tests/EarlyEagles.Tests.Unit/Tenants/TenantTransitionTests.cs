using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-028.3 — plan + status transition guards. Idempotent on self-transitions
// (replayed webhooks must not blow up); invalid transitions throw.
public class TenantTransitionTests
{
    [Theory]
    [InlineData(TenantPlan.Free)]
    [InlineData(TenantPlan.Pro)]
    [InlineData(TenantPlan.Enterprise)]
    public void ChangePlan_To_Defined_Value_Updates_Plan(TenantPlan plan)
    {
        var tenant = NewTenant();

        tenant.ChangePlan(plan);

        Assert.Equal(plan, tenant.Plan);
    }

    [Fact]
    public void ChangePlan_Is_Idempotent()
    {
        var tenant = NewTenant();
        tenant.ChangePlan(TenantPlan.Pro);

        tenant.ChangePlan(TenantPlan.Pro);

        Assert.Equal(TenantPlan.Pro, tenant.Plan);
    }

    [Fact]
    public void ChangePlan_Rejects_Undefined_Enum_Value()
    {
        var tenant = NewTenant();

        var ex = Assert.Throws<ArgumentException>(() => tenant.ChangePlan((TenantPlan)999));

        Assert.Equal("plan", ex.ParamName);
    }

    [Fact]
    public void ChangePlan_Throws_When_Tenant_Is_Deactivated()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.ChangePlan(TenantPlan.Pro));
    }

    [Fact]
    public void MarkPastDue_From_Active_Moves_To_PastDue()
    {
        var tenant = NewTenant();

        tenant.MarkPastDue();

        Assert.Equal(TenantStatus.PastDue, tenant.Status);
    }

    [Fact]
    public void MarkPastDue_From_PastDue_Is_Idempotent()
    {
        var tenant = NewTenant();
        tenant.MarkPastDue();

        tenant.MarkPastDue();

        Assert.Equal(TenantStatus.PastDue, tenant.Status);
    }

    [Fact]
    public void MarkPastDue_From_Suspended_Throws()
    {
        var tenant = NewTenant();
        tenant.Suspend();

        Assert.Throws<InvalidOperationException>(() => tenant.MarkPastDue());
    }

    [Fact]
    public void MarkPastDue_From_Deactivated_Throws()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.MarkPastDue());
    }

    [Fact]
    public void Suspend_From_Active_Moves_To_Suspended()
    {
        var tenant = NewTenant();

        tenant.Suspend();

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
    }

    [Fact]
    public void Suspend_From_PastDue_Moves_To_Suspended()
    {
        var tenant = NewTenant();
        tenant.MarkPastDue();

        tenant.Suspend();

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
    }

    [Fact]
    public void Suspend_From_Suspended_Is_Idempotent()
    {
        var tenant = NewTenant();
        tenant.Suspend();

        tenant.Suspend();

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
    }

    [Fact]
    public void Suspend_From_Deactivated_Throws()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.Suspend());
    }

    [Fact]
    public void Reactivate_From_PastDue_Moves_To_Active()
    {
        var tenant = NewTenant();
        tenant.MarkPastDue();

        tenant.Reactivate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Reactivate_From_Suspended_Moves_To_Active()
    {
        var tenant = NewTenant();
        tenant.Suspend();

        tenant.Reactivate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Reactivate_From_Active_Is_Idempotent()
    {
        var tenant = NewTenant();

        tenant.Reactivate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Reactivate_From_Deactivated_Throws()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.Reactivate());
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.PastDue)]
    [InlineData(TenantStatus.Suspended)]
    public void Deactivate_From_Any_Live_State_Moves_To_Deactivated(TenantStatus from)
    {
        var tenant = NewTenant();
        MoveTo(tenant, from);

        tenant.Deactivate();

        Assert.Equal(TenantStatus.Deactivated, tenant.Status);
    }

    [Fact]
    public void Deactivate_Is_Idempotent()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        tenant.Deactivate();

        Assert.Equal(TenantStatus.Deactivated, tenant.Status);
    }

    private static Tenant NewTenant() =>
        Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

    private static void MoveTo(Tenant tenant, TenantStatus target)
    {
        switch (target)
        {
            case TenantStatus.Active:
                break;
            case TenantStatus.PastDue:
                tenant.MarkPastDue();
                break;
            case TenantStatus.Suspended:
                tenant.Suspend();
                break;
            case TenantStatus.Deactivated:
                tenant.Deactivate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }
}
