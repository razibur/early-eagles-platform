using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-032.2 — the ApplicationDbContext global query filter. Verified against the
// real Npgsql provider via ToQueryString(), which compiles the model and emits
// SQL without touching a database, so these run in CI with no Postgres. The
// generated SQL must scope every tenant-scoped entity to the current tenant.
public class TenantQueryFilterTests
{
    private const string ConnectionString =
        "Host=localhost;Database=ee_test;Username=test;Password=test";

    [Fact]
    public void TenantMembers_Query_Filters_On_Tenant_Id_Column()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);

        var sql = context.TenantMembers.ToQueryString();

        Assert.Contains("tenant_id", sql, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tenants_Query_Filters_On_Its_Own_Id()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);

        var sql = context.Tenants.ToQueryString();

        // The Tenant aggregate is scoped to itself: filter is on the id column.
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tenantId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unresolved_Tenant_Scopes_Queries_To_Empty_Guid()
    {
        // No tenant resolved → filter compares against Guid.Empty, which matches
        // no row, so a context without a tenant reads nothing rather than leaking.
        using var context = CreateContext(resolvedTenantId: null);

        var sql = context.TenantMembers.ToQueryString();

        Assert.Contains(Guid.Empty.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filter_Reevaluates_Per_Context_Instance()
    {
        // The model is cached per context type, but the filter references the
        // DbContext instance member, so each context must scope to its own tenant
        // rather than the one that first built the model.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        using var firstContext = CreateContext(first);
        using var secondContext = CreateContext(second);

        var firstSql = firstContext.TenantMembers.ToQueryString();
        var secondSql = secondContext.TenantMembers.ToQueryString();

        Assert.Contains(first.ToString(), firstSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.ToString(), secondSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(second.ToString(), firstSql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext(Guid? resolvedTenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options, new FakeTenantContext(resolvedTenantId));
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        private readonly Guid? tenantId;

        public FakeTenantContext(Guid? tenantId) => this.tenantId = tenantId;

        public bool IsResolved => tenantId is not null;

        public Guid TenantId => tenantId ?? throw new InvalidOperationException("Not resolved.");

        public string Slug => tenantId is not null
            ? "test-tenant"
            : throw new InvalidOperationException("Not resolved.");

        public string? ClerkOrganizationId => null;
    }
}
