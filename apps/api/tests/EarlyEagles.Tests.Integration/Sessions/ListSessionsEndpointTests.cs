using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-005 — a member reads one ISO week of their own club's sessions. 2031 is
// this test's own year; week 2031-W10 runs Monday 3 to Sunday 9 March.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ListSessionsEndpointTests : IAsyncLifetime
{
    private const string Url = "/sessions?week=2031-W10";

    private readonly PostgresFixture fixture;

    public ListSessionsEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);

        await using var iota = SessionApi.Iota(fixture);
        if (iota.Sessions.Any(s => s.Date.Year == 2031))
        {
            return;
        }

        var cancelled = NewSession(SessionApi.IotaTenantId, new DateOnly(2031, 3, 9), new TimeOnly(9, 0), "Late Sunday");
        cancelled.Cancel();
        iota.Sessions.AddRange(
            cancelled,
            NewSession(SessionApi.IotaTenantId, new DateOnly(2031, 3, 9), new TimeOnly(7, 0), "Early Sunday"),
            NewSession(SessionApi.IotaTenantId, new DateOnly(2031, 3, 3), new TimeOnly(19, 0), "Monday"),
            NewSession(SessionApi.IotaTenantId, new DateOnly(2031, 3, 2), new TimeOnly(7, 0), "Sunday before"),
            NewSession(SessionApi.IotaTenantId, new DateOnly(2031, 3, 10), new TimeOnly(7, 0), "Monday after"));
        await iota.SaveChangesAsync();

        await using var kappa = SessionApi.Kappa(fixture);
        kappa.Sessions.Add(NewSession(SessionApi.KappaTenantId, new DateOnly(2031, 3, 5), new TimeOnly(7, 0), "Kappa Wednesday"));
        await kappa.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Gets_That_Week_Earliest_First_Cancelled_Included(string environment)
    {
        using var api = new SessionApi(fixture, environment);

        var sessions = await api.Client().GetFromJsonAsync<List<SessionDto>>(new Uri(Url, UriKind.Relative));

        Assert.Equal(["Monday", "Early Sunday", "Late Sunday"], sessions!.Select(s => s.Venue));
        Assert.Equal("Cancelled", sessions![2].Status);
        Assert.Equal(new DateOnly(2031, 3, 3), sessions[0].Date);
        Assert.Equal(new TimeOnly(19, 0), sessions[0].StartTime);
        Assert.Equal(1500, sessions[0].FeeCents);
    }

    [Fact]
    public async Task Another_Club_Sees_Only_Its_Own_Sessions()
    {
        using var api = new SessionApi(fixture);

        var sessions = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .GetFromJsonAsync<List<SessionDto>>(new Uri(Url, UriKind.Relative));

        Assert.Equal(["Kappa Wednesday"], sessions!.Select(s => s.Venue));
    }

    [Fact]
    public async Task A_Week_With_No_Sessions_Is_Empty()
    {
        using var api = new SessionApi(fixture);

        var sessions = await api.Client().GetFromJsonAsync<List<SessionDto>>(new Uri("/sessions?week=2031-W20", UriKind.Relative));

        Assert.Empty(sessions!);
    }

    [Theory]
    [InlineData("/sessions")]
    [InlineData("/sessions?week=2031-10")]
    [InlineData("/sessions?week=2031-W00")]
    [InlineData("/sessions?week=2031-W53")] // 2031 has 52 weeks.
    [InlineData("/sessions?week=soon")]
    public async Task A_Missing_Or_Impossible_Week_Returns_400(string url)
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Anonymous().GetAsync(new Uri(Url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg).GetAsync(new Uri(Url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(new Uri(Url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Session NewSession(Guid tenantId, DateOnly date, TimeOnly time, string venue) =>
        Session.Create(Guid.CreateVersion7(), tenantId, date, time, venue, Session.DefaultFeeCents, DateTimeOffset.UtcNow);

    private sealed record SessionDto(Guid Id, DateOnly Date, TimeOnly StartTime, string Venue, int FeeCents, string Status);
}
