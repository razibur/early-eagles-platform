namespace EarlyEagles.Application.Sessions;

/// <summary>One member on the leaderboard (P1-011). <c>LastPlayed</c> is <c>null</c> until they attend.</summary>
public sealed record LeaderboardRow(
    string ClerkUserId,
    string? DisplayName,
    string? ImageUrl,
    int SessionsAttended,
    int TimesPaid,
    DateOnly? LastPlayed);
