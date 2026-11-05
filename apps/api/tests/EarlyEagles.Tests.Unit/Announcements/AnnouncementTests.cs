using EarlyEagles.Domain.Announcements;

namespace EarlyEagles.Tests.Unit.Announcements;

// P1-001.4 — posts and replies hold trimmed, bounded text.
public class AnnouncementTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void An_Announcement_Trims_Its_Text()
    {
        var announcement = Announcement.Create(Guid.NewGuid(), Tenant, "user_admin", "  Courts moved ", "  See you at 8. ", T0);

        Assert.Equal("Courts moved", announcement.Title);
        Assert.Equal("See you at 8.", announcement.Body);
        Assert.Equal("user_admin", announcement.AuthorClerkUserId);
        Assert.Equal(T0, announcement.CreatedAt);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("   ", "body")]
    [InlineData("title", "")]
    [InlineData("title", "   ")]
    public void An_Announcement_Refuses_Blank_Text(string title, string body)
    {
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.NewGuid(), Tenant, "user_admin", title, body, T0));
    }

    [Fact]
    public void An_Announcement_Takes_Text_Up_To_Its_Limits_And_No_Further()
    {
        var title = new string('t', Announcement.TitleMaxLength);
        var body = new string('b', Announcement.BodyMaxLength);

        Assert.Equal(title, Announcement.Create(Guid.NewGuid(), Tenant, "user_admin", title, body, T0).Title);
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.NewGuid(), Tenant, "user_admin", title + "t", body, T0));
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.NewGuid(), Tenant, "user_admin", title, body + "b", T0));
    }

    [Fact]
    public void An_Announcement_Refuses_Empty_Ids_And_A_Blank_Author()
    {
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.Empty, Tenant, "user_admin", "t", "b", T0));
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.NewGuid(), Guid.Empty, "user_admin", "t", "b", T0));
        Assert.Throws<ArgumentException>(() => Announcement.Create(Guid.NewGuid(), Tenant, " ", "t", "b", T0));
    }

    [Fact]
    public void A_Comment_Trims_Its_Text_And_Keeps_To_Its_Limit()
    {
        var announcementId = Guid.NewGuid();
        var comment = AnnouncementComment.Create(Guid.NewGuid(), Tenant, announcementId, "user_1", "  Thanks ", T0);

        Assert.Equal("Thanks", comment.Body);
        Assert.Equal(announcementId, comment.AnnouncementId);
        Assert.Throws<ArgumentException>(() => AnnouncementComment.Create(Guid.NewGuid(), Tenant, announcementId, "user_1", "  ", T0));
        Assert.Throws<ArgumentException>(() =>
            AnnouncementComment.Create(Guid.NewGuid(), Tenant, announcementId, "user_1", new string('x', AnnouncementComment.BodyMaxLength + 1), T0));
    }

    [Fact]
    public void A_Comment_Refuses_Empty_Ids_And_A_Blank_Author()
    {
        var announcementId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => AnnouncementComment.Create(Guid.Empty, Tenant, announcementId, "user_1", "b", T0));
        Assert.Throws<ArgumentException>(() => AnnouncementComment.Create(Guid.NewGuid(), Guid.Empty, announcementId, "user_1", "b", T0));
        Assert.Throws<ArgumentException>(() => AnnouncementComment.Create(Guid.NewGuid(), Tenant, Guid.Empty, "user_1", "b", T0));
        Assert.Throws<ArgumentException>(() => AnnouncementComment.Create(Guid.NewGuid(), Tenant, announcementId, " ", "b", T0));
    }
}
