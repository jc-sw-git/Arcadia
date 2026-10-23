using Arcadia.Core.Settings;

namespace Arcadia.Core.Launchers;

/// <summary>
/// The first start's question "Welche Launcher soll Arcadia nutzen?" (#139). Arcadia never takes a launcher on just because
/// it is installed: until the user answered, the settings hold no list of active launchers and the question shows instead
/// of the library, also after an update from a version without it (owner, 2026-10-22). <c>--demo</c> skips it and saves
/// nothing; <c>--first-run</c> asks again, e.g. to try it out.
/// </summary>
public sealed class FirstRunSetup
{
    private readonly SettingsService _settings;
    private readonly bool _saves;

    // With --demo --first-run the answer holds for this session only, until the launcher settings save a list of their own.
    private IReadOnlyList<string>? _unsaved;
    private IReadOnlyList<string>? _savedWhenAnswered;

    /// <param name="demo">Started with <c>--demo</c>: no question unless forced, and the answer isn't saved.</param>
    /// <param name="forced">Started with <c>--first-run</c>: the question shows even if the user answered it before.</param>
    public FirstRunSetup(SettingsService settings, bool demo, bool forced)
    {
        _settings = settings;
        _saves = !demo;
        IsPending = forced || (!demo && settings.Current.ActiveLaunchers is null);
    }

    /// <summary>The question shows; it ends with <see cref="Complete"/>.</summary>
    public bool IsPending { get; private set; }

    /// <summary>
    /// The user's active launchers in tab order, as <see cref="LauncherRegistry"/> asks for them. Null while the question
    /// shows, so every launcher found is read and the question can count its games.
    /// </summary>
    public IReadOnlyList<string>? ChosenLaunchers()
    {
        if (IsPending)
        {
            return null;
        }

        var saved = _settings.Current.ActiveLaunchers;
        return _unsaved is not null && ReferenceEquals(saved, _savedWhenAnswered) ? _unsaved : saved;
    }

    /// <summary>The launchers the question starts with switched on: all found on this PC, in registered order.</summary>
    public static IReadOnlyList<string> Preselected(IReadOnlyList<string> registered, Func<string, bool> isFound) =>
        registered.Where(isFound).ToList();

    /// <summary>"Fertig": <paramref name="active"/> become the tabs, in this order, and the question doesn't show again.</summary>
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
        }
        else
        {
            _unsaved = active;
            _savedWhenAnswered = _settings.Current.ActiveLaunchers;
        }
    }
}
