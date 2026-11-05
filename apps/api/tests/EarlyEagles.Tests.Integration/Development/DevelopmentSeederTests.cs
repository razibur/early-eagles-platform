using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Development;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Development;

// P1-004.1 — the seed fills Tenant 0 and can run again without doubling anything.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class DevelopmentSeederTests
{
    private readonly PostgresFixture fixture;

    public DevelopmentSeederTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Seeds_Tenant_Zero_And_Running_It_Again_Adds_Nothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(dataSource).Options;
        var seeder = new DevelopmentSeeder(options, TimeProvider.System);

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        await using var db = fixture.CreateContext(DevelopmentSeeder.TenantId, DevelopmentSeeder.TenantSlug);
        Assert.Equal("Early Eagles", (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == DevelopmentSeeder.TenantId)).DisplayName);

        var members = await db.TenantMembers.ToListAsync();
        Assert.Equal(DevelopmentSeeder.SampleMemberCount + 1, members.Count);
        Assert.Equal(TenantMemberRole.TenantAdmin, members.Single(m => m.ClerkUserId == DevelopmentSeeder.AdminClerkUserId).Role);
        Assert.All(members, m => Assert.EndsWith("@example.com", m.Email, StringComparison.Ordinal));

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(DayOfWeek.Sunday, session.Date.DayOfWeek);
        Assert.True(session.Date > DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
