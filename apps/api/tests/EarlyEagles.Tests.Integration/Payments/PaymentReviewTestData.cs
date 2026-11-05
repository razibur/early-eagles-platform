using EarlyEagles.Domain.Payments;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Payments;

// Seeds and reads payments for the admin review tests.
internal static class PaymentReviewTestData
{
    public static async Task SeedAsync(PostgresFixture fixture, Guid sessionId, string userId, Action<SessionPayment>? then = null)
    {
        var payment = SessionPayment.Report(Guid.CreateVersion7(), SessionApi.IotaTenantId, sessionId, userId, 1500, "A Rahman", "ZL123", DateTimeOffset.UtcNow);
        then?.Invoke(payment);
        await using var db = SessionApi.Iota(fixture);
        db.SessionPayments.Add(payment);
        await db.SaveChangesAsync();
    }

    public static async Task<SessionPayment> LoadAsync(PostgresFixture fixture, Guid sessionId, string userId)
    {
        await using var db = SessionApi.Iota(fixture);
        return await db.SessionPayments.AsNoTracking().SingleAsync(p => p.SessionId == sessionId && p.ClerkUserId == userId);
    }
}
