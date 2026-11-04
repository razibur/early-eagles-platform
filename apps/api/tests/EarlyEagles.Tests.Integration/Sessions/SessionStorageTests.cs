using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-001.1 — sessions round-trip (date / time / fee / status) and are
// invisible to other tenants.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SessionStorageTests
{
    private readonly PostgresFixture fixture;

    public SessionStorageTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Session_Round_Trips_And_Is_Visible_To_Its_Tenant_Only()
    {
        var id = Guid.CreateVersion7();
        var date = new DateOnly(2026, 11, 8);
        await using (var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var session = Session.Create(id, PostgresFixture.AlphaTenantId, date, new TimeOnly(7, 15), "Edison Sports Hall", 1500, DateTimeOffset.UtcNow);
            session.Cancel();
            alpha.Sessions.Add(session);
            await alpha.SaveChangesAsync();
        }

        await using (var alphaRead = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var saved = await alphaRead.Sessions.SingleAsync(s => s.Id == id);
            Assert.Equal(date, saved.Date);
            Assert.Equal(new TimeOnly(7, 15), saved.StartTime);
            Assert.Equal("Edison Sports Hall", saved.Venue);
            Assert.Equal(1500, saved.FeeCents);
            Assert.Equal(SessionStatus.Cancelled, saved.Status);
        }

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.Sessions.AnyAsync(s => s.Id == id));
    }
}
