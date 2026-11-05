namespace EarlyEagles.Api.Sessions;

/// <summary>
/// The body of <c>POST /sessions</c>: <c>date</c> as <c>YYYY-MM-DD</c>,
/// <c>startTime</c> as <c>HH:mm</c>, both the club's local time, and the
/// <c>venue</c>. <c>feeCents</c> is optional and defaults to $15.
/// </summary>
public sealed record CreateSessionRequest(DateOnly? Date, TimeOnly? StartTime, string? Venue, int? FeeCents);
