using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Tests.Unit.Sessions;

// P1-001.2 — one answer, changeable; the session decides when answers close.
public class RsvpTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void Create_Holds_The_Answer()
    {
        var rsvp = Rsvp.Create(Guid.NewGuid(), Tenant, SessionId, "user_1", RsvpResponse.Maybe, T0);

        Assert.Equal(Tenant, rsvp.TenantId);
        Assert.Equal(SessionId, rsvp.SessionId);
        Assert.Equal("user_1", rsvp.ClerkUserId);
        Assert.Equal(RsvpResponse.Maybe, rsvp.Response);
        Assert.Equal(T0, rsvp.UpdatedAt);
    }

    [Fact]
    public void Create_Refuses_Empty_Ids_A_Blank_User_And_An_Unknown_Response()
    {
        Assert.Throws<ArgumentException>(() => Rsvp.Create(Guid.Empty, Tenant, SessionId, "user_1", RsvpResponse.Yes, T0));
        Assert.Throws<ArgumentException>(() => Rsvp.Create(Guid.NewGuid(), Guid.Empty, SessionId, "user_1", RsvpResponse.Yes, T0));
        Assert.Throws<ArgumentException>(() => Rsvp.Create(Guid.NewGuid(), Tenant, Guid.Empty, "user_1", RsvpResponse.Yes, T0));
        Assert.Throws<ArgumentException>(() => Rsvp.Create(Guid.NewGuid(), Tenant, SessionId, " ", RsvpResponse.Yes, T0));
        Assert.Throws<ArgumentException>(() => Rsvp.Create(Guid.NewGuid(), Tenant, SessionId, "user_1", (RsvpResponse)9, T0));
    }

    [Fact]
    public void Change_Updates_The_Answer_And_Its_Time()
    {
        var rsvp = Rsvp.Create(Guid.NewGuid(), Tenant, SessionId, "user_1", RsvpResponse.Yes, T0);

        Assert.True(rsvp.Change(RsvpResponse.No, T0.AddHours(1)));

        Assert.Equal(RsvpResponse.No, rsvp.Response);
        Assert.Equal(T0.AddHours(1), rsvp.UpdatedAt);
    }

    [Fact]
    public void The_Same_Answer_Changes_Nothing()
    {
        var rsvp = Rsvp.Create(Guid.NewGuid(), Tenant, SessionId, "user_1", RsvpResponse.Yes, T0);

        Assert.False(rsvp.Change(RsvpResponse.Yes, T0.AddHours(1)));

        Assert.Equal(T0, rsvp.UpdatedAt);
    }

    [Theory]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(7, 1, false)]
    public void A_Session_Takes_Answers_Until_It_Starts(int hour, int minute, bool expected)
    {
        var session = NewSession();

        Assert.Equal(expected, session.AcceptsRsvps(new DateTime(2026, 11, 8, hour, minute, 0, DateTimeKind.Unspecified)));
    }

    [Theory]
    [InlineData(6, 59, false)]
    [InlineData(7, 0, true)]
    [InlineData(7, 1, true)]
    public void A_Session_Has_Started_From_Its_Start_Time(int hour, int minute, bool expected)
    {
        var session = NewSession();

        Assert.Equal(expected, session.HasStarted(new DateTime(2026, 11, 8, hour, minute, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void A_Cancelled_Session_Takes_No_Answers()
    {
        var session = NewSession();
        session.Cancel();

        Assert.False(session.AcceptsRsvps(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Unspecified)));
    }

    private static Session NewSession() =>
        Session.Create(Guid.NewGuid(), Tenant, new DateOnly(2026, 11, 8), new TimeOnly(7, 0), "Hall", Session.DefaultFeeCents, T0);
}
