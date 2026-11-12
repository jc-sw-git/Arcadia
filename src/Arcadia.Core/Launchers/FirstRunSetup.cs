using Arcadia.Core.Settings;

namespace Arcadia.Core.Launchers;

/// <summary>
/// The first start's question "Welche Launcher soll Arcadia nutzen?" (#139). Arcadia never takes a launcher on just because
/// it is installed: until the user answered, the settings hold no list of active launchers and the question shows instead
/// of the library, also after an update from a version without it (owner, 2026-10-22). <c>--demo</c> skips it and saves
/// nothing; <c>--first-run</c> asks again, e.g. to try it out. After the launchers comes the step "Datenquellen" (#497).
/// The first start continues where it stopped, also after closing or a crash: what was chosen and the page it was on are
/// kept in a <see cref="FirstRunProgressFile"/> until it ends.
/// </summary>
public sealed class FirstRunSetup
{
    private readonly SettingsService _settings;
    private readonly bool _saves;
    private readonly FirstRunProgressFile? _record;

    // With --demo --first-run the answer holds for this session only, until the launcher settings save a list of their own.
    private IReadOnlyList<string>? _unsaved;
    private IReadOnlyList<string>? _savedWhenAnswered;

    /// <param name="demo">Started with <c>--demo</c>: no question unless forced, and the answer isn't saved.</param>
    /// <param name="forced">Started with <c>--first-run</c>: the question shows even if the user answered it before.</param>
    /// <param name="record">Where the first start keeps how far it got; null (and with <c>--demo</c>) only until Arcadia closes.</param>
    public FirstRunSetup(SettingsService settings, bool demo, bool forced, FirstRunProgressFile? record = null)
    {
        _settings = settings;
        _saves = !demo;
        _record = demo ? null : record;
        IsPending = forced || (!demo && settings.Current.ActiveLaunchers is null);
        if (IsPending)
        {
            Progress = _record?.Load() ?? FirstRunProgress.Start;
        }
        else
        {
            // Left over if Arcadia stopped between saving the launchers and deleting the record.
            _record?.Delete();
        }
    }

    /// <summary>The question shows; it ends with <see cref="Complete"/>.</summary>
    public bool IsPending { get; private set; }

    /// <summary>The page the first start is on and what was chosen so far; <see cref="FirstRunProgress.Start"/> when it isn't pending.</summary>
    public FirstRunProgress Progress { get; private set; } = FirstRunProgress.Start;

    /// <summary>
    /// The user's active launchers in tab order, as <see cref="LauncherRegistry"/> asks for them. Null while the question
    /// shows, so every launcher found is read and the question can count its games.
    /// </summary>
    public IReadOnlyList<string>? ChosenLaunchers()
    {
        if (IsPending)
        {
            // On "Datenquellen" the library already holds only the launchers chosen, so a search started there sends nothing
            // about the others.
            return Progress.Step == FirstRunStep.DataSources ? Progress.Chosen : null;
        }

        var saved = _settings.Current.ActiveLaunchers;
        return _unsaved is not null && ReferenceEquals(saved, _savedWhenAnswered) ? _unsaved : saved;
    }

    /// <summary>The launchers the question starts with switched on: all found on this PC, in registered order.</summary>
    public static IReadOnlyList<string> Preselected(IReadOnlyList<string> registered, Func<string, bool> isFound) =>
        registered.Where(isFound).ToList();

    /// <summary>A switch on the launcher page changed: the launchers found but off are kept for a restart.</summary>
    public void SwitchedOff(IReadOnlyList<string> off) => Keep(Progress with { SwitchedOff = off });

    /// <summary>"Fertig" on the launcher page: on to "Datenquellen" with <paramref name="active"/> as the tabs-to-be.</summary>
    public void ChooseLaunchers(IReadOnlyList<string> active) => Keep(Progress with { Step = FirstRunStep.DataSources, Chosen = active });

    /// <summary>B on "Datenquellen": back to the launcher page, its switches as left.</summary>
    public void BackToLaunchers() => Keep(Progress with { Step = FirstRunStep.Launchers, Chosen = null });

    /// <summary>
    /// The first start ends: <paramref name="active"/> become the tabs, in this order, and it doesn't show again.
    /// </summary>
    /// <exception cref="IOException">settings.json couldn't be written; the answer still holds until Arcadia closes.</exception>
    /// <exception cref="UnauthorizedAccessException">As <see cref="IOException"/>.</exception>
    public void Complete(IReadOnlyList<string> active)
    {
        if (!IsPending)
        {
            return;
        }

        IsPending = false;
        if (_saves)
        {
            // Changes the settings in memory before it saves them, so a failed save still ends the question.
            _settings.Update(settings => settings with { ActiveLaunchers = active });
            _record?.Delete();
        }
        else
        {
            _unsaved = active;
            _savedWhenAnswered = _settings.Current.ActiveLaunchers;
        }
    }

    private void Keep(FirstRunProgress progress)
    {
        if (!IsPending)
        {
            return;
        }

        Progress = progress;
        _record?.Save(progress);
    }
}
