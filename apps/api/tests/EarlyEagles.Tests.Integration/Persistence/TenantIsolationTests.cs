using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.3 ★ — the headline multi-tenancy proof. Against a real Postgres seeded with
// two tenants (Alpha, Beta), a context operating "as Alpha" must never see Beta's
// rows. This is the gate: it must stay green before any tenant-scoped feature ships.
[Collection(PostgresCollectionDefinition.Name)]
public class TenantIsolationTests
{
    private readonly PostgresFixture fixture;

    public TenantIsolationTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Reading_As_Alpha_Returns_Only_Alpha_Members()
    {
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var members = await context.TenantMembers.ToListAsync();

        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal(PostgresFixture.AlphaTenantId, m.TenantId));
        Assert.DoesNotContain(members, m => m.ClerkUserId == PostgresFixture.BetaAdminUserId);
    }

    [Fact]
    public async Task Reading_As_Beta_Returns_Only_Beta_Members()
    {
        await using var context = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);

        var members = await context.TenantMembers.ToListAsync();

        var member = Assert.Single(members);
        Assert.Equal(PostgresFixture.BetaTenantId, member.TenantId);
        Assert.Equal(PostgresFixture.BetaAdminUserId, member.ClerkUserId);
    }

    [Fact]
    public async Task Alpha_Cannot_Fetch_A_Beta_Member_By_Its_Key()
    {
        // Even addressing Beta's row directly by primary key must return nothing when
        // operating as Alpha — the filter is applied, not bypassable by key lookup.
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var betaMember = await context.TenantMembers
            .FirstOrDefaultAsync(m =>
                m.TenantId == PostgresFixture.BetaTenantId &&
                m.ClerkUserId == PostgresFixture.BetaAdminUserId);

        Assert.Null(betaMember);
    }

    [Fact]
    public async Task Tenant_Aggregate_Is_Self_Scoped_Alpha_Sees_Only_Itself()
    {
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var tenant = Assert.Single(await context.Tenants.ToListAsync());

        Assert.Equal(PostgresFixture.AlphaTenantId, tenant.Id);
    }

    [Fact]
    public async Task IgnoreQueryFilters_Reveals_Both_Tenants_Proving_The_Filter_Is_What_Hides_Beta()
    {
        // Sanity backstop: Beta's data really is in the database — the isolation above
        // comes from the query filter, not from missing rows.
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var allTenants = await context.Tenants.IgnoreQueryFilters().ToListAsync();

        Assert.Contains(allTenants, t => t.Id == PostgresFixture.AlphaTenantId);
        Assert.Contains(allTenants, t => t.Id == PostgresFixture.BetaTenantId);
    }
}
