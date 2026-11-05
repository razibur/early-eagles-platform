using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-001.3 — payments round-trip, stay inside their club, hold to one per member
// per session, and their events reach the outbox with the change.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SessionPaymentStorageTests
{
    private readonly PostgresFixture fixture;

    public SessionPaymentStorageTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Payment_Round_Trips_Is_Visible_To_Its_Tenant_Only_And_Queues_Its_Events()
    {
        var sessionId = await SeedSessionAsync();
        var id = Guid.CreateVersion7();
        await using (var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            alpha.SessionPayments.Add(NewPayment(id, sessionId, "user_pay"));
            await alpha.SaveChangesAsync();
        }

        await using (var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var payment = await alpha.SessionPayments.SingleAsync(p => p.Id == id);
            payment.Confirm("user_admin", DateTimeOffset.UtcNow);
            await alpha.SaveChangesAsync();
        }

        await using (var alphaRead = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var saved = await alphaRead.SessionPayments.SingleAsync(p => p.Id == id);
            Assert.Equal(PaymentStatus.Confirmed, saved.Status);
            Assert.Equal("A Rahman", saved.SenderName);
            Assert.Equal("ZL123", saved.Reference);
            Assert.Equal(1500, saved.AmountCents);
            Assert.Equal("user_admin", saved.ReviewedBy);

            var types = await alphaRead.OutboxMessages.IgnoreQueryFilters()
                .Where(m => m.Payload.Contains(id.ToString()))
                .Select(m => m.EventType)
                .ToListAsync();
            Assert.Contains(typeof(PaymentReported).FullName, types);
            Assert.Contains(typeof(PaymentConfirmed).FullName, types);
        }

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.SessionPayments.AnyAsync(p => p.Id == id));
    }

    [Fact]
    public async Task A_Second_Payment_From_The_Same_Member_Is_Refused()
    {
        var sessionId = await SeedSessionAsync();
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        alpha.SessionPayments.Add(NewPayment(Guid.CreateVersion7(), sessionId, "user_twice"));
        alpha.SessionPayments.Add(NewPayment(Guid.CreateVersion7(), sessionId, "user_twice"));

        await Assert.ThrowsAsync<DbUpdateException>(() => alpha.SaveChangesAsync());
    }

    private static SessionPayment NewPayment(Guid id, Guid sessionId, string userId) =>
        SessionPayment.Report(id, PostgresFixture.AlphaTenantId, sessionId, userId, 1500, "A Rahman", "ZL123", DateTimeOffset.UtcNow);

    private async Task<Guid> SeedSessionAsync()
    {
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        var session = Session.Create(Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, new DateOnly(2033, 3, 6), new TimeOnly(7, 0), "Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
        alpha.Sessions.Add(session);
        await alpha.SaveChangesAsync();
        return session.Id;
    }
}
