using System.Globalization;
using Arcadia.Core.Localization;

namespace Arcadia.App.Settings;

/// <summary>What "Sprache anwenden" needs to know of the running Arcadia, and how it restarts it (#554).</summary>
/// <param name="InUse">The language this Arcadia started in.</param>
/// <param name="System">The system's display language, which "Systemsprache" follows.</param>
/// <param name="GameInTheWay">The title of a game that runs or starts; null: none.</param>
/// <param name="InstallsDrivers">The driver installation runs.</param>
/// <param name="UpdatesArcadia">An Arcadia update downloads or installs.</param>
/// <param name="Reloads">"Alle Artworks neu laden" or "Alle Metadaten neu laden" runs: the restart stops it.</param>
/// <param name="Restart">Starts Arcadia again on this page; on failure a toast, and this Arcadia stays.</param>
public sealed record LanguageRestart(
    CultureInfo InUse,
    CultureInfo System,
    Func<string?> GameInTheWay,
    Func<bool> InstallsDrivers,
    Func<bool> UpdatesArcadia,
    Func<bool> Reloads,
    Action Restart);

/// <summary>
/// The group "SPRACHE" of "Lokalisierung" (#554): "Sprache" saves the choice at once and changes nothing else; "Sprache
/// anwenden" below it restarts Arcadia into it, only when that gives another language than the one in use and nothing
/// that a restart would cut off runs (a game, the driver installation, an update).
/// </summary>
public sealed class UiLanguageRows
{
    public const string LanguageKey = "localization-language";
    public const string ApplyKey = "localization-apply";

    private readonly SettingsChanges _changes;
    private readonly LanguageRestart _restart;
    private readonly ActionRowViewModel _apply;

    public UiLanguageRows(SettingsChanges changes, LanguageRestart restart)
    {
        _changes = changes;
        _restart = restart;
        var group = AppTexts.LocalizationLanguageGroup;
        var choices = UiLanguages.Choices;
        LanguageRow = new ChoiceRowViewModel(
            LanguageKey,
            AppTexts.LocalizationLanguage,
            () => Math.Max(0, IndexOf(Chosen)),
            index => Choose(choices[index]),
            index => NameOf(choices[index]),
            0,
            choices.Count - 1)
        {
            Group = group,
            Subtitle = AppTexts.LocalizationLanguageSubtitle,
            ValueInfos = [.. choices.Select(choice => choice == UiLanguages.System ? AppTexts.LocalizationLanguageSystemValue : AppTexts.LocalizationLanguageFixedValue)],
            Info =
            [
                new InfoText(SystemLanguageText(restart.System)),
                new InfoText(AppTexts.LocalizationLanguageInfo),
                new InfoText(AppTexts.LocalizationLanguageMetadata),
            ],
        };
        _apply = new ActionRowViewModel(ApplyKey, AppTexts.LocalizationApply, Apply)
        {
            Group = group,
            Indent = 1,
            ActionText = AppTexts.LocalizationApplyAction,
        };
        Refresh();
    }

    public ChoiceRowViewModel LanguageRow { get; }

    public SettingsRowViewModel ApplyRow => _apply;

    /// <summary>The saved choice; nothing saved (or a value this version doesn't know) is the system language.</summary>
    private string Chosen => _changes.Current.Localization.Language is { } language && UiLanguages.IsChoice(language) ? language : UiLanguages.System;

    /// <summary>
    /// "Sprache" in the info panel: which system language "Systemsprache" gives, or that Arcadia doesn't speak it and is
    /// in English then. The language is named in the UI language.
    /// </summary>
    public static string SystemLanguageText(CultureInfo system)
    {
        if (system.Equals(CultureInfo.InvariantCulture))
        {
            return AppTexts.LocalizationLanguageSystemUnknown;
        }

        var name = LanguageName(system);
        return UiLanguages.Speaks(system)
            ? AppTexts.LocalizationLanguageSystemSpoken(language: name)
            : AppTexts.LocalizationLanguageSystemUnsupported(language: name);
    }

    /// <summary>A game started or ended, an installation or an update or a reload began or ended: UI thread.</summary>
    public void Refresh()
    {
        var differs = !UiLanguages.SameLanguage(UiLanguages.Resolve(Chosen, _restart.System), _restart.InUse);
        var blocker = Blocker();
        _apply.IsEnabled = differs && blocker is null;
        _apply.IsDimmed = !_apply.IsEnabled;
        _apply.Subtitle = blocker?.Subtitle ?? (differs ? AppTexts.LocalizationApplyRestarts : AppTexts.LocalizationApplySame);
        _apply.Info =
        [
            .. blocker is { } why ? [new InfoNote(why.Note, Arcadia.ControllerSupport.HealthSeverity.Info)] : Array.Empty<InfoBlock>(),
            new InfoText(AppTexts.LocalizationApplyInfo),
            .. _restart.Reloads() ? [new InfoText(AppTexts.LocalizationApplyStopsReload)] : Array.Empty<InfoBlock>(),
        ];
    }

    private static int IndexOf(string choice) => UiLanguages.Choices.ToList().IndexOf(choice);

    /// <summary>"Systemsprache" in the UI language, every language in its own.</summary>
    private static string NameOf(string choice) =>
        choice == UiLanguages.System ? AppTexts.LocalizationLanguageSystem : UiLanguages.OwnName(choice);

    // The language alone ("Italienisch"), not the region ("Italienisch (Italien)").
    private static string LanguageName(CultureInfo culture)
    {
        var language = culture.IsNeutralCulture || culture.Parent == CultureInfo.InvariantCulture ? culture : culture.Parent;
        var name = language.DisplayName;
        return name.Length == 0 ? culture.Name : char.ToUpper(name[0], Services.Formatting.Culture) + name[1..];
    }

    private void Choose(string choice)
    {
        _changes.Apply(s => s with { Localization = s.Localization with { Language = choice } }, AppTexts.LocalizationLanguage);
        Refresh();
    }

    private (string Subtitle, string Note)? Blocker()
    {
        if (_restart.GameInTheWay() is { } game)
        {
            return (AppTexts.LocalizationApplyWaitsForGame(game: game), AppTexts.LocalizationApplyGameNote(game: game));
        }

        if (_restart.InstallsDrivers())
        {
            return (AppTexts.LocalizationApplyWaitsForDrivers, AppTexts.LocalizationApplyDriversNote);
        }

        return _restart.UpdatesArcadia() ? (AppTexts.LocalizationApplyWaitsForUpdate, AppTexts.LocalizationApplyUpdateNote) : null;
    }

    private void Apply()
    {
        // The row may still be focused while it greys out: A then does nothing.
        Refresh();
        if (_apply.IsEnabled)
        {
            _restart.Restart();
        }
    }
}
