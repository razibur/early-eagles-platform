namespace EarlyEagles.Domain.Primitives;

/// <summary>
/// Marks a domain entity as belonging to exactly one tenant. This is the single
/// source of truth for "which rows are tenant data": the EF Core global query
/// filter (P0-032.2) reads <see cref="TenantId"/> to constrain every query to the
/// ambient tenant, and the insert path stamps it, so no handler can read or write
/// across tenant boundaries by accident.
/// </summary>
/// <remarks>
/// Applied to <see cref="EarlyEagles.Domain.Tenants.Tenant"/> and
/// <see cref="EarlyEagles.Domain.Tenants.TenantMember"/> today; <c>AuditEvent</c>
/// implements it when it lands in P0-046.1, and every future tenant entity must
/// implement it (enforced by an architecture test).
/// </remarks>
public interface ITenantScoped
{
    /// <summary>
    /// Owning tenant. For the <see cref="EarlyEagles.Domain.Tenants.Tenant"/>
    /// aggregate itself this is its own <see cref="Entity{TId}.Id"/> — a tenant is
    /// scoped to itself; for every other entity it is a reference to the owning
    /// tenant.
    /// </summary>
    Guid TenantId { get; }
}
