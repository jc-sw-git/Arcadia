using System.Globalization;

namespace Arcadia.App.Settings;

/// <summary>Texts more than one part of the category "Launcher" writes alike.</summary>
internal static class LauncherTexts
{
    /// <summary>The box with the tabs in miniature beside the list and the page (owner, 2026-10-23).</summary>
    public const string OrderPreviewTitle = "LAUNCHER-REIHENFOLGE";

    public const string OrderPreviewSubtitle = "Eine Vorschau der Reihenfolge, in der die Launcher aufgeführt werden";

    public static string GameCount(int count) => count == 1 ? "1 Spiel" : $"{count} Spiele";

    /// <summary>When something happened: "gerade eben", minutes ago within the hour, else the time of day.</summary>
    public static string Ago(TimeProvider clock, DateTimeOffset? at)
    {
        if (at is not { } time)
        {
            return "–";
        }

        var age = clock.GetUtcNow() - time;
        return age < TimeSpan.FromMinutes(1) ? "gerade eben"
            : age < TimeSpan.FromHours(1) ? $"vor {(int)age.TotalMinutes} Min."
            : $"um {time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)} Uhr";
    }
}
