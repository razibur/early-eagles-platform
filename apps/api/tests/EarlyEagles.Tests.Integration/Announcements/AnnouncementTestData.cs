using EarlyEagles.Domain.Announcements;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;

namespace EarlyEagles.Tests.Integration.Announcements;

// Seeds posts and comments in Iota for the announcement tests.
internal static class AnnouncementTestData
{
    public static async Task<Guid> SeedPostAsync(PostgresFixture fixture)
    {
        await using var db = SessionApi.Iota(fixture);
        var post = Announcement.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, "user_post_admin", "Seeded post", "Body", DateTimeOffset.UtcNow);
        db.Announcements.Add(post);
        await db.SaveChangesAsync();
        return post.Id;
    }

    public static async Task<Guid> SeedCommentAsync(PostgresFixture fixture, Guid postId, string authorId)
    {
        await using var db = SessionApi.Iota(fixture);
        var comment = AnnouncementComment.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, postId, authorId, "Seeded comment", DateTimeOffset.UtcNow);
        db.AnnouncementComments.Add(comment);
        await db.SaveChangesAsync();
        return comment.Id;
    }
}
