using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.1 — ephemeral Postgres for the isolation gate. One container is started
// per test collection (Docker required, in CI too), the InitialCreate migration is
// applied against it, and CreateContext hands tests an ApplicationDbContext bound
// to it.
// P0-034.2 — the same fixture seeds two tenants (Alpha, Beta) with members each, so
// the isolation tests (P0-034.3) have cross-tenant data to prove nothing leaks.
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AlphaSlug = "alpha";
    public const string BetaSlug = "beta";
    public const string AlphaAdminUserId = "user_alpha_admin";
    public const string AlphaMemberUserId = "user_alpha_member";
    public const string BetaAdminUserId = "user_beta_admin";

    public static readonly Guid AlphaTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid BetaTenantId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        // Apply the real EF migrations so the schema under test matches production.
        await using var context = CreateContext(new TenantContext());
        await context.Database.MigrateAsync();

        await SeedAsync(context);
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>
    /// Builds an <see cref="ApplicationDbContext"/> against the container, scoped to
    /// the supplied tenant context (pass an unresolved one for cross-tenant setup).
    /// </summary>
    public ApplicationDbContext CreateContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    /// <summary>
    /// Builds a context whose ambient tenant is resolved to <paramref name="tenantId"/> —
    /// i.e. "operate as this tenant". The global query filter then scopes every read.
    /// </summary>
    public ApplicationDbContext CreateContext(Guid tenantId, string slug)
    {
        var tenantContext = new TenantContext();
        tenantContext.Initialize(tenantId, slug);
        return CreateContext(tenantContext);
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        // Inserts are not touched by the query filter, so the (unresolved) migration
        // context is fine for seeding both tenants' data in one go.
        context.Tenants.AddRange(
            Tenant.Create(AlphaTenantId, AlphaSlug, "Alpha Club"),
            Tenant.Create(BetaTenantId, BetaSlug, "Beta Club"));

        context.TenantMembers.AddRange(
            TenantMember.Create(AlphaTenantId, AlphaAdminUserId, TenantMemberRole.TenantAdmin),
            TenantMember.Create(AlphaTenantId, AlphaMemberUserId, TenantMemberRole.Member),
            TenantMember.Create(BetaTenantId, BetaAdminUserId, TenantMemberRole.TenantAdmin));

        await context.SaveChangesAsync();
    }
}
