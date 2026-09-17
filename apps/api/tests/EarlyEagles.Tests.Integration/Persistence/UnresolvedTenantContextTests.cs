using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.5 ★ — the read-side "default deny". A context whose ambient tenant is
// unresolved must see nothing on any ITenantScoped entity: the global query filter
// compares each row's tenant discriminator against Guid.Empty, so no seeded row
// matches. This closes the gap the resolution middleware would otherwise leave —
// if middleware misfires or is bypassed, queries fail closed, not open.
[Collection(PostgresCollectionDefinition.Name)]
public class UnresolvedTenantContextTests
{
    private readonly PostgresFixture fixture;

    public UnresolvedTenantContextTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Reading_TenantMembers_With_Unresolved_Context_Returns_Empty()
    {
        await using var context = fixture.CreateContext(new TenantContext());

        var members = await context.TenantMembers.ToListAsync();

        Assert.Empty(members);
    }

    [Fact]
    public async Task Reading_Tenants_With_Unresolved_Context_Returns_Empty()
    {
        // Tenant is self-scoped (TenantId => Id), so the filter still keys off
        // Guid.Empty and hides every seeded row.
        await using var context = fixture.CreateContext(new TenantContext());

        var tenants = await context.Tenants.ToListAsync();

        Assert.Empty(tenants);
    }

    [Fact]
    public async Task Direct_Key_Lookup_With_Unresolved_Context_Returns_Null()
    {
        // Filter isn't bypassable by addressing a known primary key.
        await using var context = fixture.CreateContext(new TenantContext());

        var alphaAdmin = await context.TenantMembers
            .FirstOrDefaultAsync(m =>
                m.TenantId == PostgresFixture.AlphaTenantId &&
                m.ClerkUserId == PostgresFixture.AlphaAdminUserId);

        Assert.Null(alphaAdmin);
    }

    [Fact]
    public async Task IgnoreQueryFilters_Reveals_The_Rows_Proving_The_Filter_Is_What_Hides_Them()
    {
        // Sanity backstop: the seeded rows really are in the database — the empty
        // results above come from the filter, not from missing data.
        await using var context = fixture.CreateContext(new TenantContext());

        var allMembers = await context.TenantMembers.IgnoreQueryFilters().ToListAsync();

        Assert.Contains(allMembers, m => m.ClerkUserId == PostgresFixture.AlphaAdminUserId);
        Assert.Contains(allMembers, m => m.ClerkUserId == PostgresFixture.BetaAdminUserId);
    }
}
