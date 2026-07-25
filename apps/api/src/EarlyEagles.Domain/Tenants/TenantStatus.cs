namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Operational state of a tenant. Drives the dunning + suspension flow
/// (P2-020) and soft-deletion (P2-022). Transitions are enforced by guards
/// on <see cref="Tenant"/>; <see cref="Deactivated"/> is terminal.
/// </summary>
public enum TenantStatus
{
    Active = 0,
    PastDue = 1,
    Suspended = 2,
    Deactivated = 3,
}
