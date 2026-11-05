using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// Creates a session in the ambient tenant (P1-006). The date and time are the
/// club's local wall clock. A <c>null</c> fee means the default $15; a
/// <c>null</c> date or time is refused by the validator.
/// </summary>
public sealed record CreateSessionCommand(
    DateOnly? Date,
    TimeOnly? StartTime,
    string Venue,
    int? FeeCents) : ICommand<CreateSessionResult>;
