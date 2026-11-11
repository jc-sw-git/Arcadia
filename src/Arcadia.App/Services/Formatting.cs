using System.Globalization;
using Arcadia.Core.Library;
using Avalonia.Media;

namespace Arcadia.App.Services;

/// <summary>
/// Display formatting. German only for now; moves into resource files once localization lands.
/// </summary>
public static class Formatting
{
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("de-DE");

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

    /// <summary>A number of entries with thousands separators: "1.234".</summary>
    public static string Count(int count) => count.ToString("N0", Culture);

    public static string Playtime(TimeSpan? playtime) =>
        playtime is { } value ? AppTexts.Hours(Math.Max(1, (int)value.TotalHours)) : "–";

    public static string Size(long? bytes) =>
        bytes is { } value ? (value / 1024d / 1024d / 1024d).ToString("0.0", Culture) + " GB" : "–";

    /// <summary>0…1 → "42 %" (German spacing). Floors, so 99.9 % never shows as 100 %.</summary>
    public static string Percent(double fraction) => $"{Math.Floor(Math.Clamp(fraction, 0, 1) * 100):0} %";

    /// <summary>"09.10.2026, 22:14" in local time, or "–".</summary>
    public static string DateAndTime(DateTimeOffset? value) =>
        value is { } v ? v.ToLocalTime().ToString("dd.MM.yyyy, HH:mm", Culture) : "–";

    /// <summary>"18:04" in local time; "20.10., 23:14" if that wasn't on <paramref name="now"/>'s day.</summary>
    public static string TimeOfDay(DateTimeOffset value, DateTimeOffset now)
    {
        var local = value.ToLocalTime();
        return local.ToString(local.Date == now.ToLocalTime().Date ? "HH:mm" : "dd.MM., HH:mm", Culture);
    }

    /// <summary>"09.10.2026" in local time, or "–".</summary>
    public static string Date(DateTimeOffset? value) =>
        value is { } v ? v.ToLocalTime().ToString("dd.MM.yyyy", Culture) : "–";

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

    public static string Clock(DateTimeOffset now) => now.ToString("HH:mm", Culture);

    public static string ShortDate(DateTimeOffset now) => now.ToString("ddd., d. MMM", Culture);

    /// <summary>Placeholder cover colors derived from the title until real artwork is loaded.</summary>
    public static (IBrush Background, IBrush Accent) CoverBrushes(string seed)
    {
        var hue = StableHash(seed) % 360;
        return (
            new SolidColorBrush(new HslColor(1, hue, 0.28, 0.20).ToRgb()),
            new SolidColorBrush(new HslColor(1, hue, 0.55, 0.66).ToRgb()));
    }

    /// <summary>The detail page's placeholder backdrop and its decorative rings, in the cover's hue.</summary>
    public static (IBrush Backdrop, IBrush Ring) HeroBrushes(string seed)
    {
        var hue = StableHash(seed) % 360;
        return (
            new SolidColorBrush(new HslColor(1, hue, 0.30, 0.17).ToRgb()),
            new SolidColorBrush(new HslColor(1, hue, 0.40, 0.28).ToRgb()));
    }

    /// <summary>The first letter of a name, as a whole character also when it is made of several (e.g. an emoji).</summary>
    public static string Monogram(string name) =>
        name.Trim() is { Length: > 0 } trimmed ? trimmed[..StringInfo.GetNextTextElementLength(trimmed)].ToUpper(Culture) : "?";

    public static IBrush LauncherTileBrush(string launcherId) =>
        new SolidColorBrush(new HslColor(1, StableHash(launcherId) % 360, 0.35, 0.30).ToRgb());

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
