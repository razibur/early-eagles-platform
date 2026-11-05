using System.Diagnostics.CodeAnalysis;
using EarlyEagles.Application.Auditing;

namespace EarlyEagles.Application.Announcements;

/// <summary>
/// Position in the feed for keyset paging (P1-013): the last post a page
/// returned. Shares <see cref="AuditEventCursor"/>'s opaque token, which is the
/// same timestamp-and-id pair. It carries no tenant: the query filter scopes
/// every page.
/// </summary>
public readonly record struct AnnouncementCursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode() => new AuditEventCursor(CreatedAt, Id).Encode();

    public static bool TryDecode(string? token, [NotNullWhen(true)] out AnnouncementCursor? cursor)
    {
        cursor = null;
        if (!AuditEventCursor.TryDecode(token, out var decoded))
        {
            return false;
        }

        cursor = new AnnouncementCursor(decoded.Value.OccurredAt, decoded.Value.Id);
        return true;
    }
}
