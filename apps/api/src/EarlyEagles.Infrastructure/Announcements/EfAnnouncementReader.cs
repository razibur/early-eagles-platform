using EarlyEagles.Application.Abstractions.Announcements;
using EarlyEagles.Application.Announcements;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Announcements;

/// <summary>
/// EF <see cref="IAnnouncementReader"/>. Reads through the request's context, so
/// the tenant query filter scopes every page. The cursor is a Postgres row-value
/// comparison, as in <c>EfAuditLogReader</c>, so .NET's different
/// <see cref="Guid"/> sort order never comes into it.
/// </summary>
public sealed class EfAnnouncementReader : IAnnouncementReader
{
    private readonly ApplicationDbContext context;

    public EfAnnouncementReader(ApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<IReadOnlyList<AnnouncementItem>> ListAsync(AnnouncementCursor? after, int take, CancellationToken cancellationToken)
    {
        var query = context.Announcements.AsNoTracking();
        if (after is { } cursor)
        {
            query = query.Where(a => EF.Functions.LessThan(
                ValueTuple.Create(a.CreatedAt, a.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var posts = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var postIds = posts.Select(p => p.Id).ToList();
        var comments = await context.AnnouncementComments
            .AsNoTracking()
            .Where(c => postIds.Contains(c.AnnouncementId))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var authorIds = posts.Select(p => p.AuthorClerkUserId).Concat(comments.Select(c => c.AuthorClerkUserId)).Distinct().ToList();
        var names = await context.TenantMembers
            .AsNoTracking()
            .Where(m => authorIds.Contains(m.ClerkUserId))
            .ToDictionaryAsync(m => m.ClerkUserId, m => m.DisplayName, cancellationToken)
            .ConfigureAwait(false);

        var commentsByPost = comments.ToLookup(c => c.AnnouncementId);
        return posts
            .Select(p => new AnnouncementItem(
                p.Id,
                p.Title,
                p.Body,
                p.AuthorClerkUserId,
                names.GetValueOrDefault(p.AuthorClerkUserId),
                p.CreatedAt,
                commentsByPost[p.Id]
                    .Select(c => new AnnouncementCommentItem(c.Id, c.AuthorClerkUserId, names.GetValueOrDefault(c.AuthorClerkUserId), c.Body, c.CreatedAt))
                    .ToList()))
            .ToList();
    }
}
