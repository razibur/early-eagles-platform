using EarlyEagles.Domain.Announcements;

namespace EarlyEagles.Application.Abstractions.Announcements;

/// <summary>
/// Write side for announcements and their comments (P1-001.4). Reads go through
/// the tenant query filter; saving runs inside the command's transaction.
/// </summary>
public interface IAnnouncementRepository
{
    /// <summary>True when the ambient tenant has this announcement.</summary>
    Task<bool> ExistsAsync(Guid announcementId, CancellationToken cancellationToken);

    /// <summary>The comment on this announcement, tracked; <c>null</c> when there is none in the ambient tenant.</summary>
    Task<AnnouncementComment?> FindCommentAsync(Guid announcementId, Guid commentId, CancellationToken cancellationToken);

    void Add(Announcement announcement);

    void AddComment(AnnouncementComment comment);

    void RemoveComment(AnnouncementComment comment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
