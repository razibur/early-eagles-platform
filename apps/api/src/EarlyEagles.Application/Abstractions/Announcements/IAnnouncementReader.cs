using EarlyEagles.Application.Announcements;

namespace EarlyEagles.Application.Abstractions.Announcements;

/// <summary>
/// Read side of the feed. Implementations read through the tenant query filter,
/// so every result belongs to the ambient tenant.
/// </summary>
public interface IAnnouncementReader
{
    /// <summary>
    /// Up to <paramref name="take"/> posts strictly older than
    /// <paramref name="after"/> (or the newest, when it is <c>null</c>), ordered
    /// by <c>CreatedAt</c> then <c>Id</c>, both descending.
    /// </summary>
    Task<IReadOnlyList<AnnouncementItem>> ListAsync(AnnouncementCursor? after, int take, CancellationToken cancellationToken);
}
