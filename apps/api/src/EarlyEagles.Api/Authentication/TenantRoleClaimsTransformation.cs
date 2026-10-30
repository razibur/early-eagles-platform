using System.Security.Claims;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// Adds the <see cref="TenantRoles.ClaimType"/> claim to an authenticated
/// Clerk session (P0-038.1), from the role in the token's active organization.
/// </summary>
/// <remarks>
/// <para>The role counts only when that organization <b>is</b> the request's
/// tenant. Without the check, an admin of one club would be an admin on every
/// club's subdomain. A tenant not linked to Clerk, no active organization, or
/// another club's organization → no role claim.</para>
/// <para>The role comes from the token, not <c>tenant_members</c>: Clerk owns
/// roles, and a session token is at most 60 s old. Roles map through
/// <see cref="ClerkOrganizationRoles"/>: only <c>org:admin</c> is an admin.</para>
/// <para>Runs inside <c>UseAuthentication</c>, after tenant resolution. ASP.NET
/// Core may call it more than once per request, so it leaves a principal that
/// already has the claim alone.</para>
/// </remarks>
public sealed class TenantRoleClaimsTransformation : IClaimsTransformation
{
    private readonly ITenantContext tenant;

    public TenantRoleClaimsTransformation(ITenantContext tenant) => this.tenant = tenant;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true
            || !tenant.IsResolved
            || tenant.ClerkOrganizationId is null
            || principal.HasClaim(c => c.Type == TenantRoles.ClaimType))
        {
            return Task.FromResult(principal);
        }

        var organization = ClerkActiveOrganization.From(principal);
        if (organization is null || !string.Equals(organization.Id, tenant.ClerkOrganizationId, StringComparison.Ordinal))
        {
            return Task.FromResult(principal);
        }

        var role = TenantRoles.ToClaimValue(ClerkOrganizationRoles.ToTenantMemberRole(organization.Role));
        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity([new Claim(TenantRoles.ClaimType, role)]));
        return Task.FromResult(transformed);
    }
}
