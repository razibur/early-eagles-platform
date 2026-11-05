using EarlyEagles.Application.Abstractions.Sessions;
using EarlyEagles.Application.Common;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class GetRsvpGridQueryHandler : IQueryHandler<GetRsvpGridQuery, IReadOnlyList<RsvpGridRow>>
{
    private readonly ISessionReader reader;

    public GetRsvpGridQueryHandler(ISessionReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<IReadOnlyList<RsvpGridRow>> Handle(GetRsvpGridQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await reader.ExistsAsync(query.SessionId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException("Session not found.");
        }

        return await reader.ListRsvpGridAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
    }
}
