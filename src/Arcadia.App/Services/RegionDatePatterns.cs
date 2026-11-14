using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>
/// The date patterns Arcadia cuts from a region's own (#592), made and checked once per region: the header date, a day
/// and month, the long date without the weekday. A pattern .NET can't read (e.g. one the player made in Windows) gives
/// way to the region's standard pattern, so a date never ends Arcadia.
/// </summary>
internal sealed record RegionDatePatterns(string HeaderDate, string DayAndMonth, string LongDate)
{
    // A date that has every field, to try a pattern on.
    private static readonly DateTime Sample = new(2026, 11, 14, 21, 58, 0, DateTimeKind.Local);

    public static RegionDatePatterns Of(CultureInfo region)
    {
        var format = region.DateTimeFormat;
        var withoutYear = DatePattern.WithoutYear(format.LongDatePattern);
        var dayAndMonth = DatePattern.WithoutYear(format.ShortDatePattern);
        return new RegionDatePatterns(
            Usable(region, nameof(HeaderDate), DatePattern.Abbreviated(HasDayAndMonth(withoutYear) ? withoutYear : format.MonthDayPattern), "M", "ddd d MMM"),
            Usable(region, nameof(DayAndMonth), HasDayAndMonth(dayAndMonth) ? dayAndMonth : format.MonthDayPattern, "M", "d MMM"),
            Usable(region, nameof(LongDate), DatePattern.WithoutWeekday(format.LongDatePattern), "D", "d MMMM yyyy"));
    }

    private static bool HasDayAndMonth(string pattern) => DatePattern.Has(pattern, 'd') && DatePattern.Has(pattern, 'M');

    // The first pattern .NET can read: the cut one, else the region's standard pattern, else a plain one that always reads
    // (the standard one is the region's too, and the player may have broken it in Windows).
    private static string Usable(CultureInfo region, string use, params string[] patterns)
    {
        foreach (var pattern in patterns[..^1])
        {
            try
            {
                if (pattern.Length > 0 && Sample.ToString(pattern, region).Length > 0)
                {
                    return pattern;
                }
            }
            catch (FormatException)
            {
                // A date pattern says nothing about the player, so it may go into the log.
                AppLog.Write($"Datumsformat der Region {region.Name} für {use} nicht lesbar: \"{pattern}\"");
            }
        }

        return patterns[^1];
    }
}
