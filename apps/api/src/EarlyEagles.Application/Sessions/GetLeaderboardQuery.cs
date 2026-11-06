using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// The club's leaderboard for the year so far on its own clock (P1-011). A member
/// attended a session when they answered Yes and it started without being
/// cancelled, so a no-show still counts. <c>year</c> is the only period.
/// </summary>
public sealed record GetLeaderboardQuery(string Period) : IQuery<IReadOnlyList<LeaderboardRow>>
{
    public const string Year = "year";
}
