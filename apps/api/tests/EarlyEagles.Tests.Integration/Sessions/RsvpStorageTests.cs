using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-001.2 — answers round-trip, stay inside their club, and the database holds
// the line at one per member per session.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RsvpStorageTests
{
    private readonly PostgresFixture fixture;

    public RsvpStorageTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task An_Answer_Round_Trips_And_Is_Visible_To_Its_Tenant_Only()
    {
        var sessionId = await SeedSessionAsync();
        var id = Guid.CreateVersion7();
        await using (var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            alpha.Rsvps.Add(Rsvp.Create(id, PostgresFixture.AlphaTenantId, sessionId, "user_rsvp", RsvpResponse.Maybe, DateTimeOffset.UtcNow));
            await alpha.SaveChangesAsync();
        }

        await using (var alphaRead = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var saved = await alphaRead.Rsvps.SingleAsync(r => r.Id == id);
            Assert.Equal(sessionId, saved.SessionId);
            Assert.Equal("user_rsvp", saved.ClerkUserId);
            Assert.Equal(RsvpResponse.Maybe, saved.Response);
        }

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.Rsvps.AnyAsync(r => r.Id == id));
    }

    [Fact]
    public async Task A_Second_Answer_From_The_Same_Member_Is_Refused()
    {
        var sessionId = await SeedSessionAsync();
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        alpha.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, sessionId, "user_twice", RsvpResponse.Yes, DateTimeOffset.UtcNow));
        alpha.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, sessionId, "user_twice", RsvpResponse.No, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => alpha.SaveChangesAsync());
    }

    private async Task<Guid> SeedSessionAsync()
    {
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var session = Session.Create(Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, new DateOnly(2033, 1, 2), new TimeOnly(7, 0), "Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
        alpha.Sessions.Add(session);
        await alpha.SaveChangesAsync();
        return session.Id;
    }
}
