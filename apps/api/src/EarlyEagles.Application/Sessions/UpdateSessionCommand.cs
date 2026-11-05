using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// Changes a session's date, time, venue or fee (P1-006.2). A <c>null</c> fee
/// keeps the current one. Unknown here or in another club: 404. Cancelled: 409.
/// </summary>
public sealed record UpdateSessionCommand(
    Guid SessionId,
    DateOnly? Date,
    TimeOnly? StartTime,
    string Venue,
    int? FeeCents) : ICommand;
