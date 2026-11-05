using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Application.Abstractions.Sessions;

/// <summary>
/// Write side for <see cref="Rsvp"/> (P1-001.2). Reads go through the tenant
/// query filter; saving runs inside the command's transaction.
/// </summary>
public interface IRsvpRepository
{
    /// <summary>This member's answer for the session, tracked for update; <c>null</c> when they haven't answered.</summary>
    Task<Rsvp?> FindAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken);

    void Add(Rsvp rsvp);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
