using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Tenants;

// P1-007.1 — the club's zone is stored, and clubs already there get the default.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantTimeZoneColumnTests
{
    private readonly PostgresFixture fixture;

    public TenantTimeZoneColumnTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Changed_Zone_Survives_A_Round_Trip()
    {
        var id = Guid.CreateVersion7();
        var tenant = Tenant.Create(id, "zone-round-trip", "Zone Round Trip");
        tenant.ChangeTimeZone("America/Chicago");
        await using (var write = fixture.CreateContext(new TenantContext()))
        {
            write.Tenants.Add(tenant);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(new TenantContext());
        var saved = await read.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == id);

        Assert.Equal("America/Chicago", saved.TimeZoneId);
    }

    [Fact]
    public async Task A_Row_Written_Without_A_Zone_Gets_The_Default()
    {
        var id = Guid.CreateVersion7();
        await using var db = fixture.CreateContext(new TenantContext());
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO tenants (id, slug, display_name, plan, status) VALUES ({id}, 'zone-backfill', 'Zone Backfill', 'Free', 'Active')");

        var saved = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == id);

        Assert.Equal(Tenant.DefaultTimeZoneId, saved.TimeZoneId);
    }
}
