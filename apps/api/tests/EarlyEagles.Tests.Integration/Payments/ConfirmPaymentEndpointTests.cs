using System.Net;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-009 — an admin confirms a reported payment, which queues PaymentConfirmed.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ConfirmPaymentEndpointTests : IAsyncLifetime
{
    private const string Member = "user_confirm_member";

    private readonly PostgresFixture fixture;
    private Guid sessionId;

    public ConfirmPaymentEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        sessionId = await SessionApi.SeedSessionAsync(fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Confirms_And_The_Event_Is_Queued(string environment)
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client("admin", userId: "user_confirm_admin").PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member);
        Assert.Equal(PaymentStatus.Confirmed, saved.Status);
        Assert.Equal("user_confirm_admin", saved.ReviewedBy);
        Assert.NotNull(saved.ReviewedAt);
        Assert.Equal(1, await CountConfirmedEventsAsync(saved.Id));
    }

    [Fact]
    public async Task Confirming_Twice_Queues_One_Event()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);
        var admin = api.Client("admin");

        await admin.PostAsync(Url(), null);
        var second = await admin.PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var saved = await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member);
        Assert.Equal(1, await CountConfirmedEventsAsync(saved.Id));
    }

    [Fact]
    public async Task A_Rejected_Payment_Returns_409()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member, p => p.Reject("user_admin", "No such transfer", DateTimeOffset.UtcNow));
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task A_Member_Who_Reported_Nothing_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Admin_Finds_Nothing_And_Changes_Nothing()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(PaymentStatus.Reported, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsync(Url(), null)).StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Cant_Confirm_Their_Own_Payment()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: Member).PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(PaymentStatus.Reported, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsync(Url(), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Uri Url() => new($"/sessions/{sessionId}/payments/{Member}/confirm", UriKind.Relative);

    private async Task<int> CountConfirmedEventsAsync(Guid paymentId)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        return await db.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(m => m.EventType == typeof(PaymentConfirmed).FullName && m.Payload.Contains(paymentId.ToString()));
    }
}
