using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Sessions;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResult>
{
    private readonly ISessionRepository sessions;
    private readonly ITenantContext tenantContext;
    private readonly TimeProvider timeProvider;

    public CreateSessionCommandHandler(ISessionRepository sessions, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        this.sessions = sessions;
        this.tenantContext = tenantContext;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<CreateSessionResult> Handle(CreateSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // A session belongs to a club; without one there is nowhere to file it.
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("CreateSessionCommand must run with the ambient tenant resolved.");
        }

        // The validator has already refused a missing date or time.
        var session = Session.Create(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            command.Date!.Value,
            command.StartTime!.Value,
            command.Venue,
            command.FeeCents ?? Session.DefaultFeeCents,
            timeProvider.GetUtcNow());
        sessions.Add(session);
        await sessions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateSessionResult(session.Id);
    }
}
