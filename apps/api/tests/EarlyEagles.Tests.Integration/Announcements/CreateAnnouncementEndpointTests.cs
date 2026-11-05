using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Api.Announcements;
using EarlyEagles.Domain.Announcements;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Announcements;

// P1-012 — an admin posts to their club.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CreateAnnouncementEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture fixture;

    public CreateAnnouncementEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync() => SessionApi.SeedClubsAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Posts_To_Their_Club(string environment)
    {
        var title = NewTitle();
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client("admin", userId: "user_post_admin").PostAsJsonAsync(AnnouncementEndpoints.Route, new { title, body = "See you at 8." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        Assert.Equal($"/announcements/{id}", response.Headers.Location?.ToString());

        await using var db = fixture.CreateContext(new TenantContext());
        var row = await db.Announcements.IgnoreQueryFilters().SingleAsync(a => a.Title == title);
        Assert.Equal(id, row.Id);
        Assert.Equal(SessionApi.IotaTenantId, row.TenantId);
        Assert.Equal("user_post_admin", row.AuthorClerkUserId);
        Assert.Equal("See you at 8.", row.Body);
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "CreateAnnouncementCommand" && e.Success));
    }

    [Theory]
    [InlineData(null, "body", "Title")]
    [InlineData("   ", "body", "Title")]
    [InlineData("title", null, "Body")]
    [InlineData("title", "   ", "Body")]
    public async Task A_Blank_Field_Returns_400_Naming_It(string? title, string? body, string field)
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsJsonAsync(AnnouncementEndpoints.Route, new { title, body });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Body_Too_Long_Returns_400()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsJsonAsync(
            AnnouncementEndpoints.Route,
            new { title = "title", body = new string('x', Announcement.BodyMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Anonymous().PostAsJsonAsync(AnnouncementEndpoints.Route, new { title = NewTitle(), body = "b" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403_And_Posts_Nothing()
    {
        var title = NewTitle();
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(AnnouncementEndpoints.Route, new { title, body = "b" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNotStored(title);
    }

    [Fact]
    public async Task An_Admin_Of_Another_Club_Gets_403_And_Posts_Nothing()
    {
        var title = NewTitle();
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg).PostAsJsonAsync(AnnouncementEndpoints.Route, new { title, body = "b" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNotStored(title);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404_And_Posts_Nothing()
    {
        var title = NewTitle();
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .PostAsJsonAsync(AnnouncementEndpoints.Route, new { title, body = "b" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotStored(title);
    }

    private static string NewTitle() => "Post " + Guid.NewGuid().ToString("N");

    private async Task AssertNotStored(string title)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.Announcements.IgnoreQueryFilters().AnyAsync(a => a.Title == title));
    }
}
