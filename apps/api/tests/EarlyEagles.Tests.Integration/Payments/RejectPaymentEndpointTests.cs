using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-009.2 — an admin rejects a reported payment with a reason.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RejectPaymentEndpointTests : IAsyncLifetime
{
    private const string Member = "user_reject_member";

    private static readonly object Body = new { reason = "No such transfer" };

    private readonly PostgresFixture fixture;
    private Guid sessionId;

    public RejectPaymentEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await SessionApi.SeedClubsAsync(fixture);
        sessionId = await SessionApi.SeedSessionAsync(fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task An_Admin_Rejects_With_A_Reason(string environment)
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client("admin", userId: "user_reject_admin").PostAsJsonAsync(Route(), Body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member);
        Assert.Equal(PaymentStatus.Rejected, saved.Status);
        Assert.Equal("No such transfer", saved.RejectionReason);
        Assert.Equal("user_reject_admin", saved.ReviewedBy);
    }

    [Fact]
    public async Task The_Member_Can_Report_Again_After_It()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);

        await api.Client("admin").PostAsJsonAsync(Route(), Body);
        var again = await api.Client(userId: Member).PostAsJsonAsync($"/sessions/{sessionId}/payment-report", new { senderName = "B Rahman", reference = "ZL9" });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(PaymentStatus.Reported, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task A_Confirmed_Payment_Returns_409_And_Stays_Confirmed()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member, p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsJsonAsync(Route(), Body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PaymentStatus.Confirmed, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Without_A_Reason_Returns_400_And_Changes_Nothing(string? reason)
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin").PostAsJsonAsync(Route(), new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Reported, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task A_Member_Who_Reported_Nothing_Returns_404()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client("admin").PostAsJsonAsync(Route(), Body)).StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Admin_Finds_Nothing_And_Changes_Nothing()
    {
        await PaymentReviewTestData.SeedAsync(fixture, sessionId, Member);
        using var api = new SessionApi(fixture);

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsJsonAsync(Route(), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(PaymentStatus.Reported, (await PaymentReviewTestData.LoadAsync(fixture, sessionId, Member)).Status);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync(Route(), Body)).StatusCode);
    }

    [Fact]
    public async Task A_Plain_Member_Gets_403()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client().PostAsJsonAsync(Route(), Body)).StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client("admin", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsJsonAsync(Route(), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private string Route() => $"/sessions/{sessionId}/payments/{Member}/reject";
}
