using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P1-007.1 — a club's zone turns the real time into its wall clock.
public sealed class TenantTimeZoneTests
{
    [Fact]
    public void A_New_Tenant_Starts_In_The_Default_Zone()
    {
        Assert.Equal("America/New_York", NewTenant().TimeZoneId);
    }

    [Theory]
    [InlineData("America/Chicago")]
    [InlineData("Asia/Dhaka")]
    public void ChangeTimeZone_Accepts_An_Iana_Zone(string zone)
    {
        var tenant = NewTenant();

        tenant.ChangeTimeZone(zone);

        Assert.Equal(zone, tenant.TimeZoneId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Eastern Standard Time")]
    public void ChangeTimeZone_Refuses_Anything_Else(string zone)
    {
        var tenant = NewTenant();

        Assert.Throws<ArgumentException>(() => tenant.ChangeTimeZone(zone));
        Assert.Equal(Tenant.DefaultTimeZoneId, tenant.TimeZoneId);
    }

    [Fact]
    public void A_Deactivated_Tenant_Keeps_Its_Zone()
    {
        var tenant = NewTenant();
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.ChangeTimeZone("America/Chicago"));
    }

    [Theory]
    [InlineData("2026-01-11T12:00:00Z", "2026-01-11T07:00:00")] // EST, UTC-5
    [InlineData("2026-07-12T12:00:00Z", "2026-07-12T08:00:00")] // EDT, UTC-4
    [InlineData("2026-01-11T02:00:00Z", "2026-01-10T21:00:00")] // still Saturday at the club
    public void ToClubTime_Follows_Daylight_Saving(string instant, string expected)
    {
        var local = NewTenant().ToClubTime(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), local);
    }

    private static Tenant NewTenant() => Tenant.Create(Guid.NewGuid(), "zone-club", "Zone Club");
}
