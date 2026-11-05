using EarlyEagles.Application.Sessions;

namespace EarlyEagles.Tests.Unit.Sessions;

// P1-005 — ISO weeks start on Monday and don't follow calendar years.
public class IsoWeekTests
{
    [Theory]
    [InlineData("2026-W45", 2026, 11, 2)]
    [InlineData("2026-W01", 2025, 12, 29)] // Week 1 can start in December.
    [InlineData("2026-W53", 2026, 12, 28)] // 2026 is a 53-week year.
    public void Parses_To_Its_Monday(string value, int year, int month, int day)
    {
        Assert.True(IsoWeek.TryParse(value, out var week));
        Assert.Equal(new DateOnly(year, month, day), week.Monday);
        Assert.Equal(week.Monday.AddDays(6), week.Sunday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2025-W53")]
    [InlineData("2026-W54")]
    [InlineData("2026-W00")]
    [InlineData("2026-W5")]
    [InlineData("0000-W01")]
    [InlineData("9999-W01")]
    [InlineData("2026-45")]
    [InlineData("2026-W45 ")]
    public void Refuses_Anything_Else(string? value)
    {
        Assert.False(IsoWeek.TryParse(value, out _));
    }
}
