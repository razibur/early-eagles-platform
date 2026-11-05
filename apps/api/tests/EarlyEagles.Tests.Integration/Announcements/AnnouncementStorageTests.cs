using EarlyEagles.Domain.Announcements;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Announcements;

// P1-001.4 — posts and replies round-trip and stay inside their club.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AnnouncementStorageTests
{
    private readonly PostgresFixture fixture;

    public AnnouncementStorageTests(PostgresFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_Post_And_Its_Comment_Round_Trip_And_Are_Visible_To_Their_Tenant_Only()
    {
        var postId = Guid.CreateVersion7();
        var commentId = Guid.CreateVersion7();
        await using (var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            alpha.Announcements.Add(Announcement.Create(postId, PostgresFixture.AlphaTenantId, "user_admin", "Courts moved", "See you at 8.", DateTimeOffset.UtcNow));
            await alpha.SaveChangesAsync();
            alpha.AnnouncementComments.Add(AnnouncementComment.Create(commentId, PostgresFixture.AlphaTenantId, postId, "user_1", "Thanks", DateTimeOffset.UtcNow));
            await alpha.SaveChangesAsync();
        }

        await using (var alphaRead = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug))
        {
            var post = await alphaRead.Announcements.SingleAsync(a => a.Id == postId);
            Assert.Equal("Courts moved", post.Title);
            Assert.Equal("See you at 8.", post.Body);
            var comment = await alphaRead.AnnouncementComments.SingleAsync(c => c.Id == commentId);
            Assert.Equal(postId, comment.AnnouncementId);
            Assert.Equal("Thanks", comment.Body);
        }

        await using var beta = fixture.CreateContext(PostgresFixture.BetaTenantId, PostgresFixture.BetaSlug);
        Assert.False(await beta.Announcements.AnyAsync(a => a.Id == postId));
        Assert.False(await beta.AnnouncementComments.AnyAsync(c => c.Id == commentId));
    }

    [Fact]
    public async Task A_Comment_On_A_Post_That_Doesnt_Exist_Is_Refused()
    {
        await using var alpha = fixture.CreateContext(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);
        alpha.AnnouncementComments.Add(AnnouncementComment.Create(Guid.CreateVersion7(), PostgresFixture.AlphaTenantId, Guid.NewGuid(), "user_1", "Thanks", DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => alpha.SaveChangesAsync());
    }
}
