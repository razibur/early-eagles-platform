using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Announcements;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Announcements;

// P1-013 — the feed, newest first, keyset paged. The other announcement tests
// write to Iota, so the feed's posts live in Kappa, where only these tests post:
// the sessions feature is on for every club in Development.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AnnouncementFeedEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture fixture;
    private readonly List<Guid> expectedOrder = [];

    public AnnouncementFeedEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);

        await using var kappa = SessionApi.Kappa(fixture);
        if (!await kappa.Announcements.AnyAsync())
        {
            var member = TenantMember.Create(SessionApi.KappaTenantId, "user_feed_karim", TenantMemberRole.Member);
            member.UpdateProfile("Feed Karim", null, null);
            kappa.TenantMembers.Add(member);

            // Five posts, two sharing a timestamp so the id tie-break is exercised.
            var posts = new[]
            {
                NewPost("Oldest", T0),
                NewPost("Second", T0.AddHours(1)),
                NewPost("Tied A", T0.AddHours(2)),
                NewPost("Tied B", T0.AddHours(2)),
                NewPost("Newest", T0.AddHours(3)),
            };
            kappa.Announcements.AddRange(posts);
            await kappa.SaveChangesAsync();

            var newest = posts[^1].Id;
            kappa.AnnouncementComments.AddRange(
                AnnouncementComment.Create(Guid.CreateVersion7(), SessionApi.KappaTenantId, newest, "user_feed_gone", "Second reply", T0.AddHours(5)),
                AnnouncementComment.Create(Guid.CreateVersion7(), SessionApi.KappaTenantId, newest, "user_feed_karim", "First reply", T0.AddHours(4)));
            await kappa.SaveChangesAsync();
        }

        // Expected order computed by Postgres: uuid ordering differs from .NET's.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM announcements WHERE tenant_id = @t ORDER BY created_at DESC, id DESC", connection);
        command.Parameters.AddWithValue("t", SessionApi.KappaTenantId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            expectedOrder.Add(reader.GetGuid(0));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Pages_Through_The_Whole_Feed_Newest_First_Without_Gaps_Or_Duplicates()
    {
        using var api = new SessionApi(fixture);
        var client = Kappa(api);

        var seen = new List<Guid>();
        var pageSizes = new List<int>();
        string? cursor = null;
        do
        {
            var url = cursor is null ? "/announcements?limit=2" : $"/announcements?limit=2&cursor={cursor}";
            var page = await client.GetFromJsonAsync<PageDto>(new Uri(url, UriKind.Relative));
            pageSizes.Add(page!.Items.Count);
            seen.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal([2, 2, 1], pageSizes);
        Assert.Equal(expectedOrder, seen);
    }

    [Fact]
    public async Task A_Post_Comes_With_Its_Replies_Oldest_First_And_Names_Where_Known()
    {
        using var api = new SessionApi(fixture);

        var page = await Kappa(api).GetFromJsonAsync<PageDto>(new Uri("/announcements", UriKind.Relative));

        Assert.Null(page!.NextCursor);
        var newest = page.Items[0];
        Assert.Equal("Newest", newest.Title);
        Assert.Equal("Body of Newest", newest.Body);
        Assert.Equal("Feed Karim", newest.AuthorName);
        Assert.Equal(["First reply", "Second reply"], newest.Comments.Select(c => c.Body));
        Assert.Equal("Feed Karim", newest.Comments[0].AuthorName);
        Assert.Null(newest.Comments[1].AuthorName);
        Assert.Empty(page.Items[^1].Comments);
    }

    [Fact]
    public async Task A_Member_Of_Another_Club_Sees_None_Of_These_Posts()
    {
        await AnnouncementTestData.SeedPostAsync(fixture);
        using var api = new SessionApi(fixture, "Production");

        var page = await api.Client().GetFromJsonAsync<PageDto>(new Uri("/announcements?limit=50", UriKind.Relative));

        Assert.NotEmpty(page!.Items);
        Assert.DoesNotContain(page.Items, i => expectedOrder.Contains(i.Id));
    }

    [Theory]
    [InlineData("/announcements?limit=0")]
    [InlineData("/announcements?limit=51")]
    [InlineData("/announcements?cursor=not-a-cursor")]
    public async Task Invalid_Paging_Input_Returns_400(string url)
    {
        using var api = new SessionApi(fixture);

        var response = await Kappa(api).GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(new Uri("/announcements", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg).GetAsync(new Uri("/announcements", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await Kappa(api).GetAsync(new Uri("/announcements", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Announcement NewPost(string title, DateTimeOffset at) =>
        Announcement.Create(Guid.CreateVersion7(), SessionApi.KappaTenantId, "user_feed_karim", title, "Body of " + title, at);

    private static HttpClient Kappa(SessionApi api) => api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug);

    private sealed record PageDto(IReadOnlyList<ItemDto> Items, string? NextCursor);

    private sealed record ItemDto(Guid Id, string Title, string Body, string AuthorClerkUserId, string? AuthorName, DateTimeOffset CreatedAt, IReadOnlyList<CommentDto> Comments);

    private sealed record CommentDto(Guid Id, string AuthorClerkUserId, string? AuthorName, string Body, DateTimeOffset CreatedAt);
}
