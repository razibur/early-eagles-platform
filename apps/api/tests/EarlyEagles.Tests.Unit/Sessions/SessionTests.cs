using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Tests.Unit.Sessions;

// P1-001.1 — a play session: when, where, the fee; scheduled until cancelled,
// and a cancelled session can't change.
public class SessionTests
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);
    private static readonly TimeOnly Seven = new(7, 0);

    [Fact]
    public void Create_Records_The_Details_And_Starts_Scheduled()
    {
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var session = Session.Create(Guid.NewGuid(), tenantId, Sunday, Seven, "  Edison Sports Hall  ", Session.DefaultFeeCents, createdAt);

        Assert.Equal(tenantId, session.TenantId);
        Assert.Equal(Sunday, session.Date);
        Assert.Equal(Seven, session.StartTime);
        Assert.Equal("Edison Sports Hall", session.Venue);
        Assert.Equal(1500, session.FeeCents);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Equal(createdAt, session.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Session.MaxFeeCents)]
    public void Free_And_Maximum_Fees_Are_Allowed(int fee) => Assert.True(Session.IsValidFee(fee));

    [Theory]
    [InlineData(-1)]
    [InlineData(Session.MaxFeeCents + 1)]
    public void Negative_Or_Absurd_Fees_Are_Refused(int fee)
    {
        Assert.False(Session.IsValidFee(fee));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewSession(fee: fee));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_Venue_Is_Refused(string? venue)
    {
        Assert.False(Session.IsValidVenue(venue));
        Assert.Throws<ArgumentException>(() => NewSession(venue: venue!));
    }

    [Fact]
    public void A_Venue_Longer_Than_The_Column_Is_Refused() =>
        Assert.Throws<ArgumentException>(() => NewSession(venue: new string('v', Session.VenueMaxLength + 1)));

    [Fact]
    public void Empty_Ids_Are_Refused()
    {
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.Empty, Guid.NewGuid(), Sunday, Seven, "Hall", 1500, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Session.Create(Guid.NewGuid(), Guid.Empty, Sunday, Seven, "Hall", 1500, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Updating_Changes_The_Details_And_Reports_It()
    {
        var session = NewSession();

        Assert.True(session.UpdateDetails(Sunday.AddDays(7), new TimeOnly(8, 30), "New Hall", 2000));

        Assert.Equal(Sunday.AddDays(7), session.Date);
        Assert.Equal(new TimeOnly(8, 30), session.StartTime);
        Assert.Equal("New Hall", session.Venue);
        Assert.Equal(2000, session.FeeCents);
    }

    [Fact]
    public void Updating_To_The_Same_Details_Changes_Nothing()
    {
        var session = NewSession();

        Assert.False(session.UpdateDetails(Sunday, Seven, " Hall ", 1500));
    }

    [Fact]
    public void A_Bad_Update_Leaves_The_Session_As_It_Was()
    {
        var session = NewSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateDetails(Sunday.AddDays(7), Seven, "New Hall", -5));

        Assert.Equal(Sunday, session.Date);
        Assert.Equal("Hall", session.Venue);
    }

    [Fact]
    public void Cancel_Is_Idempotent()
    {
        var session = NewSession();

        Assert.True(session.Cancel());
        Assert.False(session.Cancel());
        Assert.Equal(SessionStatus.Cancelled, session.Status);
    }

    [Fact]
    public void A_Cancelled_Session_Cannot_Change()
    {
        var session = NewSession();
        session.Cancel();

        Assert.Throws<InvalidOperationException>(() => session.UpdateDetails(Sunday.AddDays(7), Seven, "Hall", 1500));
        Assert.Equal(Sunday, session.Date);
    }

    private static Session NewSession(int fee = 1500, string venue = "Hall") =>
        Session.Create(Guid.NewGuid(), Guid.NewGuid(), Sunday, Seven, venue, fee, DateTimeOffset.UtcNow);
}
