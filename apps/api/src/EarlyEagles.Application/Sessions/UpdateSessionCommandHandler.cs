using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Sessions;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class UpdateSessionCommandHandler : ICommandHandler<UpdateSessionCommand>
{
    private readonly ISessionRepository sessions;

    public UpdateSessionCommandHandler(ISessionRepository sessions)
    {
        this.sessions = sessions;
    }

    public async ValueTask<Unit> Handle(UpdateSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await sessions.FindAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");

        if (session.Status is SessionStatus.Cancelled)
        {
            throw new ConflictException("A cancelled session can't be changed.");
        }

        // The validator has already refused a missing date or time.
        if (session.UpdateDetails(
            command.Date!.Value,
            command.StartTime!.Value,
            command.Venue,
            command.FeeCents ?? session.FeeCents))
        {
            await sessions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
