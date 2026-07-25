using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-029.2 — role-change invariants on TenantMember. Per-method validation
// (undefined enum, idempotency) lives in TenantMemberTests; this file covers
// the cross-aggregate edges and pins the application-layer responsibilities
// the domain cannot enforce alone.
public class TenantMemberRoleChangeTests
{
    [Fact]
    public void Last_Admin_Demotion_Is_Enforced_By_The_Application_Service_Not_The_Aggregate()
    {
        // A TenantMember has no view of sibling members in the same tenant,
        // so the "the last TenantAdmin cannot be demoted" rule cannot live
        // here. The application service handling the role-change command
        // counts remaining admins before calling ChangeRole and rejects the
        // operation if this would be the last one. This test pins the
        // contract so a future reader does not try to lift it into the
        // domain.
        var sole = TenantMember.Create(Guid.NewGuid(), "user_admin", TenantMemberRole.TenantAdmin);

        sole.ChangeRole(TenantMemberRole.Member);

        Assert.Equal(TenantMemberRole.Member, sole.Role);
    }

    [Fact]
    public void Role_Change_Preserves_Composite_Identity()
    {
        var tenantId = Guid.NewGuid();
        var member = TenantMember.Create(tenantId, "user_abc", TenantMemberRole.Member);
        var originalId = member.Id;

        member.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(originalId, member.Id);
        Assert.Equal(tenantId, member.TenantId);
        Assert.Equal("user_abc", member.ClerkUserId);
    }

    [Fact]
    public void Multiple_Role_Changes_Resolve_To_Latest_Assignment()
    {
        var member = TenantMember.Create(Guid.NewGuid(), "user_abc", TenantMemberRole.Member);

        member.ChangeRole(TenantMemberRole.TenantAdmin);
        member.ChangeRole(TenantMemberRole.Member);
        member.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.TenantAdmin, member.Role);
    }

    [Fact]
    public void Role_Is_Independent_Across_Tenants_For_The_Same_Clerk_User()
    {
        var clerkUserId = "user_abc";
        var inAlpha = TenantMember.Create(Guid.NewGuid(), clerkUserId, TenantMemberRole.TenantAdmin);
        var inBeta = TenantMember.Create(Guid.NewGuid(), clerkUserId, TenantMemberRole.Member);

        inAlpha.ChangeRole(TenantMemberRole.Member);

        Assert.Equal(TenantMemberRole.Member, inAlpha.Role);
        Assert.Equal(TenantMemberRole.Member, inBeta.Role);

        inBeta.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.Member, inAlpha.Role);
        Assert.Equal(TenantMemberRole.TenantAdmin, inBeta.Role);
    }

    [Fact]
    public void Role_Change_Does_Not_Affect_Other_Members_In_The_Same_Tenant()
    {
        var tenantId = Guid.NewGuid();
        var alice = TenantMember.Create(tenantId, "user_alice", TenantMemberRole.TenantAdmin);
        var bob = TenantMember.Create(tenantId, "user_bob", TenantMemberRole.Member);

        alice.ChangeRole(TenantMemberRole.Member);

        Assert.Equal(TenantMemberRole.Member, alice.Role);
        Assert.Equal(TenantMemberRole.Member, bob.Role);
    }
}
