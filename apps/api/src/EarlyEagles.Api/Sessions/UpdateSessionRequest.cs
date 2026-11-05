namespace EarlyEagles.Api.Sessions;

/// <summary>
/// The body of <c>PUT /sessions/{id}</c>: the same fields as creating one. A
/// missing <c>feeCents</c> keeps the session's current fee.
/// </summary>
public sealed record UpdateSessionRequest(DateOnly? Date, TimeOnly? StartTime, string? Venue, int? FeeCents);
