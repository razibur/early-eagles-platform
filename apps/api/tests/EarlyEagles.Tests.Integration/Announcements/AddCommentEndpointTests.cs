using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Api.Announcements;
using EarlyEagles.Domain.Announcements;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Announcements;

// P1-012.2 — a member replies to a post in their club.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AddCommentEndpointTests : IAsyncLifetime
{
    private static readonly object Body = new { body = "Thanks" };

    private readonly PostgresFixture fixture;
    private Guid postId;

    public AddCommentEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        postId = await AnnouncementTestData.SeedPostAsync(fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Replies(string environment)
    {
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client(userId: "user_reply").PostAsJsonAsync(Route(postId), new { body = "  Thanks " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        Assert.Equal($"/announcements/{postId}/comments/{id}", response.Headers.Location?.ToString());

        await using var db = fixture.CreateContext(new TenantContext());
        var row = await db.AnnouncementComments.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.Equal(postId, row.AnnouncementId);
        Assert.Equal(SessionApi.IotaTenantId, row.TenantId);
        Assert.Equal("user_reply", row.AuthorClerkUserId);
        Assert.Equal("Thanks", row.Body);
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "AddCommentCommand" && e.Success));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_Blank_Comment_Returns_400(string? body)
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(postId), new { body });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task A_Comment_Too_Long_Returns_400()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(postId), new { body = new string('x', AnnouncementComment.BodyMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Unknown_Post_Returns_404()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client().PostAsJsonAsync(Route(Guid.NewGuid()), Body)).StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Member_Cant_Reply_To_This_Post()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsJsonAsync(Route(postId), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync(Route(postId), Body)).StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client(organization: SessionApi.KappaOrg).PostAsJsonAsync(Route(postId), Body)).StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsJsonAsync(Route(postId), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string Route(Guid id) => $"/announcements/{id}/comments";

    private async Task<int> CountAsync()
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.AnnouncementComments.IgnoreQueryFilters().CountAsync(c => c.AnnouncementId == postId);
    }
}
