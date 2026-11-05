using EarlyEagles.Infrastructure.Development;

namespace EarlyEagles.Tests.Unit.Development;

// P1-004.1 — "next Sunday" is always ahead of today.
public class DevelopmentSeederTests
{
    [Theory]
    [InlineData(2026, 11, 2, 2026, 11, 8)] // Monday
    [InlineData(2026, 11, 7, 2026, 11, 8)] // Saturday
    [InlineData(2026, 11, 8, 2026, 11, 15)] // Sunday: the one a week on
    [InlineData(2026, 12, 28, 2027, 1, 3)] // Across the year end
    public void NextSunday_Is_The_First_Sunday_After_Today(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), DevelopmentSeeder.NextSunday(new DateOnly(y, m, d)));
    }
}
