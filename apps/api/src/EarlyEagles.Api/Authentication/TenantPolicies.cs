using Microsoft.AspNetCore.Authorization;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// Authorization policies over the request's tenant (P0-038.2). Both need a
/// signed-in Clerk session (none → 401) whose active organization is this
/// tenant, so <see cref="TenantRoleClaimsTransformation"/> granted it a role
/// (otherwise → 403).
/// </summary>
public static class TenantPolicies
{
    /// <summary>Any member of the request's tenant, admins included.</summary>
    public const string Member = "TenantMember";

    /// <summary>An admin of the request's tenant.</summary>
    public const string Admin = "TenantAdmin";

    public static AuthorizationBuilder AddTenantPolicies(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddPolicy(Member, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(TenantRoles.ClaimType, TenantRoles.Member, TenantRoles.Admin))
            .AddPolicy(Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(TenantRoles.ClaimType, TenantRoles.Admin));
    }
}
