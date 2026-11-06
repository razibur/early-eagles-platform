using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Application.Sessions;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Sessions;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Integration.Sessions;

// P1-011 — the rules run against the reader on a pinned clock, in a year no other
// test uses; the endpoint tests cover the route. Other tests add members to Iota
// too, so these look only at their own.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class LeaderboardTests : IAsyncLifetime
{
    private const string Route = "/leaderboard?period=year";

    private static readonly DateTime ClubNow = new(2031, 6, 15, 12, 0, 0, DateTimeKind.Unspecified);

    private readonly PostgresFixture fixture;

    public LeaderboardTests(PostgresFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync() => SessionApi.SeedClubsAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Yes_On_A_Session_That_Started_Counts_As_Attended()
    {
        await using var db = SessionApi.Iota(fixture);
        db.TenantMembers.Add(Member("user_lb_yes", "Board Yes"));
        Answer(db, Played(db, new DateOnly(2031, 3, 2)), "user_lb_yes", RsvpResponse.Yes);
        Answer(db, Played(db, new DateOnly(2031, 4, 6)), "user_lb_yes", RsvpResponse.Yes);
        await db.SaveChangesAsync();

        var row = await RowAsync("user_lb_yes");

        Assert.Equal(2, row.SessionsAttended);
        Assert.Equal(new DateOnly(2031, 4, 6), row.LastPlayed);
    }

    [Fact]
    public async Task Other_Answers_And_Other_Sessions_Dont_Count()
    {
        await using var db = SessionApi.Iota(fixture);
        db.TenantMembers.Add(Member("user_lb_none", "Board None"));
        Answer(db, Played(db, new DateOnly(2031, 3, 9)), "user_lb_none", RsvpResponse.No);
        Answer(db, Played(db, new DateOnly(2031, 3, 16)), "user_lb_none", RsvpResponse.Maybe);
        Answer(db, Played(db, new DateOnly(2031, 3, 23), cancelled: true), "user_lb_none", RsvpResponse.Yes);
        Answer(db, Played(db, new DateOnly(2030, 12, 29)), "user_lb_none", RsvpResponse.Yes);
        Answer(db, Played(db, new DateOnly(2031, 6, 15), new TimeOnly(12, 1)), "user_lb_none", RsvpResponse.Yes);
        await db.SaveChangesAsync();

        var row = await RowAsync("user_lb_none");

        Assert.Equal(0, row.SessionsAttended);
        Assert.Null(row.LastPlayed);
    }

    [Fact]
    public async Task Only_Confirmed_Payments_On_Sessions_That_Stand_Count_As_Paid()
    {
        await using var db = SessionApi.Iota(fixture);
        db.TenantMembers.Add(Member("user_lb_paid", "Board Paid"));
        Pay(db, Played(db, new DateOnly(2031, 5, 4)), "user_lb_paid", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        Pay(db, Played(db, new DateOnly(2031, 8, 3)), "user_lb_paid", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        Pay(db, Played(db, new DateOnly(2031, 5, 11)), "user_lb_paid", _ => { });
        Pay(db, Played(db, new DateOnly(2031, 5, 18)), "user_lb_paid", p => p.Reject("user_admin", "No such transfer", DateTimeOffset.UtcNow));
        Pay(db, Played(db, new DateOnly(2031, 5, 25), cancelled: true), "user_lb_paid", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        Pay(db, Played(db, new DateOnly(2030, 11, 3)), "user_lb_paid", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        Assert.Equal(2, (await RowAsync("user_lb_paid")).TimesPaid);
    }

    [Fact]
    public async Task Rows_Sort_By_Attended_Then_Paid_Then_Name_With_Unnamed_Last()
    {
        await using var db = SessionApi.Iota(fixture);
        var first = Played(db, new DateOnly(2031, 2, 2));
        var second = Played(db, new DateOnly(2031, 2, 9));
        db.TenantMembers.AddRange(
            Member("user_lb_sort_a", "Sort Zaman"),
            Member("user_lb_sort_b", "Sort Bashir"),
            Member("user_lb_sort_c", "Sort Anwar"),
            Member("user_lb_sort_d", "Sort Chowdhury"),
            Member("user_lb_sort_e", null));
        Answer(db, first, "user_lb_sort_a", RsvpResponse.Yes);
        Answer(db, second, "user_lb_sort_a", RsvpResponse.Yes);
        Answer(db, first, "user_lb_sort_b", RsvpResponse.Yes);
        Pay(db, first, "user_lb_sort_b", p => p.Confirm("user_admin", DateTimeOffset.UtcNow));
        Answer(db, first, "user_lb_sort_c", RsvpResponse.Yes);
        Answer(db, first, "user_lb_sort_d", RsvpResponse.Yes);
        Answer(db, first, "user_lb_sort_e", RsvpResponse.Yes);
        await db.SaveChangesAsync();

        var ids = (await ListAsync()).Select(r => r.ClerkUserId).Where(id => id.StartsWith("user_lb_sort_", StringComparison.Ordinal));

        Assert.Equal(["user_lb_sort_a", "user_lb_sort_b", "user_lb_sort_c", "user_lb_sort_d", "user_lb_sort_e"], ids);
    }

    [Fact]
    public async Task A_Member_Who_Left_Is_Not_Listed()
    {
        await using var db = SessionApi.Iota(fixture);
        Answer(db, Played(db, new DateOnly(2031, 1, 12)), "user_lb_gone", RsvpResponse.Yes);
        await db.SaveChangesAsync();

        Assert.DoesNotContain(await ListAsync(), r => r.ClerkUserId == "user_lb_gone");
    }

    [Fact]
    public async Task Another_Clubs_Members_And_Answers_Stay_Out()
    {
        await using (var kappa = SessionApi.Kappa(fixture))
        {
            kappa.TenantMembers.Add(TenantMember.Create(SessionApi.KappaTenantId, "user_lb_kappa", TenantMemberRole.Member));
            var session = Session.Create(Guid.CreateVersion7(), SessionApi.KappaTenantId, new DateOnly(2031, 1, 19), new TimeOnly(7, 0), "Kappa Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
            kappa.Sessions.Add(session);
            kappa.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), SessionApi.KappaTenantId, session.Id, "user_lb_shared", RsvpResponse.Yes, DateTimeOffset.UtcNow));
            await kappa.SaveChangesAsync();
        }

        await using (var iota = SessionApi.Iota(fixture))
        {
            iota.TenantMembers.Add(Member("user_lb_shared", "Board Shared"));
            await iota.SaveChangesAsync();
        }

        var rows = await ListAsync();

        Assert.DoesNotContain(rows, r => r.ClerkUserId == "user_lb_kappa");
        Assert.Equal(0, rows.Single(r => r.ClerkUserId == "user_lb_shared").SessionsAttended);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Member_Reads_The_Leaderboard(string environment)
    {
        await using (var db = SessionApi.Iota(fixture))
        {
            if (!await db.TenantMembers.AnyAsync(m => m.ClerkUserId == "user_lb_route"))
            {
                db.TenantMembers.Add(Member("user_lb_route", "Board Route"));
                await db.SaveChangesAsync();
            }
        }

        using var api = new SessionApi(fixture, environment);

        var rows = await api.Client().GetFromJsonAsync<List<LeaderboardRow>>(new Uri(Route, UriKind.Relative));

        var row = Assert.Single(rows!, r => r.ClerkUserId == "user_lb_route");
        Assert.Equal("Board Route", row.DisplayName);
    }

    [Theory]
    [InlineData("/leaderboard")]
    [InlineData("/leaderboard?period=month")]
    public async Task Any_Other_Period_Returns_400(string route)
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client().GetAsync(new Uri(route, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Token_Returns_401()
    {
        using var api = new SessionApi(fixture);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(new Uri(Route, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task A_Token_From_Another_Club_Returns_403()
    {
        using var api = new SessionApi(fixture);

        var response = await api.Client(organization: SessionApi.KappaOrg).GetAsync(new Uri(Route, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task With_The_Flag_Off_For_The_Club_Returns_404()
    {
        using var api = new SessionApi(fixture, "Production");

        var response = await api.Client(organization: SessionApi.KappaOrg, slug: SessionApi.KappaSlug).GetAsync(new Uri(Route, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static TenantMember Member(string userId, string? name)
    {
        var member = TenantMember.Create(SessionApi.IotaTenantId, userId, TenantMemberRole.Member);
        member.UpdateProfile(name, null, null);
        return member;
    }

    private static Session Played(ApplicationDbContext db, DateOnly date, TimeOnly? start = null, bool cancelled = false)
    {
        var session = Session.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, date, start ?? new TimeOnly(7, 0), "Board Hall", Session.DefaultFeeCents, DateTimeOffset.UtcNow);
        if (cancelled)
        {
            session.Cancel();
        }

        db.Sessions.Add(session);
        return session;
    }

    private static void Answer(ApplicationDbContext db, Session session, string userId, RsvpResponse response) =>
        db.Rsvps.Add(Rsvp.Create(Guid.CreateVersion7(), SessionApi.IotaTenantId, session.Id, userId, response, DateTimeOffset.UtcNow));

    private static void Pay(ApplicationDbContext db, Session session, string userId, Action<SessionPayment> review)
    {
        var payment = SessionPayment.Report(Guid.CreateVersion7(), SessionApi.IotaTenantId, session.Id, userId, session.FeeCents, "A Rahman", "ZL1", DateTimeOffset.UtcNow);
        review(payment);
        db.SessionPayments.Add(payment);
    }

    private async Task<LeaderboardRow> RowAsync(string userId) =>
        (await ListAsync()).Single(r => r.ClerkUserId == userId);

    private async Task<IReadOnlyList<LeaderboardRow>> ListAsync()
    {
        await using var db = SessionApi.Iota(fixture);
        return await new EfSessionReader(db).ListLeaderboardAsync(ClubNow, CancellationToken.None);
    }
}
