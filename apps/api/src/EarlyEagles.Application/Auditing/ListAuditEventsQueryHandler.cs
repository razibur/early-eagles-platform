using EarlyEagles.Application.Abstractions.Auditing;
using Mediator;

namespace EarlyEagles.Application.Auditing;

/// <summary>Fetches one page plus one row to learn whether another page exists.</summary>
public sealed class ListAuditEventsQueryHandler : IQueryHandler<ListAuditEventsQuery, AuditEventPage>
{
    private readonly IAuditLogReader reader;

    public ListAuditEventsQueryHandler(IAuditLogReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<AuditEventPage> Handle(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        AuditEventCursor.TryDecode(query.Cursor, out var after);

        // One extra row tells us whether another page exists without a COUNT.
        var rows = await reader.ListAsync(after, query.Limit + 1, cancellationToken).ConfigureAwait(false);
        if (rows.Count <= query.Limit)
        {
            return new AuditEventPage(rows, NextCursor: null);
        }

        var items = rows.Take(query.Limit).ToArray();
        var last = items[^1];
        return new AuditEventPage(items, new AuditEventCursor(last.OccurredAt, last.Id).Encode());
    }
}
