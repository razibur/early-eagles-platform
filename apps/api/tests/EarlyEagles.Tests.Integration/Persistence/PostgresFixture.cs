using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.1 — ephemeral Postgres for the isolation gate. One container is started
// per test collection (Docker required, in CI too), the InitialCreate migration is
// applied against it, and CreateContext hands tests an ApplicationDbContext bound
// to it. Real cross-tenant isolation tests (P0-034.3/.4) build on this.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        // Apply the real EF migrations so the schema under test matches production.
        await using var context = CreateContext(new TenantContext());
        await context.Database.MigrateAsync();
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
}
