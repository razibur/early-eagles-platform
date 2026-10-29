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
}
