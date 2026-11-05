using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Common;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class CancelSessionCommandHandler : ICommandHandler<CancelSessionCommand>
{
    private readonly ISessionRepository sessions;

    public CancelSessionCommandHandler(ISessionRepository sessions)
    {
        this.sessions = sessions;
    }

    public async ValueTask<Unit> Handle(CancelSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await sessions.FindAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");

        if (session.Cancel())
        {
            await sessions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
