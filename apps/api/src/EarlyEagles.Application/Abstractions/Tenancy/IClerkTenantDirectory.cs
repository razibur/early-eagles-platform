namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Platform-level lookup used before a tenant context exists (P0-036.2.3): the
/// Clerk webhook has to learn which tenant an organization already is, and
/// whether the slug it wants belongs to someone else. Reads across tenants
/// (bypassing the query filter) and returns ids and slugs only.
/// </summary>
public interface IClerkTenantDirectory
{
    Task<ClerkTenantDirectoryEntry> FindAsync(string clerkOrganizationId, string slug, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant linked to the organization, or <c>null</c> when it hasn't been
    /// provisioned (yet: Clerk doesn't guarantee delivery order). Used by the
    /// membership and user events (P0-036.3.2 onward).
    /// </summary>
    Task<ClerkLinkedTenant?> FindTenantAsync(string clerkOrganizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Every tenant the Clerk user is a member of (P0-036.5.1): <c>user.updated</c>
    /// patches the profile in each. Empty when the user is in no club.
    /// </summary>
    Task<IReadOnlyList<ClerkLinkedTenant>> FindTenantsOfUserAsync(string clerkUserId, CancellationToken cancellationToken);
}
