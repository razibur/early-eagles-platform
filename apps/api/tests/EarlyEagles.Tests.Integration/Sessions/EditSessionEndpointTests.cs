using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-006.2 — an admin edits or cancels their own club's session.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class EditSessionEndpointTests : IAsyncLifetime
{
    private static readonly object Edit = new { date = "2032-01-11", startTime = "08:30", venue = "New Hall", feeCents = 2000 };

    private readonly PostgresFixture fixture;

    public EditSessionEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync() => SessionApi.SeedClubsAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Edits_A_Session(string environment)
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client("admin").PutAsJsonAsync($"/sessions/{id}", Edit);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await LoadAsync(id);
        Assert.Equal(new DateOnly(2032, 1, 11), saved.Date);
        Assert.Equal(new TimeOnly(8, 30), saved.StartTime);
        Assert.Equal("New Hall", saved.Venue);
        Assert.Equal(2000, saved.FeeCents);
    }

    [Fact]
    public async Task Without_A_Fee_The_Current_Fee_Stays()
    {
        var id = await SeedAsync(feeCents: 1800);
        using var api = new SessionApi(fixture);

        await api.Client("admin").PutAsJsonAsync($"/sessions/{id}", new { date = "2032-01-11", startTime = "08:30", venue = "New Hall" });

        Assert.Equal(1800, (await LoadAsync(id)).FeeCents);
    }

    [Fact]
    public async Task An_Admin_Cancels_A_Session_And_Doing_It_Again_Is_Harmless()
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture);
        var client = api.Client("admin");

        var first = await client.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null);
        var second = await client.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(SessionStatus.Cancelled, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task A_Cancelled_Session_Cant_Be_Edited_And_The_Attempt_Is_Audited_As_Failed()
    {
        var id = await SeedAsync(cancelled: true);
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PutAsJsonAsync($"/sessions/{id}", Edit);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Old Hall", (await LoadAsync(id)).Venue);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "UpdateSessionCommand" && !e.Success));
    }

    [Fact]
    public async Task An_Invalid_Edit_Returns_400_And_Changes_Nothing()
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PutAsJsonAsync($"/sessions/{id}", new { date = "2032-01-11", startTime = "08:30", venue = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Old Hall", (await LoadAsync(id)).Venue);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);
        var client = api.Client("admin");
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/sessions/{id}", Edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Session_Returns_404_And_Stays_As_It_Was()
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture);
        var kappaAdmin = api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug);

        var edit = await kappaAdmin.PutAsJsonAsync($"/sessions/{id}", Edit);
        var cancel = await kappaAdmin.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        var saved = await LoadAsync(id);
        Assert.Equal("Old Hall", saved.Venue);
        Assert.Equal(SessionStatus.Scheduled, saved.Status);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture);
        var client = api.Anonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/sessions/{id}", Edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403_And_Changes_Nothing()
    {
        var id = await SeedAsync();
        using var api = new SessionApi(fixture);
        var member = api.Client();

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/sessions/{id}", Edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync(new Uri($"/sessions/{id}/cancel", UriKind.Relative), null)).StatusCode);
        Assert.Equal(SessionStatus.Scheduled, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");
        var kappaAdmin = api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug);

        var response = await kappaAdmin.PostAsync(new Uri($"/sessions/{Guid.NewGuid()}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> SeedAsync(int feeCents = Session.DefaultFeeCents, bool cancelled = false)
    {
        var session = Session.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, new DateOnly(2032, 1, 4), new TimeOnly(7, 0), "Old Hall", feeCents, DateTimeOffset.UtcNow);
        if (cancelled)
        {
            session.Cancel();
        }

        await using var db = SessionApi.Iota(fixture);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Session> LoadAsync(Guid id)
    {
        await using var db = SessionApi.Iota(fixture);
        return await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == id);
    }
}
