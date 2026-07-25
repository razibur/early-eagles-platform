namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Role of a Clerk user within a single tenant. Maps to Clerk org roles
/// via the claim transformer in P0-038. Tenant-level authorisation policies
/// resolve against this enum.
/// </summary>
public enum TenantMemberRole
{
    Member = 0,
    TenantAdmin = 1,
}
