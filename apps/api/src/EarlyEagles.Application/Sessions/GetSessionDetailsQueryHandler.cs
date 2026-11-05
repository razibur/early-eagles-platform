using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Common;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class GetSessionDetailsQueryHandler : IQueryHandler<GetSessionDetailsQuery, SessionDetails>
{
    private readonly ISessionReader reader;

    public GetSessionDetailsQueryHandler(ISessionReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<SessionDetails> Handle(GetSessionDetailsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await reader.GetDetailsAsync(query.SessionId, query.ClerkUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session not found.");
    }
}
