using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Common;
using EarlyEagles.Domain.Sessions;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class SetRsvpCommandHandler : ICommandHandler<SetRsvpCommand>
{
    private readonly ISessionRepository sessions;
    private readonly IRsvpRepository rsvps;
    private readonly IClubClock clubClock;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public SetRsvpCommandHandler(
        ISessionRepository sessions,
        IRsvpRepository rsvps,
        IClubClock clubClock,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        this.sessions = sessions;
        this.rsvps = rsvps;
        this.clubClock = clubClock;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<Unit> Handle(SetRsvpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await sessions.FindAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");

        if (!session.AcceptsRsvps(await clubClock.GetNowAsync(cancellationToken).ConfigureAwait(false)))
        {
            throw new ConflictException(session.Status is SessionStatus.Cancelled
                ? "This session was cancelled."
                : "This session has already started.");
        }

        // The validator has refused a missing answer by now.
        var response = command.Response!.Value;
        var now = timeProvider.GetUtcNow();
        var rsvp = await rsvps.FindAsync(command.SessionId, command.ClerkUserId, cancellationToken).ConfigureAwait(false);
        if (rsvp is null)
        {
            rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), tenantContext.TenantId, session.Id, command.ClerkUserId, response, now));
        }
        else if (!rsvp.Change(response, now))
        {
            return Unit.Value;
        }

        await rsvps.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
