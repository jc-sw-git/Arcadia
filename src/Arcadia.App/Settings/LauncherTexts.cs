using System.Globalization;

namespace Arcadia.App.Settings;

/// <summary>Texts more than one part of the category "Launcher" writes alike.</summary>
internal static class LauncherTexts
{
    /// <summary>The box with the tabs in miniature beside the list and the page (owner, 2026-10-23).</summary>
    public static string OrderPreviewTitle => AppTexts.LauncherOrderHeading;

    public static string OrderPreviewSubtitle => AppTexts.LauncherOrderPreviewSubtitle;

    public static string GameCount(int count) => count == 1 ? AppTexts.GameCountOne : AppTexts.GameCountMany(count);

    /// <summary>When something happened: "gerade eben", minutes ago within the hour, else the time of day.</summary>
    public static string Ago(TimeProvider clock, DateTimeOffset? at)
    {
        if (at is not { } time)
        {
            return "–";
        }

        var age = clock.GetUtcNow() - time;
        return age < TimeSpan.FromMinutes(1) ? AppTexts.JustNow
            : age < TimeSpan.FromHours(1) ? AppTexts.MinutesAgo((int)age.TotalMinutes)
            : AppTexts.AtTime(Services.Formatting.ClockWithUnit(time.ToLocalTime()));
    }
}
