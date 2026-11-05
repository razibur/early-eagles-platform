using EarlyEagles.Application.Abstractions.Sessions;
using Mediator;

namespace EarlyEagles.Application.Sessions;

public sealed class ListSessionsQueryHandler : IQueryHandler<ListSessionsQuery, IReadOnlyList<SessionSummary>>
{
    private readonly ISessionReader reader;

    public ListSessionsQueryHandler(ISessionReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<IReadOnlyList<SessionSummary>> Handle(ListSessionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!IsoWeek.TryParse(query.Week, out var week))
        {
            throw new InvalidOperationException("The validator lets only a real ISO week through.");
        }

        return await reader.ListAsync(week.Monday, week.Sunday, cancellationToken).ConfigureAwait(false);
    }
}
