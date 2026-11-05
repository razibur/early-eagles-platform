using System.Globalization;
using System.Text.RegularExpressions;

namespace EarlyEagles.Application.Sessions;

/// <summary>An ISO 8601 week, written <c>YYYY-Www</c> (P1-005): Monday to Sunday.</summary>
public readonly partial record struct IsoWeek(DateOnly Monday)
{
    public DateOnly Sunday => Monday.AddDays(6);

    /// <summary>Parses <c>2026-W45</c>. Refuses a week the year doesn't have (most years have no W53).</summary>
    public static bool TryParse(string? value, out IsoWeek week)
    {
        week = default;
        if (value is null || !Format().IsMatch(value))
        {
            return false;
        }

        var year = int.Parse(value.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var number = int.Parse(value.AsSpan(6, 2), CultureInfo.InvariantCulture);
        if (year < 1 || year > 9998 || number < 1 || number > ISOWeek.GetWeeksInYear(year))
        {
            return false;
        }

        week = new IsoWeek(DateOnly.FromDateTime(ISOWeek.ToDateTime(year, number, DayOfWeek.Monday)));
        return true;
    }

    [GeneratedRegex(@"^\d{4}-W\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
