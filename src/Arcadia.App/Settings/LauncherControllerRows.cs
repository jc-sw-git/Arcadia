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
    private static string ControllerGroup => AppTexts.ControllerGroup;
    private static string PanOffSubtitle => AppTexts.LauncherControllerPanOff;

    // As Pan's own row says it, so the rows don't send the user to a switch that can't help (QE review PR #319).
    private static string PanUnsupportedSubtitle => AppTexts.LauncherControllerUnsupported;

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
            ValueInfos = [AppTexts.PanModeArcadiaCard, AppTexts.PanModeSteamCard],
        };
        _resetRow = new ActionRowViewModel("launcher-controller-reset", AppTexts.LauncherControllerResetTitle, AskReset)
        {
            Group = ControllerGroup,
            Indent = 1,
            ActionText = AppTexts.Reset,
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
        var panOff = panOn ? null : panSupported ? AppTexts.LauncherControllerNeedsPan : AppTexts.LauncherControllerPanUnsupported;

        _modeRow.ShowPan();
        _modeRow.Subtitle = panOn ? AppTexts.LauncherControllerPresetSubtitle : panOffSubtitle;
        _modeRow.DimmedNote = panOff;
        _modeRow.Info =
        [
            new InfoText(AppTexts.LauncherControllerPresetInfo(name)),
            new InfoFootnote(gamesUseSteamInput
                ? AppTexts.LauncherControllerFactorySteam(mode: ControllerModes.Steam, launcher: name)
                : AppTexts.LauncherControllerFactoryArcadia(ControllerModes.Arcadia)),
        ];

        // Only games of the library count and are listed; the reset clears the missing ones' choices too.
        var own = (library?.Games ?? [])
            .Select(game => (game.Title, Choice: settings.ControllerModeChoiceFor(game.Key)))
            .Where(game => game.Choice is not null)
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _resetRow.IsEnabled = panOn;
        _resetRow.IsDimmed = !panOn;
        _resetRow.DimmedNote = panOff;
        _resetRow.Subtitle = !panOn ? panOffSubtitle : own.Count == 1 ? AppTexts.LauncherControllerOwnModeOne : AppTexts.LauncherControllerOwnModeMany(own.Count);
        _resetRow.Info =
        [
            new InfoText(AppTexts.LauncherControllerOwnModeInfo(launcher: name, preset: preset)),
            new InfoFacts(string.Empty, [.. own.Select(game => new InfoFact(game.Title, ControllerModes.Of(game.Choice!.Value)))]),
            new InfoText(AppTexts.LauncherControllerAsksFirst),
            new InfoFootnote(AppTexts.AppliesFromNextStart),
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
            AppTexts.LauncherControllerAskReset,
            AppTexts.LauncherControllerAskResetText(games: ResetGames(_ownModeTitles, name), preset: preset, titles: TitleList(_ownModeTitles)),
            AppTexts.Reset,
            () => Reset(id),
            () => _category.Dialog = null);
    }

    private static string ResetGames(IReadOnlyList<string> titles, string launcher) =>
        titles.Count == 1 ? AppTexts.LauncherControllerResetGamesOne(launcher) : AppTexts.LauncherControllerResetGamesMany(count: titles.Count, launcher: launcher);

    /// <summary>The first few titles; a long list ends with how many more there are.</summary>
    private static string TitleList(IReadOnlyList<string> titles) =>
        titles.Count <= MaxTitlesInQuestion ? string.Join(", ", titles) : AppTexts.TitleListAndMore(titles: string.Join(", ", titles.Take(MaxTitlesInQuestion)), more: titles.Count - MaxTitlesInQuestion);

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
