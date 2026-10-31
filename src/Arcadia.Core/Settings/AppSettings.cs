using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// "Controller-Wächter (Pan)" (#242): Pan hides the real Steam Controller and Arcadia emulates it, for games (Xbox
    /// pad) and for Steam (a copy). Off, nothing is emulated and every program sees the real controllers. On by default,
    /// so it runs as soon as "Treiber installieren" has set up its parts.
    /// </summary>
    public bool PanEnabled { get; init; } = true;

    /// <summary>
    /// Each game's own Steam Controller mode (<see cref="UsesArcadiaPad"/>), keyed by <see cref="Game.Key"/>: true =
    /// "Arcadia", false = "Steam-Eingabe"; a game without an entry is on "Standard" and follows its launcher's default
    /// (#178). Before #178 only choices that differed from the launcher's fixed default were stored here; they are the
    /// games' own choices now, so nothing changes for the player. Older versions stored "SteamControllerSupport" and
    /// "LastSisrUpdateCheck" beside it for SISR; they are no properties anymore, so loading ignores them and the next
    /// save drops them (#214).
    /// </summary>
    public Dictionary<string, bool> ControllerSupportOverrides { get; init; } = [];

    /// <summary>
    /// "Controller zuordnen" (#238 D3a): the layout the player chose per controller model, keyed by its USB ids
    /// ("054C:0CE6"), the value a kind's name ("DualSense") or "NotTakenOver". A model without an entry keeps its own
    /// kind (D3b). Read by <c>ControllerLayouts</c>, which ignores what it doesn't know.
    /// </summary>
    public Dictionary<string, string> ControllerLayouts { get; init; } = [];

    /// <summary>
    /// "SISR behalten" (#214): the group "SISR" in the controller settings stays hidden and SISR stays as it is. A factory
    /// reset (#146) shows the group again; the warning "SISR läuft" under Pan shows anyway.
    /// </summary>
    public bool KeepSisr { get; init; }

    /// <summary>When Arcadia last installed the usbip driver; a Windows restart is due after that.</summary>
    public DateTimeOffset? UsbipInstalledAt { get; init; }

    /// <summary>The library's sort order, the same for every tab (owner, 2026-10-17).</summary>
    public SortOrder LibrarySort { get; init; } = SortOrder.Default;

    /// <summary>
    /// The page size the user chose, the same for every tab. A tab with too few games shows a
    /// smaller one (<see cref="PageSizes.Effective"/>) without changing this.
    /// </summary>
    public int PreferredPageSize { get; init; } = PageSizes.Default;

    /// <summary>
    /// The active launchers in tab order (#136): only they are scanned, watched and shown as tabs. Null until the user
    /// chooses; until then every launcher found on this PC is active (<see cref="LauncherSelection"/>).
    /// </summary>
    public IReadOnlyList<string>? ActiveLaunchers { get; init; }

    /// <summary>Name, search keyword, logo and folder the user gave a launcher (#137, #138), by <see cref="LauncherInfo.Id"/>.</summary>
    public IReadOnlyDictionary<string, LauncherPreferences> Launchers { get; init; } = new Dictionary<string, LauncherPreferences>();

    /// <summary>The <see cref="LauncherInfo.Id"/> of the tab Arcadia opens with; null for the first tab.</summary>
    public string? LastTabLauncherId { get; init; }

    /// <summary>The on-screen keyboard's layout the user picked last; null for the default (German).</summary>
    public string? KeyboardLayout { get; init; }

    /// <summary>The controller as a mouse (#64).</summary>
    public MouseModeSettings MouseMode { get; init; } = new();

    /// <summary>View held brings Arcadia back from the background (#64); independent of the mouse mode.</summary>
    public ReturnWithViewSettings ReturnWithView { get; init; } = new();

    /// <summary>
    /// "Steams Desktop-Belegung ignorieren" (#64): the mouse and keys Steam makes of the controller are
    /// dropped. Independent of the mouse mode, because Steam's layout also doubles Arcadia's own navigation (owner, 2026-10-20).
    /// </summary>
    public bool IgnoreSteamDesktopLayout { get; init; } = true;

    /// <summary>
    /// Replaces view values this version doesn't know — written by a newer Arcadia or edited by
    /// hand — with the defaults, instead of failing on them later.
    /// </summary>
    public AppSettings WithValidLibraryView() => this with
    {
        LibrarySort = Enum.IsDefined(LibrarySort.Field) && Enum.IsDefined(LibrarySort.Direction) ? LibrarySort : SortOrder.Default,
        PreferredPageSize = PageSizes.Options.Contains(PreferredPageSize) ? PreferredPageSize : PageSizes.Default,
    };

    /// <summary>Like <see cref="WithValidLibraryView"/> for the mouse mode and View's way back; also replaces a <c>null</c> written by hand.</summary>
    public AppSettings WithValidMouseMode() => this with
    {
        MouseMode = (MouseMode ?? new MouseModeSettings()).WithValidValues(),
        ReturnWithView = (ReturnWithView ?? new ReturnWithViewSettings()).WithValidValues(),
    };

    /// <summary>What the user changed about the launcher; nothing changed gives an empty entry.</summary>
    public LauncherPreferences PreferencesOf(string launcherId) =>
        (Launchers ?? new Dictionary<string, LauncherPreferences>()).GetValueOrDefault(launcherId) ?? new LauncherPreferences();

    /// <summary>Changes what the user set for the launcher; an entry left with nothing set is removed.</summary>
    public AppSettings WithLauncherPreferences(string launcherId, Func<LauncherPreferences, LauncherPreferences> change)
    {
        var launchers = new Dictionary<string, LauncherPreferences>(Launchers ?? new Dictionary<string, LauncherPreferences>(), StringComparer.Ordinal);
        var changed = change(PreferencesOf(launcherId));
        if (changed.IsEmpty)
        {
            launchers.Remove(launcherId);
        }
        else
        {
            launchers[launcherId] = changed;
        }

        return this with { Launchers = launchers };
    }

    /// <summary>
    /// The Steam Controller mode of the game with this key (#213): true = "Arcadia" (Arcadia's Xbox pad), false =
    /// "Steam-Eingabe". The game's own choice, else its launcher's default (<see cref="LauncherUsesArcadiaPad"/>). Takes a
    /// key, not a game, so a Steam game started directly in Steam counts too when it isn't in the library (#119).
    /// </summary>
    /// <param name="gamesUseSteamInput">Whether a launcher's games use Steam Input, by launcher id (<see cref="LauncherRegistry.GamesUseSteamInput"/>).</param>
    public bool UsesArcadiaPad(string gameKey, Func<string, bool> gamesUseSteamInput) =>
        ControllerModeChoiceFor(gameKey) ?? LauncherUsesArcadiaPad(Game.LauncherIdOf(gameKey), gamesUseSteamInput);

    /// <summary>
    /// The mode a launcher's games on "Standard" get (#178): what the user chose on the launcher's page, else the
    /// launcher's own (#156): "Steam-Eingabe" for a launcher whose games use Steam Input (Steam's), "Arcadia" for all others.
    /// </summary>
    /// <param name="gamesUseSteamInput">Whether a launcher's games use Steam Input, by launcher id (<see cref="LauncherRegistry.GamesUseSteamInput"/>).</param>
    public bool LauncherUsesArcadiaPad(string launcherId, Func<string, bool> gamesUseSteamInput) =>
        PreferencesOf(launcherId).UsesArcadiaPad ?? !gamesUseSteamInput(launcherId);

    /// <summary>
    /// Sets the launcher's default. The launcher's own is kept as none, so no launcher needs a special case and a launcher
    /// that changes its own later is followed (#178).
    /// </summary>
    /// <param name="gamesUseSteamInput">Whether a launcher's games use Steam Input, by launcher id (<see cref="LauncherRegistry.GamesUseSteamInput"/>).</param>
    public AppSettings WithLauncherControllerMode(string launcherId, bool useArcadiaPad, Func<string, bool> gamesUseSteamInput) =>
        WithLauncherPreferences(launcherId, p => p with { UsesArcadiaPad = useArcadiaPad == !gamesUseSteamInput(launcherId) ? null : useArcadiaPad });

    /// <summary>The game's own mode; null means "Standard": it follows its launcher's default.</summary>
    public bool? ControllerModeChoiceFor(string gameKey) =>
        (ControllerSupportOverrides ?? []).TryGetValue(gameKey, out var arcadia) ? arcadia : null;

    /// <summary>Sets the game's own mode; null puts it on "Standard".</summary>
    public AppSettings WithControllerModeChoice(string gameKey, bool? useArcadiaPad)
    {
        var choices = new Dictionary<string, bool>(ControllerSupportOverrides ?? []);
        if (useArcadiaPad is { } value)
        {
            choices[gameKey] = value;
        }
        else
        {
            choices.Remove(gameKey);
        }

        return this with { ControllerSupportOverrides = choices };
    }

    /// <summary>
    /// "Alle Spiele auf die Vorauswahl setzen" (#178): every game of the launcher goes back to "Standard", also games no
    /// longer in the library, whose choice would otherwise come back with them.
    /// </summary>
    public AppSettings WithoutControllerModeChoices(string launcherId) => this with
    {
        ControllerSupportOverrides = (ControllerSupportOverrides ?? [])
            .Where(choice => Game.LauncherIdOf(choice.Key) != launcherId)
            .ToDictionary(choice => choice.Key, choice => choice.Value),
    };
}

public sealed class SettingsStore(string path)
{
    private const int ReadAttempts = 3;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>A virus scanner or a sync tool (OneDrive) may hold the file for a moment.</summary>
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(100);

    // The file exists but couldn't be read at the start: it may still hold the user's settings.
    private bool _keepUnreadFile;

    public string Path { get; } = path;

    public static SettingsStore CreateDefault() => new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arcadia", "settings.json"));

    /// <summary>
    /// Missing or unreadable files yield the defaults rather than an error. A file that can't be
    /// parsed is kept as <c>settings.json.broken</c>, because the next save would overwrite it.
    /// A file that can't be read even after a short wait is not overwritten while Arcadia runs
    /// (<see cref="Save"/> refuses), so a lock at the start doesn't cost the user their settings.
    /// </summary>
    public AppSettings Load()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _keepUnreadFile = false;
                return File.Exists(Path)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings()
                    : new AppSettings();
            }
            catch (JsonException)
            {
                KeepBrokenFile();
                return new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < ReadAttempts)
                {
                    Thread.Sleep(ReadRetryDelay);
                    continue;
                }

                _keepUnreadFile = File.Exists(Path);
                return new AppSettings();
            }
        }
    }

    private void KeepBrokenFile()
    {
        try
        {
            File.Copy(Path, Path + ".broken", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing more to save; the defaults still apply.
        }
    }

    /// <exception cref="IOException">The file couldn't be saved, or it couldn't be read at the start and is kept as it is.</exception>
    public void Save(AppSettings settings)
    {
        if (_keepUnreadFile)
        {
            throw new IOException($"{System.IO.Path.GetFileName(Path)} ließ sich beim Start nicht lesen und wird deshalb nicht überschrieben.");
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, Path, overwrite: true);
    }
}
