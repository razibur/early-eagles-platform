using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-007.2 — an admin sees every member against a session, answered or not.
// Other tests add members to Iota too, so these look only at their own.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RsvpGridEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture fixture;
    private Guid sessionId;

    public RsvpGridEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);

        await using var iota = SessionApi.Iota(fixture);
        if (!await iota.TenantMembers.AnyAsync(m => m.ClerkUserId == "user_grid_yes"))
        {
            iota.TenantMembers.AddRange(
                Member("user_grid_yes", "Grid Bashir"),
                Member("user_grid_quiet", "Grid Anwar"),
                Member("user_grid_unnamed", null));
        }

        var session = Session.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, new DateOnly(2033, 2, 6), new TimeOnly(7, 0), "Grid Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
        iota.Sessions.Add(session);
        iota.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, session.Id, "user_grid_yes", RsvpResponse.Yes, DateTimeOffset.UtcNow));
        await iota.SaveChangesAsync();
        sessionId = session.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Sees_Every_Member_By_Name_With_Their_Answer(string environment)
    {
        using var api = new SessionApi(fixture, environment);

        var grid = await api.Client("admin").GetFromJsonAsync<List<RowDto>>(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        var mine = grid!.Where(r => r.ClerkUserId.StartsWith("user_grid_", StringComparison.Ordinal)).ToList();
        Assert.Equal(["user_grid_quiet", "user_grid_yes", "user_grid_unnamed"], mine.Select(r => r.ClerkUserId));
        Assert.Null(mine[0].Response);
        Assert.Equal("Yes", mine[1].Response);
        Assert.Equal("Grid Bashir", mine[1].DisplayName);
    }

    [Fact]
    public async Task An_Answer_For_Another_Session_Is_Not_Shown()
    {
        await using (var iota = SessionApi.Iota(fixture))
        {
            var other = Session.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, new DateOnly(2033, 2, 13), new TimeOnly(7, 0), "Grid Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
            iota.Sessions.Add(other);
            await iota.SaveChangesAsync();
            sessionId = other.Id;
        }

        using var api = new SessionApi(fixture);

        var grid = await api.Client("admin").GetFromJsonAsync<List<RowDto>>(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        Assert.Null(grid!.Single(r => r.ClerkUserId == "user_grid_yes").Response);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").GetAsync(new Uri($"/sessions/{Guid.NewGuid()}/rsvps", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Admin_Sees_Nothing_Of_This_Session()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .GetAsync(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Anonymous().GetAsync(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().GetAsync(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .GetAsync(new Uri($"/sessions/{sessionId}/rsvps", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static TenantMember Member(string userId, string? name)
    {
        var member = TenantMember.Create(SessionApi.IotaTenantId, userId, TenantMemberRole.Member);
        member.UpdateProfile(name, null, null);
        return member;
    }

    private sealed record RowDto(string ClerkUserId, string? DisplayName, string? ImageUrl, string? Response);
}
