using System.Net;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Announcements;

// P1-012.3 — your own comment, or any as an admin.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class DeleteCommentEndpointTests : IAsyncLifetime
{
    private const string Author = "user_comment_author";

    private readonly PostgresFixture fixture;
    private Guid postId;
    private Guid commentId;

    public DeleteCommentEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        postId = await AnnouncementTestData.SeedPostAsync(fixture);
        commentId = await AnnouncementTestData.SeedCommentAsync(fixture, postId, Author);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task The_Author_Deletes_Their_Own_Comment_And_It_Is_Audited(string environment)
    {
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client(userId: Author).DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await ExistsAsync());
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "DeleteCommentCommand" && e.Success));
    }

    [Fact]
    public async Task An_Admin_Deletes_Anyones_Comment()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", userId: "user_moderator").DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await ExistsAsync());
    }

    [Fact]
    public async Task Another_Member_Gets_403_And_The_Comment_Stays()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_someone_else").DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await ExistsAsync());
    }

    [Fact]
    public async Task Deleting_It_Twice_Returns_404()
    {
        using var api = new SessionApi(fixture);
        var client = api.Client(userId: Author);

        await client.DeleteAsync(Url());
        var second = await client.DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task A_Comment_Named_Under_The_Wrong_Post_Returns_404_And_Stays()
    {
        var otherPost = await AnnouncementTestData.SeedPostAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").DeleteAsync(new Uri($"/announcements/{otherPost}/comments/{commentId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(await ExistsAsync());
    }

    [Fact]
    public async Task Another_Clubs_Admin_Finds_Nothing_And_The_Comment_Stays()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(await ExistsAsync());
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().DeleteAsync(Url())).StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).DeleteAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Uri Url() => new($"/announcements/{postId}/comments/{commentId}", UriKind.Relative);

    private async Task<bool> ExistsAsync()
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.AnnouncementComments.IgnoreQueryFilters().AnyAsync(c => c.Id == commentId);
    }
}
