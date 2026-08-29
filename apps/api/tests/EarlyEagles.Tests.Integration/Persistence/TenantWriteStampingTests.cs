using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Persistence.Interceptors;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.4 ★ — the write side of the isolation gate. P0-034.3 proves a tenant
// cannot *read* another tenant's rows; these prove it cannot *write* them either.
// The TenantStampingInterceptor stamps the ambient tenant onto inserts that lack
// one and rejects any write that names a different tenant.
[Collection(PostgresCollectionDefinition.Name)]
public class TenantWriteStampingTests
{
    private readonly PostgresFixture fixture;

    public TenantWriteStampingTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Insert_Without_A_Tenant_Id_Is_Stamped_From_The_Ambient_Tenant()
    {
        const string clerkUserId = "user_stamped_from_context";

        try
        {
            await using (var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
            {
                // Stand in for a handler that built an entity without setting the
                // tenant: the row reaches SaveChanges with no tenant id at all.
                var member = TenantMember.Create(
                    PostgresFixture.AlphaTenantId,
                    clerkUserId,
                    TenantMemberRole.Member);

                context.TenantMembers.Add(member);
                ClearTenantId(context, member);

                await context.SaveChangesAsync();

                Assert.Equal(PostgresFixture.AlphaTenantId, member.TenantId);
            }

            // And it really landed in Alpha: Alpha sees the row, Beta does not.
            Assert.NotNull(await ReadMemberAsAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug, clerkUserId));
            Assert.Null(await ReadMemberAsAsync(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug, clerkUserId));
        }
        finally
        {
            await DeleteMemberAsync(clerkUserId);
        }
    }

    [Fact]
    public async Task Insert_Carrying_Another_Tenants_Id_Is_Rejected()
    {
        const string clerkUserId = "user_cross_tenant_insert";

        try
        {
            await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

            context.TenantMembers.Add(TenantMember.Create(
                PostgresFixture.BetaTenantId,
                clerkUserId,
                TenantMemberRole.Member));

            await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());

            // The save was refused before it reached the database.
            Assert.Null(await ReadMemberAsAsync(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug, clerkUserId));
        }
        finally
        {
            await DeleteMemberAsync(clerkUserId);
        }
    }

    [Fact]
    public async Task Insert_With_The_Ambient_Tenant_Id_Is_Allowed()
    {
        const string clerkUserId = "user_ordinary_insert";

        try
        {
            await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

            context.TenantMembers.Add(TenantMember.Create(
                PostgresFixture.AlphaTenantId,
                clerkUserId,
                TenantMemberRole.Member));

            await context.SaveChangesAsync();

            Assert.NotNull(await ReadMemberAsAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug, clerkUserId));
        }
        finally
        {
            await DeleteMemberAsync(clerkUserId);
        }
    }

    [Fact]
    public async Task Insert_Without_A_Tenant_Id_And_Without_A_Resolved_Tenant_Is_Rejected()
    {
        // Nothing to stamp from. Rather than write a row into nobody's tenant, the
        // interceptor refuses the save.
        const string clerkUserId = "user_no_tenant_anywhere";

        try
        {
            await using var context = fixture.CreateContext(new TenantContext());

            var member = TenantMember.Create(
                PostgresFixture.AlphaTenantId,
                clerkUserId,
                TenantMemberRole.Member);

            context.TenantMembers.Add(member);
            ClearTenantId(context, member);

            await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());
        }
        finally
        {
            await DeleteMemberAsync(clerkUserId);
        }
    }

    [Fact]
    public async Task Insert_With_An_Explicit_Tenant_Id_And_No_Resolved_Tenant_Is_Allowed()
    {
        // The write-side twin of IgnoreQueryFilters: seeding, provisioning and
        // background jobs run outside a request and must name the tenant themselves.
        const string clerkUserId = "user_seeded_out_of_band";

        try
        {
            await using (var context = fixture.CreateContext(new TenantContext()))
            {
                context.TenantMembers.Add(TenantMember.Create(
                    PostgresFixture.BetaTenantId,
                    clerkUserId,
                    TenantMemberRole.Member));

                await context.SaveChangesAsync();
            }

            Assert.NotNull(await ReadMemberAsAsync(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug, clerkUserId));
        }
        finally
        {
            await DeleteMemberAsync(clerkUserId);
        }
    }

    [Fact]
    public async Task Update_Of_Another_Tenants_Row_Is_Rejected()
    {
        // The query filter means Alpha cannot load Beta's member, so the only way
        // here is attaching a detached instance — which the interceptor catches.
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var betaMember = TenantMember.Create(
            PostgresFixture.BetaTenantId,
            PostgresFixture.BetaAdminUserId,
            TenantMemberRole.TenantAdmin);

        betaMember.ChangeRole(TenantMemberRole.Member);
        context.TenantMembers.Update(betaMember);

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());

        // Beta's row is untouched.
        var stillAdmin = await ReadMemberAsAsync(
            PostgresFixture.BetaTenantId,
            PostgresFixture.BetaSlug,
            PostgresFixture.BetaAdminUserId);

        Assert.Equal(TenantMemberRole.TenantAdmin, stillAdmin!.Role);
    }

    [Fact]
    public async Task Delete_Of_Another_Tenants_Row_Is_Rejected()
    {
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        context.TenantMembers.Remove(TenantMember.Create(
            PostgresFixture.BetaTenantId,
            PostgresFixture.BetaAdminUserId,
            TenantMemberRole.TenantAdmin));

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());

        Assert.NotNull(await ReadMemberAsAsync(
            PostgresFixture.BetaTenantId,
            PostgresFixture.BetaSlug,
            PostgresFixture.BetaAdminUserId));
    }

    [Fact]
    public async Task Update_Of_Another_Tenants_Tenant_Row_Is_Rejected()
    {
        // The Tenant aggregate is self-scoped: its tenant id is its own key. Alpha
        // must not be able to edit Beta.
        await using var context = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        var beta = Tenant.Create(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug, "Beta Club");
        context.Tenants.Update(beta);

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Provisioning_A_New_Tenant_Without_A_Resolved_Tenant_Is_Allowed()
    {
        // Creating a tenant happens before that tenant can be ambient, so the
        // interceptor deliberately leaves inserts of the self-scoped aggregate alone.
        var gammaId = Guid.NewGuid();

        try
        {
            await using (var context = fixture.CreateContext(new TenantContext()))
            {
                context.Tenants.Add(Tenant.Create(gammaId, "gamma", "Gamma Club"));
                await context.SaveChangesAsync();
            }

            await using var reader = fixture.CreateContext(gammaId, "gamma");
            Assert.NotNull(await reader.Tenants.SingleOrDefaultAsync());
        }
        finally
        {
            await using var cleanup = fixture.CreateContext(new TenantContext());
            await cleanup.Tenants.IgnoreQueryFilters().Where(t => t.Id == gammaId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// Blanks the tenant id on a tracked, not-yet-saved entity — the state a handler
    /// leaves behind when it forgets to set it. Done through the change tracker
    /// because the domain factory rightly refuses an empty tenant id.
    /// </summary>
    private static void ClearTenantId(ApplicationDbContext context, TenantMember member) =>
        context.Entry(member).Property(nameof(TenantMember.TenantId)).CurrentValue = Guid.Empty;

    private async Task<TenantMember?> ReadMemberAsAsync(Guid tenantId, string slug, string clerkUserId)
    {
        await using var context = fixture.CreateContext(tenantId, slug);
        return await context.TenantMembers.SingleOrDefaultAsync(m => m.ClerkUserId == clerkUserId);
    }

    private async Task DeleteMemberAsync(string clerkUserId)
    {
        // ExecuteDelete bypasses the change tracker (and so the interceptor); it is
        // scoped here by IgnoreQueryFilters plus an explicit predicate, which is
        // exactly the deliberate out-of-band access this cleanup needs.
        await using var context = fixture.CreateContext(new TenantContext());
        await context.TenantMembers
            .IgnoreQueryFilters()
            .Where(m => m.ClerkUserId == clerkUserId)
            .ExecuteDeleteAsync();
    }
}
