using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Infrastructure.Identity;

/// <summary>
/// Maps Clerk organization role keys to <see cref="TenantMemberRole"/>
/// (P0-036.3.2; P0-038 reuses it for claims).
/// </summary>
/// <remarks>
/// Only the admin role grants <see cref="TenantMemberRole.TenantAdmin"/>.
/// Anything else, including custom roles and a missing role, is a plain
/// member: an unrecognised role must never widen access.
/// </remarks>
public static class ClerkOrganizationRoles
{
    /// <summary>Clerk's built-in admin role key.</summary>
    public const string Admin = "org:admin";

    /// <summary>Clerk's built-in member role key.</summary>
    public const string Member = "org:member";

    // Instances created before Clerk namespaced role keys still send "admin".
    private const string LegacyAdmin = "admin";

    public static TenantMemberRole ToTenantMemberRole(string? clerkRole) =>
        clerkRole is Admin or LegacyAdmin ? TenantMemberRole.TenantAdmin : TenantMemberRole.Member;
}
