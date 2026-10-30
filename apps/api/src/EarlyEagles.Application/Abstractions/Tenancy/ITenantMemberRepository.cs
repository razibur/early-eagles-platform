using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Write side for <see cref="TenantMember"/> (P0-036.3.1). Reads go through the
/// tenant query filter, so only the ambient tenant's members are visible;
/// saving runs inside the command's transaction.
/// </summary>
public interface ITenantMemberRepository
{
    /// <summary>The ambient tenant's member for this Clerk user, tracked for update; <c>null</c> when there is none.</summary>
    Task<TenantMember?> FindAsync(Guid tenantId, string clerkUserId, CancellationToken cancellationToken);

    void Add(TenantMember member);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
