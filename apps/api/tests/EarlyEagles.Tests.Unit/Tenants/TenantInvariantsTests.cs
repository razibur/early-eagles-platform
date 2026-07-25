using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-028.4 — cross-aspect invariants for the Tenant aggregate. Slug-format,
// branding-validation, and per-transition guards live in their own focused
// test classes; this file documents the rules that emerge when those aspects
// are combined.
public class TenantInvariantsTests
{
    [Fact]
    public void Slug_Uniqueness_Is_Enforced_By_The_Persistence_Layer_Not_The_Aggregate()
    {
        // The Tenant aggregate has no view of sibling tenants, so it cannot
        // enforce slug uniqueness. Uniqueness is enforced by the unique index
        // on `tenants.slug` introduced in P0-033.1; application services
        // SHOULD pre-check before insert to surface a friendlier error, but
        // the DB constraint is the source of truth. This test pins the
        // contract so a future reader does not try to lift uniqueness into
        // the domain.
        var slug = "early-eagles";

        var a = Tenant.Create(Guid.NewGuid(), slug, "Tenant A");
        var b = Tenant.Create(Guid.NewGuid(), slug, "Tenant B");

        Assert.Equal(slug, a.Slug);
        Assert.Equal(slug, b.Slug);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void SetBranding_Throws_When_Tenant_Is_Deactivated()
    {
        var tenant = NewTenant();
        tenant.Deactivate();
        var branding = NewBranding();

        Assert.Throws<InvalidOperationException>(() => tenant.SetBranding(branding));
    }

    [Fact]
    public void SetBranding_Is_Allowed_In_PastDue_And_Suspended_States()
    {
        var pastDue = NewTenant();
        pastDue.MarkPastDue();
        var suspended = NewTenant();
        suspended.Suspend();
        var branding = NewBranding();

        pastDue.SetBranding(branding);
        suspended.SetBranding(branding);

        Assert.Equal(branding, pastDue.Branding);
        Assert.Equal(branding, suspended.Branding);
    }

    [Fact]
    public void Branding_Persists_Across_Plan_Changes()
    {
        var tenant = NewTenant();
        var branding = NewBranding();
        tenant.SetBranding(branding);

        tenant.ChangePlan(TenantPlan.Pro);
        tenant.ChangePlan(TenantPlan.Enterprise);
        tenant.ChangePlan(TenantPlan.Free);

        Assert.Equal(branding, tenant.Branding);
    }

    [Fact]
    public void Plan_Persists_Across_Status_Changes()
    {
        var tenant = NewTenant();
        tenant.ChangePlan(TenantPlan.Pro);

        tenant.MarkPastDue();
        tenant.Reactivate();
        tenant.Suspend();
        tenant.Reactivate();

        Assert.Equal(TenantPlan.Pro, tenant.Plan);
    }

    [Fact]
    public void Status_Persists_Across_Plan_Changes()
    {
        var tenant = NewTenant();
        tenant.MarkPastDue();

        tenant.ChangePlan(TenantPlan.Pro);

        Assert.Equal(TenantStatus.PastDue, tenant.Status);
    }

    [Fact]
    public void Full_Lifecycle_Tenant_Subscribes_Upgrades_Fails_Recovers_And_Deactivates()
    {
        var tenant = NewTenant();
        var branding = NewBranding();

        // Day 1 — sign up, brand, upgrade to Pro.
        tenant.SetBranding(branding);
        tenant.ChangePlan(TenantPlan.Pro);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(TenantPlan.Pro, tenant.Plan);
        Assert.Equal(branding, tenant.Branding);

        // Day 30 — payment fails, grace period begins.
        tenant.MarkPastDue();
        Assert.Equal(TenantStatus.PastDue, tenant.Status);

        // Day 32 — payment recovers.
        tenant.Reactivate();
        Assert.Equal(TenantStatus.Active, tenant.Status);

        // Day 60 — payment fails again, grace expires, suspended.
        tenant.MarkPastDue();
        tenant.Suspend();
        Assert.Equal(TenantStatus.Suspended, tenant.Status);

        // Day 65 — admin manually reactivates after invoice cleared.
        tenant.Reactivate();
        Assert.Equal(TenantStatus.Active, tenant.Status);

        // Day 365 — tenant chooses to leave the platform.
        tenant.Deactivate();
        Assert.Equal(TenantStatus.Deactivated, tenant.Status);

        // Deactivated is terminal — no further plan / branding mutations.
        Assert.Throws<InvalidOperationException>(() => tenant.ChangePlan(TenantPlan.Free));
        Assert.Throws<InvalidOperationException>(() => tenant.SetBranding(NewBranding()));
        Assert.Throws<InvalidOperationException>(() => tenant.MarkPastDue());
        Assert.Throws<InvalidOperationException>(() => tenant.Suspend());
        Assert.Throws<InvalidOperationException>(() => tenant.Reactivate());
    }

    private static Tenant NewTenant() =>
        Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

    private static Branding NewBranding() =>
        new("https://cdn.example.com/logo.png", "#1a2b3c", "#ffaa00");
}
