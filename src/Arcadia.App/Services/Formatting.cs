using System.Globalization;
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
            return "Nie gespielt";
        }

        var days = (int)(now.Date - value.ToLocalTime().Date).TotalDays;
        return days switch
        {
            <= 0 => "Heute",
            1 => "Gestern",
            < 7 => $"Vor {days} Tagen",
            < 14 => "Vor 1 Woche",
            < 30 => $"Vor {days / 7} Wochen",
            < 60 => "Vor 1 Monat",
            < 365 => $"Vor {days / 30} Monaten",
            _ => "Vor über 1 Jahr",
        };
    }

    public static string Playtime(TimeSpan? playtime) =>
        playtime is { } value ? $"{Math.Max(1, (int)value.TotalHours)} Std." : "–";

    public static string Size(long? bytes) =>
        bytes is { } value ? (value / 1024d / 1024d / 1024d).ToString("0.0", Culture) + " GB" : "–";

    /// <summary>0…1 → "42 %" (German spacing). Floors, so 99.9 % never shows as 100 %.</summary>
    public static string Percent(double fraction) => $"{Math.Floor(Math.Clamp(fraction, 0, 1) * 100):0} %";

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
