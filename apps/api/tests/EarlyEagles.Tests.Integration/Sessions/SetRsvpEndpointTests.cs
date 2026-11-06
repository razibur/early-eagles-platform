using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-007 — a member answers for a session until it starts on the club's clock.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SetRsvpEndpointTests : IAsyncLifetime
{
    private static readonly object Yes = new { response = "Yes" };

    private readonly PostgresFixture fixture;

    public SetRsvpEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync() => SessionApi.SeedClubsAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Answers_And_It_Is_Stored_And_Audited(string environment)
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client(userId: "user_rsvp_1").PutAsJsonAsync(Route(sessionId), new { response = "Maybe" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(RsvpResponse.Maybe, (await LoadAsync(sessionId, "user_rsvp_1")).Response);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "SetRsvpCommand" && e.Success));
    }

    [Fact]
    public async Task Changing_Your_Mind_Updates_The_Same_Answer()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);
        var client = api.Client(userId: "user_rsvp_change");
        await client.PutAsJsonAsync(Route(sessionId), Yes);
        var first = await LoadAsync(sessionId, "user_rsvp_change");

        var response = await client.PutAsJsonAsync(Route(sessionId), new { response = "No" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await LoadAsync(sessionId, "user_rsvp_change");
        Assert.Equal(first.Id, saved.Id);
        Assert.Equal(RsvpResponse.No, saved.Response);
    }

    [Fact]
    public async Task The_Same_Answer_Again_Changes_Nothing()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);
        var client = api.Client(userId: "user_rsvp_same");
        await client.PutAsJsonAsync(Route(sessionId), Yes);
        var first = await LoadAsync(sessionId, "user_rsvp_same");

        var response = await client.PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(first.UpdatedAt, (await LoadAsync(sessionId, "user_rsvp_same")).UpdatedAt);
    }

    [Fact]
    public async Task A_Double_Tap_Leaves_One_Answer_And_No_Server_Error()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);
        var client = api.Client(userId: "user_rsvp_tap");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PutAsJsonAsync(Route(sessionId), Yes)));

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(RsvpResponse.Yes, (await LoadAsync(sessionId, "user_rsvp_tap")).Response);
    }

    [Fact]
    public async Task Before_The_Start_On_The_Clubs_Clock_Is_Open()
    {
        // New York is behind UTC, so this has already started on a UTC clock.
        var sessionId = await SeedSessionStartingAsync(TimeSpan.FromHours(1));
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_rsvp_open").PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task After_The_Start_On_The_Clubs_Clock_Returns_409()
    {
        var sessionId = await SeedSessionStartingAsync(TimeSpan.FromHours(-1));
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_rsvp_late").PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(await FindAsync(sessionId, "user_rsvp_late"));
    }

    [Fact]
    public async Task A_Cancelled_Session_Returns_409()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture, cancelled: true);
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_rsvp_cancelled").PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(await FindAsync(sessionId, "user_rsvp_cancelled"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"response\":null}")]
    [InlineData("{\"response\":\"Perhaps\"}")]
    [InlineData("{\"response\":7}")]
    public async Task A_Missing_Or_Unknown_Answer_Returns_400(string body)
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await api.Client().PutAsync(Route(sessionId), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().PutAsJsonAsync(Route(Guid.NewGuid()), Yes);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Member_Cant_Answer_For_This_Session()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_rsvp_kappa", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.Rsvps.IgnoreQueryFilters().AnyAsync(r => r.ClerkUserId == "user_rsvp_kappa"));
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PutAsJsonAsync(Route(sessionId), Yes)).StatusCode);
    }

    [Fact]
    public async Task A_Token_From_Another_Club_Returns_403()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg).PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Flag_Off_For_The_Club_Returns_404()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PutAsJsonAsync(Route(sessionId), Yes);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Uri Route(Guid sessionId) => new($"/sessions/{sessionId}/rsvp", UriKind.Relative);

    // A session this far from the club's wall clock right now.
    private async Task<Guid> SeedSessionStartingAsync(TimeSpan fromNow)
    {
        var clock = Tenant.Create(SessionApi.IotaTenantId, SessionApi.IotaSlug, "Clock");
        var start = clock.ToClubTime(DateTimeOffset.UtcNow).Add(fromNow);
        var session = Session.Create(
            Guid.CreateVersion7(),
            SessionApi.IotaTenantId,
            DateOnly.FromDateTime(start),
            TimeOnly.FromDateTime(start),
            "Clock Hall",
            Session.DefaultFeeCents,
            DateTimeOffset.UtcNow);

        await using var db = SessionApi.Iota(fixture);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Rsvp> LoadAsync(Guid sessionId, string userId) =>
        await FindAsync(sessionId, userId) ?? throw new InvalidOperationException("No answer stored.");

    private async Task<Rsvp?> FindAsync(Guid sessionId, string userId)
    {
        await using var db = SessionApi.Iota(fixture);
        return await db.Rsvps.AsNoTracking().SingleOrDefaultAsync(r => r.SessionId == sessionId && r.ClerkUserId == userId);
    }
}
