using System.Security.Claims;
using EarlyEagles.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Integration.Authentication;

// P0-038.2 — the tenant policies as the app registers them, evaluated by its
// own IAuthorizationService. The HTTP 401 / 403 path is covered where a policy
// guards a real endpoint (/audit-events, P0-048.2).
public sealed class TenantPoliciesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public TenantPoliciesTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Theory]
    [InlineData(TenantPolicies.Member, TenantRoles.Member, true)]
    [InlineData(TenantPolicies.Member, TenantRoles.Admin, true)]
    [InlineData(TenantPolicies.Admin, TenantRoles.Admin, true)]
    [InlineData(TenantPolicies.Admin, TenantRoles.Member, false)]
    [InlineData(TenantPolicies.Member, null, false)]
    [InlineData(TenantPolicies.Admin, null, false)]
    [InlineData(TenantPolicies.Admin, "owner", false)]
    public async Task A_Signed_In_User_Is_Allowed_Only_With_The_Right_Tenant_Role(string policy, string? role, bool allowed)
    {
        var claims = new List<Claim> { new("sub", "user_policy_check") };
        if (role is not null)
        {
            claims.Add(new Claim(TenantRoles.ClaimType, role));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));

        Assert.Equal(allowed, await AuthorizeAsync(user, policy));
    }

    [Theory]
    [InlineData(TenantPolicies.Member)]
    [InlineData(TenantPolicies.Admin)]
    public async Task A_Role_Claim_Without_A_Signed_In_User_Is_Not_Enough(string policy)
    {
        // No authentication type → not authenticated, whatever the claims say.
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TenantRoles.ClaimType, TenantRoles.Admin)]));

        Assert.False(await AuthorizeAsync(user, policy));
    }

    private async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string policy)
    {
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(user, resource: null, policy)).Succeeded;
    }
}
