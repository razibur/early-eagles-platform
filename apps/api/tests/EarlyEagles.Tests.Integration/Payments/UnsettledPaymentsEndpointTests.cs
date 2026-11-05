using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-009.3 — the admin's list of who still needs dealing with for a session.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UnsettledPaymentsEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture fixture;
    private Guid sessionId;

    public UnsettledPaymentsEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        sessionId = await SessionApi.SeedSessionAsync(fixture, feeCents: 1800);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Reported_Comes_First_Then_Rejected_Then_Yes_Without_A_Report(string environment)
    {
        await SeedAnswerAsync(sessionId, "user_unsettled_yes", RsvpResponse.Yes);
        await SeedAnswerAsync(sessionId, "user_unsettled_no", RsvpResponse.No);
        await SeedAnswerAsync(sessionId, "user_unsettled_maybe", RsvpResponse.Maybe);
        await SeedAnswerAsync(sessionId, "user_unsettled_paid", RsvpResponse.Yes);
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, "user_unsettled_paid", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, "user_unsettled_rejected", p => p.Reject("user_admin", "No such transfer", DateTimeOffset.UtcNow));
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, "user_unsettled_reported");
        using var api = new SessionApi(fixture, environment);

        var rows = await api.Client("admin").GetFromJsonAsync<List<RowDto>>(Url());

        Assert.Equal(["user_unsettled_reported", "user_unsettled_rejected", "user_unsettled_yes"], rows!.Select(r => r.ClerkUserId));
        Assert.Equal("Reported", rows![0].Status);
        Assert.Equal("ZL123", rows[0].Reference);
        Assert.Equal(1500, rows[0].AmountCents);
        Assert.Equal("No such transfer", rows[1].RejectionReason);
        Assert.Null(rows[2].Status);
        Assert.Equal(1800, rows[2].AmountCents);
    }

    [Fact]
    public async Task A_Free_Session_Chases_Nobody()
    {
        var free = await SessionApi.SeedSessionAsync(fixture, feeCents: 0);
        await SeedAnswerAsync(free, "user_unsettled_free", RsvpResponse.Yes);
        using var api = new SessionApi(fixture);

        var rows = await api.Client("admin").GetFromJsonAsync<List<RowDto>>(new Uri($"/sessions/{free}/payments", UriKind.Relative));

        Assert.Empty(rows!);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").GetAsync(new Uri($"/sessions/{Guid.NewGuid()}/payments", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Admin_Sees_Nothing_Of_This_Session()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(Url())).StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client().GetAsync(Url())).StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(Url());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Uri Url() => new($"/sessions/{sessionId}/payments", UriKind.Relative);

    private async Task SeedAnswerAsync(Guid session, string userId, RsvpResponse response)
    {
        await using var db = SessionApi.Iota(fixture);
        db.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, session, userId, response, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private sealed record RowDto(string ClerkUserId, string? DisplayName, int AmountCents, string? Status, string? SenderName, string? Reference, string? RejectionReason);
}
