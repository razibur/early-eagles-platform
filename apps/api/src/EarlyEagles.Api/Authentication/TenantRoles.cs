using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// The caller's role in the request's tenant, as a claim (P0-038.1). Present
/// only when the session's active organization is the tenant's Clerk
/// organization.
/// </summary>
public static class TenantRoles
{
    /// <summary>Claim type carrying <see cref="Admin"/> or <see cref="Member"/>.</summary>
    public const string ClaimType = "ee_tenant_role";

    public const string Admin = "tenant-admin";

    public const string Member = "member";

    public static string ToClaimValue(TenantMemberRole role) => role is TenantMemberRole.TenantAdmin ? Admin : Member;
}
