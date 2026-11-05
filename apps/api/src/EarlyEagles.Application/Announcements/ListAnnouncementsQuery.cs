using Mediator;

namespace EarlyEagles.Application.Announcements;

/// <summary>The club's feed, newest first, keyset paged (P1-013).</summary>
public sealed record ListAnnouncementsQuery(int Limit, string? Cursor) : IQuery<AnnouncementPage>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
