using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Library;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "Controller" on a launcher's page (#178): the Steam Controller mode the launcher's games on "Standard" get,
/// and below it, while games of its library have a mode of their own, the row that puts them all back on "Standard". On
/// the page of the launcher that owns Steam Input also the group "Desktop-Steuerung" (#315).
/// </summary>
internal sealed class LauncherControllerRows
{
    private const string ControllerGroup = "CONTROLLER";
    private const string PanOffSubtitle = "Aus – erst mit dem Controller-Wächter (Pan)";

    // As Pan's own row says it, so the rows don't send the user to a switch that can't help (QE review PR #319).
    private const string PanUnsupportedSubtitle = "Auf diesem System noch nicht verfügbar";

    // The question before the reset names this many games, then how many more.
    private const int MaxTitlesInQuestion = 5;

    private readonly ILauncherCategory _category;
    private readonly LauncherControllerModeRowViewModel _modeRow;
    private readonly ActionRowViewModel _resetRow;
    private readonly SteamDesktopLayoutRowViewModel _steamDesktopLayoutRow;

    // The library's games with a mode of their own, as the reset row lists them, for its question.
    private IReadOnlyList<string> _ownModeTitles = [];

    public LauncherControllerRows(ILauncherCategory category)
    {
        _category = category;
        _modeRow = new LauncherControllerModeRowViewModel(
            () => category.Changes.Current.LauncherUsesArcadiaPad(category.PageLauncherId!, category.Host.Launchers.GamesUseSteamInput),
            SetMode,
            () => category.Host.IsPanOn)
        {
            Group = ControllerGroup,
        };
        _resetRow = new ActionRowViewModel("launcher-controller-reset", "Alle Spiele auf die Vorauswahl setzen", AskReset)
        {
            Group = ControllerGroup,
            Indent = 1,
            ActionText = "Zurücksetzen",
        };
        _steamDesktopLayoutRow = new SteamDesktopLayoutRowViewModel(category.Changes);
    }

    /// <summary>The rows for the page of launcher <paramref name="id"/>, as Pan and the library's games are now.</summary>
    public IReadOnlyList<SettingsRowViewModel> Show(string id, string name, bool gamesUseSteamInput, LauncherLibrary? library)
    {
        var host = _category.Host;
        var panOn = host.IsPanOn;
        var settings = _category.Changes.Current;
        var preset = ControllerModes.Of(settings.LauncherUsesArcadiaPad(id, host.Launchers.GamesUseSteamInput));
        var panSupported = host.IsPanSupported;
        var panOffSubtitle = panSupported ? PanOffSubtitle : PanUnsupportedSubtitle;
        IReadOnlyList<InfoBlock> panOff = panOn ? [] : [new InfoNote(panSupported
            ? "Einstellbar, sobald der Controller-Wächter (Pan) an ist: Controller → Pan."
            : "Der Controller-Wächter (Pan) läuft auf diesem System noch nicht, und ohne ihn gibt es keinen Steam-Controller-Modus.", HealthSeverity.Info)];

        _modeRow.ShowPan();
        _modeRow.Subtitle = panOn ? "Vorauswahl für die Spiele dieses Launchers" : panOffSubtitle;
        _modeRow.Info =
        [
            new InfoText($"So startet ein Spiel aus {name} mit dem Steam Controller, solange du in seinen Details nichts anderes wählst. Spiele mit eigenem Modus behalten ihn."),
            .. panOff,
            .. PanRows.ModeCards,
            new InfoFootnote($"{(gamesUseSteamInput ? $"Ab Werk: {ControllerModes.Steam}, weil Spiele aus {name} Steams Belegungen nutzen." : $"Ab Werk: {ControllerModes.Arcadia}.")} Gilt ab dem nächsten Spielstart."),
        ];

        // Only games of the library count and are listed; the reset clears the missing ones' choices too.
        var own = (library?.Games ?? [])
            .Select(game => (game.Title, Choice: settings.ControllerModeChoiceFor(game.Key)))
            .Where(game => game.Choice is not null)
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _resetRow.IsEnabled = panOn;
        _resetRow.IsDimmed = !panOn;
        _resetRow.Subtitle = !panOn ? panOffSubtitle : own.Count == 1 ? "1 Spiel hat einen eigenen Modus" : $"{own.Count} Spiele haben einen eigenen Modus";
        _resetRow.Info =
        [
            new InfoText($"Diese Spiele aus {name} hast du selbst umgestellt. Danach folgen sie wieder der Vorauswahl „{preset}“:"),
            new InfoFacts(string.Empty, [.. own.Select(game => new InfoFact(game.Title, ControllerModes.Of(game.Choice!.Value)))]),
            .. panOff,
            new InfoText("[A] fragt vorher nach."),
            new InfoFootnote("Gilt ab dem nächsten Spielstart."),
        ];
        _ownModeTitles = [.. own.Select(game => game.Title)];

        return [_modeRow, .. own.Count > 0 ? [_resetRow] : Array.Empty<SettingsRowViewModel>(), .. DesktopControlRows(id)];
    }

    /// <summary>
    /// The group "Desktop-Steuerung" on Steam's page (#315), the launcher that owns Steam Input. Not the launcher
    /// integration's: it is about Steam the program, which Arcadia's controller support deals with anyway, so it is there
    /// whether Steam's launcher is active or not.
    /// </summary>
    private IReadOnlyList<SettingsRowViewModel> DesktopControlRows(string id)
    {
        if (id != _category.Host.Launchers.SteamInputOwner?.Launcher.Id)
        {
            return [];
        }

        _steamDesktopLayoutRow.Refresh();
        return [_steamDesktopLayoutRow];
    }

    private void SetMode(bool useArcadiaPad)
    {
        var id = _category.PageLauncherId!;
        _category.Changes.Apply(settings => settings.WithLauncherControllerMode(id, useArcadiaPad, _category.Host.Launchers.GamesUseSteamInput), "Steam Controller");
        AppLog.Write($"Steam-Controller-Vorauswahl von {id}: {ControllerModes.Of(useArcadiaPad)}");
        _category.Refresh();
    }

    /// <summary>[A] on "Alle Spiele auf die Vorauswahl setzen": asks first, since the games' own choices are gone afterwards.</summary>
    private void AskReset()
    {
        var host = _category.Host;
        if (_category.PageLauncherId is not { } id || !host.IsPanOn || _ownModeTitles.Count == 0)
        {
            return;
        }

        var name = host.Launchers.Info(id).DisplayName;
        var preset = ControllerModes.Of(_category.Changes.Current.LauncherUsesArcadiaPad(id, host.Launchers.GamesUseSteamInput));
        _category.Dialog = new SettingsQuestionViewModel(
            "Alle Spiele auf die Vorauswahl setzen?",
            $"{ResetGames(_ownModeTitles, name)} wieder „{preset}“: {TitleList(_ownModeTitles)}. Gilt ab dem nächsten Spielstart.",
            "Zurücksetzen",
            () => Reset(id),
            () => _category.Dialog = null);
    }

    private static string ResetGames(IReadOnlyList<string> titles, string launcher) =>
        titles.Count == 1 ? $"1 Spiel aus {launcher} bekommt" : $"{titles.Count} Spiele aus {launcher} bekommen";

    /// <summary>The first few titles; a long list ends with how many more there are.</summary>
    private static string TitleList(IReadOnlyList<string> titles) =>
        titles.Count <= MaxTitlesInQuestion ? string.Join(", ", titles) : $"{string.Join(", ", titles.Take(MaxTitlesInQuestion))} und {titles.Count - MaxTitlesInQuestion} weitere";

    /// <summary>Every game of the launcher back on "Standard", missing ones too, in one write; the focus goes to "Steam Controller".</summary>
    private void Reset(string id)
    {
        _category.Dialog = null;
        _category.Changes.Apply(settings => settings.WithoutControllerModeChoices(id), "Steam Controller");
        AppLog.Write($"Steam-Controller-Modus aller Spiele von {id} auf die Vorauswahl gesetzt");
        _category.Refresh();
        _category.Focus(_modeRow);
    }
}
