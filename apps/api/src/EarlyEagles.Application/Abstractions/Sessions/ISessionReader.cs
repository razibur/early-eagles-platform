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

    /// <summary>The session as <paramref name="clerkUserId"/> sees it; <c>null</c> when the ambient tenant has no such session.</summary>
    Task<SessionDetails?> GetDetailsAsync(Guid sessionId, string clerkUserId, CancellationToken cancellationToken);

    /// <summary>True when the ambient tenant has this session.</summary>
    Task<bool> ExistsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Every member with their answer for the session, by name; unnamed members last.</summary>
    Task<IReadOnlyList<RsvpGridRow>> ListRsvpGridAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Every member's standing for the calendar year of <paramref name="clubLocalNow"/>,
    /// most sessions attended first, then most paid, then by name; unnamed members
    /// last. Cancelled sessions count for nothing. A payment counts for any session
    /// in the year, so paying ahead shows straight away.
    /// </summary>
    Task<IReadOnlyList<LeaderboardRow>> ListLeaderboardAsync(DateTime clubLocalNow, CancellationToken cancellationToken);
}
