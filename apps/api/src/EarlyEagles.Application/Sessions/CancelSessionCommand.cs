using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>Cancels a session (P1-006.2). Cancelling it again changes nothing.</summary>
public sealed record CancelSessionCommand(Guid SessionId) : ICommand;
