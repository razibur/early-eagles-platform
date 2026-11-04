using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Application.Abstractions.Sessions;

/// <summary>
/// Write side for <see cref="Session"/> (P1-001.1). Reads go through the
/// tenant query filter, so only the ambient tenant's sessions are visible;
/// saving runs inside the command's transaction.
/// </summary>
public interface ISessionRepository
{
    /// <summary>The ambient tenant's session, tracked for update; <c>null</c> when there is none (or it is another club's).</summary>
    Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken);

    void Add(Session session);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
