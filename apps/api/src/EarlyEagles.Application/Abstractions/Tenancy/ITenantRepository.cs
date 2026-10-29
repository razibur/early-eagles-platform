using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Write side for the <see cref="Tenant"/> aggregate (P0-036.2.2). Reads go
/// through the tenant query filter, so only the ambient tenant's own row is
/// visible; saving runs inside the command's transaction.
/// </summary>
public interface ITenantRepository
{
    /// <summary>The ambient tenant's row, tracked for update; <c>null</c> when it doesn't exist yet.</summary>
    Task<Tenant?> FindAsync(Guid tenantId, CancellationToken cancellationToken);

    void Add(Tenant tenant);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
