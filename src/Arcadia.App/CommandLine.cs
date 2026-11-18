namespace Arcadia.App;

/// <summary>
/// Arcadia's command line: the switches it starts with, and the ones it hands the Arcadia it starts again after an
/// update or "Sprache anwenden".
/// </summary>
internal static class CommandLine
{
    /// <summary>Demo libraries for every launcher without an integration; skips the first start's question and saves no launchers.</summary>
    public const string DemoArgument = "--demo";

    /// <summary>With <see cref="DemoArgument"/>: this many demo games in all, e.g. 5000 to measure a large library.</summary>
    public const string DemoGamesArgument = "--demo-games";

    /// <summary>Shows the first start's question again, even after it was answered (#139), e.g. to try it out.</summary>
    public const string FirstRunArgument = "--first-run";

    /// <summary>Set on the restart after an update while Arcadia wasn't in front (e.g. a game was).</summary>
    public const string BackgroundArgument = "--background";

    /// <summary>Passed by Arcadia before #214 on an update restart; dropped, there is no SISR to pause anymore.</summary>
    public const string SisrPausedArgument = "--sisr-paused-for";

    /// <summary>Hands a game started from Arcadia over to the Arcadia started after an update, so its end brings Arcadia back (#94).</summary>
    public const string StartedGameArgument = "--started-game";

    /// <summary>After "Sprache anwenden" (#554): the settings open on "Sprache", in the new language.</summary>
    public const string ShowLanguageArgument = "--show-language-setting";

    public const string FullscreenArgument = "--fullscreen";

    public static int? DemoGames(string[] args)
    {
        var at = Array.FindIndex(args, a => a.Equals(DemoGamesArgument, StringComparison.OrdinalIgnoreCase));
        return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
            && count is > 0 and <= 100_000
            ? count
            : null;
    }

    /// <summary>
    /// After "Sprache anwenden" (#554): the settings open on "Sprache", in fullscreen exactly when the window is now, whatever
    /// the first start said.
    /// </summary>
    public static string[] LanguageRestart(string[] kept, bool fullscreen) =>
    [
        .. kept.Where(a => !a.Equals(FullscreenArgument, StringComparison.OrdinalIgnoreCase)),
        .. fullscreen ? [FullscreenArgument] : Array.Empty<string>(),
        ShowLanguageArgument,
    ];

    /// <summary>Arcadia's own arguments for the start after an update or a restart of its own, plus the state to carry over.</summary>
    public static string[] Restart(string[] args, bool inFront, IEnumerable<string> startedGames)
    {
        var kept = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is SisrPausedArgument or StartedGameArgument)
            {
                i++;
            }
            // --first-run asked once; the start after an update must not ask again, nor open the settings again.
            else if (args[i] != BackgroundArgument && args[i] != ShowLanguageArgument && !args[i].Equals(FirstRunArgument, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(args[i]);
            }
        }

        if (!inFront)
        {
            kept.Add(BackgroundArgument);
        }

        foreach (var gameKey in startedGames)
        {
            kept.Add(StartedGameArgument);
            kept.Add(gameKey);
        }

        return [.. kept];
    }
}
