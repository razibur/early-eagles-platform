using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Api.Payments;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using EarlyEagles.Tests.Integration.Sessions;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Payments;

// P1-008 — a member reports their Zelle payment for a session.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ReportPaymentEndpointTests : IAsyncLifetime
{
    private static readonly object Body = new { senderName = "A Rahman", reference = "ZL123" };

    private readonly PostgresFixture fixture;

    public ReportPaymentEndpointTests(PostgresFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync() => SessionApi.SeedClubsAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Reports_And_A_Payment_Is_Stored_At_The_Sessions_Fee(string environment)
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture, feeCents: 1800);
        using var api = new SessionApi(fixture, environment);

        var response = await api.Client(userId: "user_pay_1").PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<ReportPaymentResponse>())!.Id;
        var saved = await LoadAsync(sessionId, "user_pay_1");
        Assert.Equal(id, saved.Id);
        Assert.Equal(PaymentStatus.Reported, saved.Status);
        Assert.Equal(1800, saved.AmountCents);
        Assert.Equal("A Rahman", saved.SenderName);
        Assert.Equal("ZL123", saved.Reference);

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.True(await db.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == SessionApi.IotaTenantId && e.Action == "ReportPaymentCommand" && e.Success));
    }

    [Fact]
    public async Task After_A_Rejection_The_Member_Reports_Again_On_The_Same_Payment()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        var id = await SeedPaymentAsync(sessionId, "user_pay_again", p => p.Reject("user_admin", "No such transfer", DateTimeOffset.UtcNow));
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_pay_again").PostAsJsonAsync(Route(sessionId), new { senderName = "B Rahman", reference = "ZL999" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await LoadAsync(sessionId, "user_pay_again");
        Assert.Equal(id, saved.Id);
        Assert.Equal(PaymentStatus.Reported, saved.Status);
        Assert.Equal("B Rahman", saved.SenderName);
        Assert.Null(saved.RejectionReason);
    }

    [Fact]
    public async Task Two_Members_Report_Separately()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var first = await api.Client(userId: "user_pay_a").PostAsJsonAsync(Route(sessionId), Body);
        var second = await api.Client(userId: "user_pay_b").PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Reporting_Twice_Returns_409()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        await SeedPaymentAsync(sessionId, "user_pay_twice");
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_pay_twice").PostAsJsonAsync(Route(sessionId), new { senderName = "Changed", reference = "ZL0" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A Rahman", (await LoadAsync(sessionId, "user_pay_twice")).SenderName);
    }

    [Fact]
    public async Task A_Confirmed_Payment_Returns_409_And_Stays_Confirmed()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        await SeedPaymentAsync(sessionId, "user_pay_done", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_pay_done").PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PaymentStatus.Confirmed, (await LoadAsync(sessionId, "user_pay_done")).Status);
    }

    [Fact]
    public async Task A_Cancelled_Session_Returns_409()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture, cancelled: true);
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_Free_Session_Returns_409()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture, feeCents: 0);
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "ZL1", "SenderName")]
    [InlineData("  ", "ZL1", "SenderName")]
    [InlineData("A Rahman", null, "Reference")]
    [InlineData("A Rahman", "  ", "Reference")]
    public async Task A_Blank_Field_Returns_400_Naming_It(string? senderName, string? reference, string field)
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(sessionId), new { senderName, reference });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Reference_Too_Long_Returns_400()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(
            Route(sessionId),
            new { senderName = "A Rahman", reference = new string('x', SessionPayment.ReferenceMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Unknown_Session_Returns_404()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().PostAsJsonAsync(Route(Guid.NewGuid()), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_Clubs_Member_Cant_Report_On_This_Session()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client(userId: "user_pay_kappa", organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug)
            .PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.SessionPayments.IgnoreQueryFilters().AnyAsync(p => p.ClerkUserId == "user_pay_kappa"));
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync(Route(sessionId), Body)).StatusCode);
    }

    [Fact]
    public async Task A_Session_In_Another_Club_Gets_403()
    {
        var sessionId = await SessionApi.SeedSessionAsync(fixture);
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg).PostAsJsonAsync(Route(sessionId), Body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Feature_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).PostAsJsonAsync(Route(Guid.NewGuid()), Body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string Route(Guid sessionId) => $"/sessions/{sessionId}/payment-report";

    private async Task<Guid> SeedPaymentAsync(Guid sessionId, string userId, Action<SessionPayment>? then = null)
    {
        var payment = SessionPayment.Report(Guid.CreateVersion7(), SessionApi.IotaTenantId, sessionId, userId, 1500, "A Rahman", "ZL123", DateTimeOffset.UtcNow);
        then?.Invoke(payment);
        await using var db = SessionApi.Iota(fixture);
        db.SessionPayments.Add(payment);
        await db.SaveChangesAsync();
        return payment.Id;
    }

    private async Task<SessionPayment> LoadAsync(Guid sessionId, string userId)
    {
        await using var db = SessionApi.Iota(fixture);
        return await db.SessionPayments.AsNoTracking().SingleAsync(p => p.SessionId == sessionId && p.ClerkUserId == userId);
    }
}
