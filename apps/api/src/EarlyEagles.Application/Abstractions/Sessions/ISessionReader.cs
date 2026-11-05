using EarlyEagles.Application.Sessions;

namespace EarlyEagles.Application.Abstractions.Sessions;

/// <summary>
/// Read side for sessions. Implementations read through the tenant query
/// filter, so every result belongs to the ambient tenant.
/// </summary>
public interface ISessionReader
{
    /// <summary>Sessions from <paramref name="firstDay"/> to <paramref name="lastDay"/> inclusive, earliest first.</summary>
    Task<IReadOnlyList<SessionSummary>> ListAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken);
}
