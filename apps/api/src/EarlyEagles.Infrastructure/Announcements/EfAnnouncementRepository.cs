using EarlyEagles.Application.Abstractions.Announcements;
using EarlyEagles.Domain.Announcements;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Announcements;

/// <summary>EF Core <see cref="IAnnouncementRepository"/> on the request's context.</summary>
public sealed class EfAnnouncementRepository : IAnnouncementRepository
{
    private readonly ApplicationDbContext context;

    public EfAnnouncementRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<bool> ExistsAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        RequireTenant();
        return context.Announcements.AnyAsync(a => a.Id == announcementId, cancellationToken);
    }

    public Task<AnnouncementComment?> FindCommentAsync(Guid announcementId, Guid commentId, CancellationToken cancellationToken)
    {
        RequireTenant();
        return context.AnnouncementComments.SingleOrDefaultAsync(c => c.Id == commentId && c.AnnouncementId == announcementId, cancellationToken);
    }

    public void Add(Announcement announcement) => context.Announcements.Add(announcement);

    public void AddComment(AnnouncementComment comment) => context.AnnouncementComments.Add(comment);

    public void RemoveComment(AnnouncementComment comment) => context.AnnouncementComments.Remove(comment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    // A context created before the tenant resolved filters on no tenant and
    // would report everything as missing.
    private void RequireTenant()
    {
        if (context.CurrentTenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The request's DbContext has no tenant. Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }
    }
}
