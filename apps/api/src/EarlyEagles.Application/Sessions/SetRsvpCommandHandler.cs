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
    private readonly ITenantRepository tenants;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public SetRsvpCommandHandler(
        ISessionRepository sessions,
        IRsvpRepository rsvps,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        this.sessions = sessions;
        this.rsvps = rsvps;
        this.tenants = tenants;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<Unit> Handle(SetRsvpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await sessions.FindAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");

        var tenant = await tenants.FindAsync(tenantContext.TenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The ambient tenant has no row.");

        var now = timeProvider.GetUtcNow();
        if (!session.AcceptsRsvps(tenant.ToClubTime(now)))
        {
            throw new ConflictException(session.Status is SessionStatus.Cancelled
                ? "This session was cancelled."
                : "This session has already started.");
        }

        // The validator has refused a missing answer by now.
        var response = command.Response!.Value;
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
