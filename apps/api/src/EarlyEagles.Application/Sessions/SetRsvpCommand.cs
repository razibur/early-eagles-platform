using EarlyEagles.Domain.Sessions;
using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// A member answers for a session, or changes their answer (P1-007). Unknown
/// session: 404. Cancelled or already started on the club's clock: 409.
/// </summary>
public sealed record SetRsvpCommand(Guid SessionId, string ClerkUserId, RsvpResponse? Response) : ICommand;
