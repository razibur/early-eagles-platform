using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Application.Abstractions.Payments;

/// <summary>
/// Write side for <see cref="SessionPayment"/> (P1-001.3). Reads go through the
/// tenant query filter; saving runs inside the command's transaction.
/// </summary>
public interface ISessionPaymentRepository
{
    /// <summary>This member's payment for the session, tracked for update; <c>null</c> when they haven't reported one.</summary>
    Task<SessionPayment?> FindAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken);

    void Add(SessionPayment payment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
