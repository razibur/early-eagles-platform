using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Persistence;

// P0-034.1 — proves the shared Postgres fixture actually stands up a database and
// that the InitialCreate migration is applied against it.
[Collection(PostgresCollectionDefinition.Name)]
public class PostgresFixtureSmokeTests
{
    private readonly PostgresFixture fixture;

    public PostgresFixtureSmokeTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Container_Is_Reachable_And_Migration_Applied()
    {
        await using var context = fixture.CreateContext(new TenantContext());

        Assert.True(await context.Database.CanConnectAsync());

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, id => id.EndsWith("InitialCreate", StringComparison.Ordinal));
    }
}
