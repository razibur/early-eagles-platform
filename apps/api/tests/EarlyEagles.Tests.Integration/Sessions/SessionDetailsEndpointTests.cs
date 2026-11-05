using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-005.2 — one session, with the club's RSVP counts and the caller's own RSVP
// and payment.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class SessionDetailsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture fixture;
    private Guid sessionId;

    public SessionDetailsEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);

        await using var iota = SessionApi.Iota(fixture);
        var session = Session.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, new DateOnly(2033, 4, 3), new TimeOnly(7, 15), "Detail Hall", 1800, DateTimeOffset.UtcNow);
        iota.Sessions.Add(session);
        iota.Rsvps.AddRange(
            NewRsvp(session.Id, "user_detail_a", RsvpResponse.Yes),
            NewRsvp(session.Id, "user_detail_b", RsvpResponse.Yes),
            NewRsvp(session.Id, "user_detail_c", RsvpResponse.No),
            NewRsvp(session.Id, "user_detail_d", RsvpResponse.Maybe));
        var payment = SessionPayment.Report(Guid.CreateVersion7(), SessionApi.IotaTenantId, session.Id, "user_detail_a", 1800, "A Rahman", "ZL1", DateTimeOffset.UtcNow);
        payment.Reject("user_admin", "No such transfer", DateTimeOffset.UtcNow);
        iota.SessionPayments.Add(payment);
        await iota.SaveChangesAsync();
        sessionId = session.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Sees_The_Session_The_Counts_And_Their_Own_Rsvp_And_Payment(string environment)
    {
        using var api = new SessionApi(fixture, environment);

        var details = await api.Client(userId: "user_detail_a").GetFromJsonAsync<DetailsDto>(Url());

        Assert.Equal(sessionId, details!.Id);
        Assert.Equal(new DateOnly(2033, 4, 3), details.Date);
        Assert.Equal(new TimeOnly(7, 15), details.StartTime);
        Assert.Equal("Detail Hall", details.Venue);
        Assert.Equal(1800, details.FeeCents);
        Assert.Equal("Scheduled", details.Status);
        Assert.Equal(new CountsDto(2, 1, 1), details.RsvpCounts);
        Assert.Equal("Yes", details.MyRsvp);
        Assert.Equal(new PaymentDto("Rejected", 1800, "No such transfer"), details.MyPayment);
    }

    [Fact]
    public async Task Another_Members_Answer_And_Payment_Are_Not_Mine()
    {
        using var api = new SessionApi(fixture);

        var details = await api.Client(userId: "user_detail_quiet").GetFromJsonAsync<DetailsDto>(Url());

        Assert.Null(details!.MyRsvp);
        Assert.Null(details.MyPayment);
        Assert.Equal(new CountsDto(2, 1, 1), details.RsvpCounts);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().GetAsync(new Uri($"/sessions/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Member_Sees_Nothing_Of_This_Session()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(Url())).StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client(organization: SessionApi.KappaOrg).GetAsync(Url())).StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Rsvp NewRsvp(Guid session, string userId, RsvpResponse response) =>
        Rsvp.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, session, userId, response, DateTimeOffset.UtcNow);

    private Uri Url() => new($"/sessions/{sessionId}", UriKind.Relative);

    private sealed record DetailsDto(
        Guid Id,
        DateOnly Date,
        TimeOnly StartTime,
        string Venue,
        int FeeCents,
        string Status,
        CountsDto RsvpCounts,
        string? MyRsvp,
        PaymentDto? MyPayment);

    private sealed record CountsDto(int Yes, int No, int Maybe);

    private sealed record PaymentDto(string Status, int AmountCents, string? RejectionReason);
}
