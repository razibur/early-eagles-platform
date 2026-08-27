using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenancy;

// P0-032.1 — the ITenantScoped marker is the single source of truth the EF Core
// global query filter (P0-032.2) keys off. These tests pin that the existing
// tenant entities carry the marker and expose the right TenantId, and — as the
// standing multi-tenancy gate — that no concrete domain entity can be added
// without opting into tenant scoping.
public class TenantScopedMarkerTests
{
    [Fact]
    public void Tenant_Implements_ITenantScoped_With_TenantId_As_Its_Own_Id()
    {
        var id = Guid.NewGuid();
        var tenant = Tenant.Create(id, "early-eagles", "Early Eagles");

        Assert.IsAssignableFrom<ITenantScoped>(tenant);
        Assert.Equal(id, ((ITenantScoped)tenant).TenantId);
    }

    [Fact]
    public void TenantMember_Implements_ITenantScoped_With_TenantId_From_Composite_Id()
    {
        var tenantId = Guid.NewGuid();
        var member = TenantMember.Create(tenantId, "user_123", TenantMemberRole.Member);

        Assert.IsAssignableFrom<ITenantScoped>(member);
        Assert.Equal(tenantId, ((ITenantScoped)member).TenantId);
    }

    [Fact]
    public void Every_Concrete_Domain_Entity_Implements_ITenantScoped()
    {
        // Multi-tenancy is non-negotiable: a new domain entity that forgets the
        // marker would silently escape the global query filter. Fail loudly here
        // rather than leak rows across tenants. A deliberately platform-level
        // entity (none exist yet) would need an explicit allow-list entry.
        var offenders = typeof(Domain.AssemblyMarker).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(IsDomainEntity)
            .Where(t => !typeof(ITenantScoped).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Domain entities missing ITenantScoped: {string.Join(", ", offenders)}");
    }

    private static bool IsDomainEntity(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}
