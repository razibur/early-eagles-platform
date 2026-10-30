using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Identity;

namespace EarlyEagles.Tests.Unit.Identity;

// P0-036.3.2 — Clerk role keys → TenantMemberRole. Only the admin key grants
// TenantAdmin; anything unrecognised stays a plain member.
public class ClerkOrganizationRolesTests
{
    [Theory]
    [InlineData("org:admin")]
    [InlineData("admin")]
    public void The_Admin_Role_Maps_To_TenantAdmin(string clerkRole) =>
        Assert.Equal(TenantMemberRole.TenantAdmin, ClerkOrganizationRoles.ToTenantMemberRole(clerkRole));

    [Theory]
    [InlineData("org:member")]
    [InlineData("basic_member")]
    [InlineData("org:billing_manager")]
    [InlineData("ORG:ADMIN")]
    [InlineData("org:admin ")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_Else_Maps_To_Member(string? clerkRole) =>
        Assert.Equal(TenantMemberRole.Member, ClerkOrganizationRoles.ToTenantMemberRole(clerkRole));
}
