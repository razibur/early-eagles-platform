namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Subscription tier a tenant is on. Drives Stripe billing (P0-042.*) and
/// in-app feature gating (e.g. AI Coach quotas in Phase 5).
/// </summary>
public enum TenantPlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2,
}
