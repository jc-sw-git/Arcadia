using System.Globalization;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Services;

/// <summary>
/// Display formatting (#553): the order and separators of numbers, dates and times follow the system's regional format
/// (<see cref="Region"/>) and "Uhrzeit" (<see cref="ClockFormat"/>); day and month names, units and the space before "%"
/// follow the UI language (<see cref="Culture"/>), like every text.
/// </summary>
public static class Formatting
{
    /// <summary>The UI language: upper case, sorting, the search.</summary>
    public static CultureInfo Culture => UiLanguage.Culture;

    /// <summary>
    /// The system's regional format, as Windows reports it with the player's own changes ("1.234" or "1,234"), with the
    /// day and month names of the UI language. Read once: a change in Windows shows at Arcadia's next start (#553 A6).
    /// </summary>
    public static CultureInfo Region => _region ??= RegionWithNamesOf(CultureInfo.CurrentCulture, Culture);

    private static CultureInfo? _region;

    /// <summary>
    /// "Uhrzeit" in "Lokalisierung", for every time Arcadia shows. Set from the settings at the start and on every change,
    /// so it applies at once; a time already shown changes when it is shown again (the header clock: at once).
    /// </summary>
    public static ClockFormat ClockFormat { get; set; }

    public static string LastPlayed(DateTimeOffset? lastPlayed, DateTimeOffset now)
    {
        if (lastPlayed is not { } value)
        {
            return AppTexts.NeverPlayed;
        }

        var days = (int)(now.Date - value.ToLocalTime().Date).TotalDays;
        return days switch
        {
            <= 0 => AppTexts.Today,
            1 => AppTexts.Yesterday,
            < 7 => AppTexts.DaysAgo(days),
            < 14 => AppTexts.PlayedAWeekAgo,
            < 30 => AppTexts.WeeksAgo(days / 7),
            < 60 => AppTexts.PlayedAMonthAgo,
            < 365 => AppTexts.MonthsAgo(days / 30),
            _ => AppTexts.PlayedOverAYearAgo,
        };
    }

    /// <summary>A number of entries with the region's thousands separators: "1.234" or "1,234".</summary>
    public static string Count(int count) => count.ToString("N0", Region);

    /// <summary>A number without separators, e.g. a year or a setting's value, in the region's digits.</summary>
    public static string Number(int value) => value.ToString(Region);

    public static string Playtime(TimeSpan? playtime) =>
        playtime is { } value ? AppTexts.Hours(Math.Max(1, (int)value.TotalHours)) : "–";

    /// <summary>An install size in GB ("Go" in French), the number in the region's format.</summary>
    public static string Size(long? bytes) =>
        bytes is { } value ? AppTexts.Gigabytes((value / 1024d / 1024d / 1024d).ToString("0.0", Region)) : "–";

    /// <summary>0…1 → "42 %" (the UI language's spacing). Floors, so 99.9 % never shows as 100 %.</summary>
    public static string Percent(double fraction) => AppTexts.Percent(Number((int)Math.Floor(Math.Clamp(fraction, 0, 1) * 100)));

    /// <summary>"09.10.2026, 22:14" in local time, or "–".</summary>
    public static string DateAndTime(DateTimeOffset? value) =>
        value is { } v ? $"{Date(v)}, {Clock(v.ToLocalTime())}" : "–";

    /// <summary>"18:04" in local time; "20.10., 23:14" if that wasn't on <paramref name="now"/>'s day.</summary>
    public static string TimeOfDay(DateTimeOffset value, DateTimeOffset now)
    {
        var local = value.ToLocalTime();
        return local.Date == now.ToLocalTime().Date ? Clock(local) : $"{local.ToString(DayAndMonthPattern(), Region)}, {Clock(local)}";
    }

    /// <summary>"09.10.2026" in local time, or "–".</summary>
    public static string Date(DateTimeOffset? value) =>
        value is { } v ? v.ToLocalTime().ToString("d", Region) : "–";

    /// <summary>A day without a time, e.g. a release date: "09.10.2026".</summary>
    public static string Date(DateOnly value) => value.ToString("d", Region);

    /// <summary>"9. Oktober 2026" in local time: the region's long date without the weekday.</summary>
    public static string LongDate(DateTimeOffset value) => value.ToLocalTime().ToString(Dates.LongDate, Region);

    /// <summary>"Laufwerk D:" for a Windows path, otherwise null.</summary>
    public static string? Drive(string? path) =>
        path is { Length: >= 2 } && char.IsAsciiLetter(path[0]) && path[1] == ':' ? AppTexts.Drive(char.ToUpperInvariant(path[0])) : null;

    /// <summary>"Name A–Z"; null is the search's own order, "Relevanz".</summary>
    public static string SortLabel(SortOrder? sort) => (sort?.Field, sort?.Direction) switch
    {
        (null, _) => AppTexts.SortRelevance,
        (SortField.Name, SortDirection.Ascending) => AppTexts.SortNameAscending,
        (SortField.Name, SortDirection.Descending) => AppTexts.SortNameDescending,
        (SortField.InstalledAt, SortDirection.Descending) => AppTexts.SortInstalledNewest,
        (SortField.InstalledAt, SortDirection.Ascending) => AppTexts.SortInstalledOldest,
        (SortField.LastPlayed, SortDirection.Descending) => AppTexts.SortPlayedNewest,
        _ => AppTexts.SortPlayedOldest,
    };

    /// <summary>"21:58" or "9:58 PM" as "Uhrzeit" says, for a time already in local time (<see cref="ClockFormat"/>).</summary>
    public static string Clock(DateTimeOffset local) => ClockFormat switch
    {
        ClockFormat.TwentyFourHours => local.ToString("HH:mm", Region),
        ClockFormat.TwelveHours => local.ToString("h:mm tt", TwelveHourFormat()),
        _ => local.ToString("t", Region),
    };

    /// <summary>
    /// <see cref="Clock"/> with the UI language's word for o'clock where the time takes one: "21:58 Uhr", but "9:58 PM"
    /// (#553 A5).
    /// </summary>
    public static string ClockWithUnit(DateTimeOffset local) =>
        UsesTwelveHours ? Clock(local) : AppTexts.TimeWithUnit(Clock(local));

    private static bool UsesTwelveHours => ClockFormat switch
    {
        ClockFormat.TwelveHours => true,
        ClockFormat.TwentyFourHours => false,
        _ => Region.DateTimeFormat.ShortTimePattern.Contains('t'),
    };

    /// <summary>Uses <paramref name="region"/> as the system's regional format from now on, e.g. in a test; null reads the system's again.</summary>
    internal static void UseRegion(CultureInfo? region)
    {
        _region = region is null ? null : RegionWithNamesOf(region, Culture);
        _dates = null;
    }

    /// <summary>The header's date: weekday, day and month, short, in the region's order ("Sa., 24. Okt." or "Sat, Oct 24").</summary>
    public static string ShortDate(DateTimeOffset now) => now.ToString(Dates.HeaderDate, Region);

    /// <summary>
    /// The region's short date without the year ("dd.MM." of "dd.MM.yyyy", "M/d" of "M/d/yyyy"); the region's
    /// day and month pattern if its short date can't be cut.
    /// </summary>
    internal static string DayAndMonthPattern() => Dates.DayAndMonth;

    // Cut from the region's patterns once, with the region (#592).
    private static RegionDatePatterns Dates => _dates ??= RegionDatePatterns.Of(Region);

    private static RegionDatePatterns? _dates;

    private static CultureInfo RegionWithNamesOf(CultureInfo region, CultureInfo language)
    {
        var mixed = (CultureInfo)region.Clone();
        var names = language.DateTimeFormat;
        var format = mixed.DateTimeFormat;
        format.DayNames = names.DayNames;
        format.AbbreviatedDayNames = names.AbbreviatedDayNames;
        format.ShortestDayNames = names.ShortestDayNames;
        format.MonthNames = names.MonthNames;
        format.AbbreviatedMonthNames = names.AbbreviatedMonthNames;
        format.MonthGenitiveNames = names.MonthGenitiveNames;
        format.AbbreviatedMonthGenitiveNames = names.AbbreviatedMonthGenitiveNames;
        return CultureInfo.ReadOnly(mixed);
    }

    // Some regions name no AM and PM (German on Windows): the 12-hour clock then shows them in English, as its value says.
    private static DateTimeFormatInfo TwelveHourFormat()
    {
        var region = Region.DateTimeFormat;
        if (region.AMDesignator.Length > 0 && region.PMDesignator.Length > 0)
        {
            return region;
        }

        var format = (DateTimeFormatInfo)region.Clone();
        format.AMDesignator = CultureInfo.InvariantCulture.DateTimeFormat.AMDesignator;
        format.PMDesignator = CultureInfo.InvariantCulture.DateTimeFormat.PMDesignator;
        return format;
    }

    /// <summary>Placeholder cover colors derived from the title until real artwork is loaded.</summary>
    public static (IBrush Background, IBrush Accent) CoverBrushes(string seed)
    {
        var hue = StableHash(seed) % 360;
        return (Tone(hue, "PlaceholderCoverTone"), Tone(hue, "PlaceholderCoverAccentTone"));
    }

    /// <summary>The detail page's placeholder backdrop and its decorative rings, in the cover's hue.</summary>
    public static (IBrush Backdrop, IBrush Ring) HeroBrushes(string seed)
    {
        var hue = StableHash(seed) % 360;
        return (Tone(hue, "PlaceholderHeroBackdropTone"), Tone(hue, "PlaceholderHeroRingTone"));
    }

    /// <summary>The first letter of a name, as a whole character also when it is made of several (e.g. an emoji).</summary>
    public static string Monogram(string name) =>
        name.Trim() is { Length: > 0 } trimmed ? trimmed[..StringInfo.GetNextTextElementLength(trimmed)].ToUpper(Culture) : "?";

    public static IBrush LauncherTileBrush(string launcherId) => Tone(StableHash(launcherId) % 360, "PlaceholderLauncherTileTone");

    /// <summary>
    /// The hue with the theme's saturation and lightness ("0.28 0.20" in <c>Theme.axaml</c>), so a theme changes the
    /// placeholders as it changes every other colour. Without a theme (or a value it can't read) the tone is grey.
    /// </summary>
    private static SolidColorBrush Tone(int hue, string key)
    {
        var (saturation, lightness) = Application.Current?.TryFindResource(key, out var value) == true && value is string tone
            ? ParsedTones.GetOrAdd(tone, ParseTone)
            : Grey;
        return new SolidColorBrush(new HslColor(1, hue, saturation, lightness).ToRgb());
    }

    private static readonly (double Saturation, double Lightness) Grey = (0, 0.25);

    // A placeholder per game at every scan: each tone is parsed once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (double Saturation, double Lightness)> ParsedTones = new(StringComparer.Ordinal);

    private static (double Saturation, double Lightness) ParseTone(string tone) =>
        tone.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var s, var l]
        && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var saturation)
        && double.TryParse(l, NumberStyles.Float, CultureInfo.InvariantCulture, out var lightness)
            ? (saturation, lightness)
            : Grey;

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash & int.MaxValue;
    }
}
