using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

/// <summary>
/// The one place launchers are registered. Scanning, starting and install tracking read the
/// launchers from here and only see the active ones (<see cref="LauncherSelection"/>, #136).
/// </summary>
public sealed class LauncherRegistry
{
    private readonly IReadOnlyList<ILauncherIntegration> _all;
    private readonly List<string> _ids;
    private readonly Func<IReadOnlyList<string>?> _chosen;
    private readonly Func<string, LauncherPreferences> _preferences;

    // Replaced as a whole by Locate, so a reader on another thread sees either the old or the new locations.
    private volatile IReadOnlyDictionary<string, string?>? _locations;

    /// <param name="integrations">In the order they were registered, the tab order until the user chooses one.</param>
    /// <param name="chosen">
    /// The user's active launchers in tab order, or null if the user never chose. Asked on every read, so a change
    /// applies at once.
    /// </param>
    /// <param name="preferences">What the user changed about a launcher (#137, #138), asked on every read; null: nothing.</param>
    public LauncherRegistry(IEnumerable<ILauncherIntegration> integrations, Func<IReadOnlyList<string>?> chosen, Func<string, LauncherPreferences>? preferences = null)
    {
        _chosen = chosen;
        _preferences = preferences ?? (_ => new LauncherPreferences());

        // A broken integration is left out rather than stopping Arcadia; later it may come from a plugin.
        var accepted = new List<ILauncherIntegration>();
        var rejected = new List<string>();
        var ids = new List<string>();
        foreach (var integration in integrations)
        {
            try
            {
                var id = integration.Launcher.Id;
                if (LauncherIds.Problem(id) is { } problem)
                {
                    rejected.Add(problem);
                }
                else if (ids.Contains(id))
                {
                    rejected.Add($"Launcher „{id}“ ist doppelt registriert.");
                }
                else if (integration.Scanner.Launcher.Id != id
                    || (integration.Client is { LauncherId: var clientId } && clientId != id)
                    || (integration.InstallMonitor is { LauncherId: var monitorId } && monitorId != id)
                    || (integration.RunningGames is { LauncherId: var runningId } && runningId != id))
                {
                    // Games, tabs and installs are matched by launcher id; a part with another id would never match.
                    rejected.Add($"Launcher „{id}“ enthält Teile eines anderen Launchers.");
                }
                else
                {
                    accepted.Add(integration);
                    ids.Add(id);
                }
            }
            catch (Exception ex)
            {
                // A plugin's getter may throw; that plugin is left out, Arcadia starts with the others.
                rejected.Add($"Launcher {integration.GetType().FullName} ließ sich nicht lesen: {ex}");
            }
        }

        _all = accepted;
        _ids = ids;
        Rejected = rejected;
    }

    /// <summary>Why integrations were left out, for the log; empty if all were accepted.</summary>
    public IReadOnlyList<string> Rejected { get; }

    /// <summary>Every registered launcher, active or not, e.g. for the launcher settings.</summary>
    public IReadOnlyList<ILauncherIntegration> All => _all;

    /// <summary>The switches the launcher offers for software, tools or soundtracks (#141); none for an unknown launcher.</summary>
    public IReadOnlyList<LibraryTypeOption> LibraryOptions(string launcherId) => Registered(launcherId)?.LibraryOptions ?? [];

    /// <summary>Whether the user switched on the launcher's option <paramref name="optionId"/> (#141).</summary>
    public bool IsOptionOn(string launcherId, string optionId) => _preferences(launcherId).SwitchedOnOptions.Contains(optionId);

    /// <summary>The scanned entries of <paramref name="launcherId"/> its tab shows, by type and the user's switches (#141).</summary>
    public IReadOnlyList<Game> ShownEntries(string launcherId, IReadOnlyList<Game> games) =>
        Library.LibraryTypes.Shown(games, LibraryOptions(launcherId), _preferences(launcherId).SwitchedOnOptions);

    /// <summary>
    /// The launcher as Arcadia shows it: with the name and search keyword the user gave it (#137). A value that breaks the
    /// rules (<see cref="LauncherNames"/>), e.g. edited by hand in settings.json, gives way to the launcher's own.
    /// </summary>
    /// <exception cref="ArgumentException">No launcher with this id is registered.</exception>
    public LauncherInfo Info(string launcherId)
    {
        var own = Registered(launcherId)?.Launcher ?? throw new ArgumentException($"Launcher „{launcherId}“ ist nicht registriert.", nameof(launcherId));
        var preferences = _preferences(launcherId);
        var name = preferences.Name is { } chosenName && LauncherNames.NameProblem(chosenName) is null ? chosenName.Trim() : own.DisplayName;
        var keyword = preferences.Keyword is { } chosenKeyword && chosenKeyword.Length is > 0 and <= LauncherNames.MaxKeywordLength && chosenKeyword.All(LauncherNames.IsKeywordCharacter)
            ? chosenKeyword
            : own.SearchKeyword;
        return own with { DisplayName = name, SearchKeyword = keyword };
    }

    /// <summary>Every registered launcher as Arcadia shows it (<see cref="Info"/>), in registered order.</summary>
    public IReadOnlyList<LauncherInfo> Shown => _ids.Select(Info).ToList();

    /// <summary>The ids of the active launchers, in tab order.</summary>
    public IReadOnlyList<string> ActiveIds => LauncherSelection.Active(_ids, _chosen(), IsFound);

    /// <summary>The ids of the launchers that aren't active: those found on this PC first.</summary>
    public IReadOnlyList<string> InactiveIds => LauncherSelection.Inactive(_ids, ActiveIds, IsFound);

    /// <summary>The active launchers, in tab order.</summary>
    public IReadOnlyList<ILauncherIntegration> Enabled => ActiveIds.Select(id => Registered(id)!).ToList();

    /// <summary>The active launcher with this id, or null.</summary>
    public ILauncherIntegration? Find(string launcherId) =>
        ActiveIds.Contains(launcherId) ? Registered(launcherId) : null;

    /// <summary>The client of the active launcher with this id; null if it has none or isn't active.</summary>
    public ILauncherClient? ClientOf(string launcherId) => Find(launcherId)?.Client;

    /// <summary>
    /// Whether the games of this launcher use Steam Input themselves (<see cref="LauncherInfo.GamesUseSteamInput"/>).
    /// False for a launcher that isn't registered, e.g. a removed plugin's: "Arcadia" is the safe default, as a game on
    /// "Steam-Eingabe" that doesn't use Steam Input would see no controller at all (#156).
    /// </summary>
    public bool GamesUseSteamInput(string launcherId) => Registered(launcherId)?.Launcher.GamesUseSteamInput == true;

    /// <summary>
    /// The registered launcher that owns Steam Input (<see cref="LauncherInfo.OwnsSteamInput"/>), active or not: the Steam
    /// Controller belongs to its program whether its tab is shown or not. Null if none is registered.
    /// </summary>
    public ILauncherIntegration? SteamInputOwner => _all.FirstOrDefault(i => i.Launcher.OwnsSteamInput);

    /// <summary>The registered launcher with this id, active or not, or null.</summary>
    /// <remarks>By the id read once at registration, so a plugin's getter isn't asked again.</remarks>
    public ILauncherIntegration? Registered(string launcherId) => _ids.IndexOf(launcherId) is var index and >= 0 ? _all[index] : null;

    /// <summary>
    /// Whether the launcher is installed on this PC, as <see cref="Locate"/> found it last. Before the first search every
    /// launcher counts as found, so nothing is left out before Arcadia knows.
    /// </summary>
    public bool IsFound(string launcherId) => _locations is not { } locations || locations.GetValueOrDefault(launcherId) is not null;

    /// <summary>Where the launcher was found, as shown to the user; null if it wasn't, or wasn't looked for yet.</summary>
    public string? LocationOf(string launcherId) => _locations?.GetValueOrDefault(launcherId);

    /// <summary>
    /// Looks for every registered launcher on this PC (<see cref="ILauncherIntegration.Locate"/>). Reads the registry or
    /// disk; call it off the UI thread.
    /// </summary>
    public void Locate()
    {
        var locations = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (id, integration) in _ids.Zip(_all))
        {
            try
            {
                UseChosenFolder(id, integration);
                locations[id] = integration.Locate();
            }
            catch (Exception ex)
            {
                // Later a plugin's; one that fails counts as not found rather than stopping the scan of the others.
                System.Diagnostics.Trace.TraceWarning($"Looking for launcher {id} failed: {ex}");
                locations[id] = null;
            }
        }

        _locations = locations;
    }

    /// <summary>
    /// A folder the user picked for the launcher (#138) is checked again before every search: a folder that no longer holds
    /// the launcher (moved, deleted, or edited by hand in settings.json) gives way to the launcher's own search.
    /// </summary>
    private void UseChosenFolder(string launcherId, ILauncherIntegration integration)
    {
        if (integration.FolderChoice is not { } choice)
        {
            return;
        }

        var folder = _preferences(launcherId).Folder is { } chosen ? choice.Find(chosen) : null;
        choice.Use(folder);
    }
}
