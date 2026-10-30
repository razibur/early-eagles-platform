using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-029.1 — TenantMember skeleton. Identity is composite (TenantId, ClerkUserId);
// role-change invariants (e.g. "last admin cannot demote") land in P0-029.2 as
// application-layer docs tests.
public class TenantMemberTests
{
    [Fact]
    public void Create_With_Valid_Inputs_Round_Trips_Properties()
    {
        var tenantId = Guid.NewGuid();

        var member = TenantMember.Create(tenantId, "user_abc123", TenantMemberRole.Member);

        Assert.Equal(tenantId, member.TenantId);
        Assert.Equal("user_abc123", member.ClerkUserId);
        Assert.Equal(TenantMemberRole.Member, member.Role);
        Assert.Equal(new TenantMemberId(tenantId, "user_abc123"), member.Id);
    }

    [Fact]
    public void Two_Members_With_Same_TenantId_And_ClerkUserId_Are_Equal()
    {
        var tenantId = Guid.NewGuid();
        var a = TenantMember.Create(tenantId, "user_abc", TenantMemberRole.Member);
        var b = TenantMember.Create(tenantId, "user_abc", TenantMemberRole.TenantAdmin);

        // Role differs, but composite identity matches — entities are equal.
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Same_ClerkUserId_In_Different_Tenants_Is_Different_Member()
    {
        var clerkUserId = "user_abc";
        var a = TenantMember.Create(Guid.NewGuid(), clerkUserId, TenantMemberRole.Member);
        var b = TenantMember.Create(Guid.NewGuid(), clerkUserId, TenantMemberRole.Member);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Different_ClerkUserId_In_Same_Tenant_Is_Different_Member()
    {
        var tenantId = Guid.NewGuid();
        var a = TenantMember.Create(tenantId, "user_abc", TenantMemberRole.Member);
        var b = TenantMember.Create(tenantId, "user_xyz", TenantMemberRole.Member);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Create_Rejects_Empty_TenantId()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => TenantMember.Create(Guid.Empty, "user_abc", TenantMemberRole.Member));

        Assert.Equal("tenantId", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Rejects_Empty_Or_Whitespace_ClerkUserId(string clerkUserId)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => TenantMember.Create(Guid.NewGuid(), clerkUserId, TenantMemberRole.Member));

        Assert.Equal("clerkUserId", ex.ParamName);
    }

    [Fact]
    public void Create_Rejects_ClerkUserId_Exceeding_Max_Length()
    {
        var tooLong = new string('a', TenantMember.ClerkUserIdMaxLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => TenantMember.Create(Guid.NewGuid(), tooLong, TenantMemberRole.Member));

        Assert.Equal("clerkUserId", ex.ParamName);
    }

    [Fact]
    public void Create_Rejects_Undefined_Role()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => TenantMember.Create(Guid.NewGuid(), "user_abc", (TenantMemberRole)999));

        Assert.Equal("role", ex.ParamName);
    }

    [Fact]
    public void ChangeRole_Updates_Role()
    {
        var member = TenantMember.Create(Guid.NewGuid(), "user_abc", TenantMemberRole.Member);

        member.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.TenantAdmin, member.Role);
    }

    [Fact]
    public void ChangeRole_Is_Idempotent()
    {
        var member = TenantMember.Create(Guid.NewGuid(), "user_abc", TenantMemberRole.TenantAdmin);

        member.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.TenantAdmin, member.Role);
    }

    [Fact]
    public void ChangeRole_Rejects_Undefined_Value()
    {
        var member = TenantMember.Create(Guid.NewGuid(), "user_abc", TenantMemberRole.Member);

        var ex = Assert.Throws<ArgumentException>(
            () => member.ChangeRole((TenantMemberRole)999));

        Assert.Equal("role", ex.ParamName);
    }

    [Fact]
    public void Is_An_Entity_With_Composite_Identity()
    {
        // TenantMember is an Entity, not an AggregateRoot — sealed inheritance
        // from Entity<TenantMemberId> is what disqualifies it at compile time.
        // It does not raise its own domain events; membership transitions
        // travel through the Tenant aggregate when needed.
        var member = TenantMember.Create(Guid.NewGuid(), "user_abc", TenantMemberRole.Member);

        Assert.IsAssignableFrom<Entity<TenantMemberId>>(member);
    }

    [Theory]
    [InlineData("user_abc", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidClerkUserId_Agrees_With_Create(string? clerkUserId, bool expected)
    {
        Assert.Equal(expected, TenantMember.IsValidClerkUserId(clerkUserId));
        AssertCreateAgrees(clerkUserId, expected);
    }

    [Theory]
    [InlineData(TenantMember.ClerkUserIdMaxLength, true)]
    [InlineData(TenantMember.ClerkUserIdMaxLength + 1, false)]
    public void IsValidClerkUserId_Agrees_With_Create_At_The_Length_Limit(int length, bool expected)
    {
        var clerkUserId = new string('u', length);

        Assert.Equal(expected, TenantMember.IsValidClerkUserId(clerkUserId));
        AssertCreateAgrees(clerkUserId, expected);
    }

    private static void AssertCreateAgrees(string? clerkUserId, bool valid)
    {
        var create = () => TenantMember.Create(Guid.NewGuid(), clerkUserId!, TenantMemberRole.Member);
        if (valid)
        {
            Assert.NotNull(create());
        }
        else
        {
            Assert.ThrowsAny<ArgumentException>(create);
        }
    }
}
