using EarlyEagles.Application.Abstractions.Announcements;
using Mediator;

namespace EarlyEagles.Application.Announcements;

/// <summary>Fetches one page plus one row to learn whether another page exists.</summary>
public sealed class ListAnnouncementsQueryHandler : IQueryHandler<ListAnnouncementsQuery, AnnouncementPage>
{
    private readonly IAnnouncementReader reader;

    public ListAnnouncementsQueryHandler(IAnnouncementReader reader)
    {
        this.reader = reader;
    }

    public async ValueTask<AnnouncementPage> Handle(ListAnnouncementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        AnnouncementCursor.TryDecode(query.Cursor, out var after);

        var rows = await reader.ListAsync(after, query.Limit + 1, cancellationToken).ConfigureAwait(false);
        if (rows.Count <= query.Limit)
        {
            return new AnnouncementPage(rows, NextCursor: null);
        }

        var items = rows.Take(query.Limit).ToArray();
        var last = items[^1];
        return new AnnouncementPage(items, new AnnouncementCursor(last.CreatedAt, last.Id).Encode());
    }
}
