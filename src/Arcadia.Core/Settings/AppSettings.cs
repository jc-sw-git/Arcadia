using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed record AppSettings
{
    /// <summary>The format of <c>settings.json</c> this Arcadia writes; see <see cref="SchemaVersion"/>.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The format the file was written in; a file without it is version 1. A change older files can't be read with (a
    /// property changes its type) raises <see cref="CurrentSchemaVersion"/> and converts the older JSON in
    /// <see cref="SettingsStore.Load"/> before it is parsed: otherwise one changed type would put every setting into
    /// <c>settings.json.broken</c>.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// "Controller-Wächter (Pan)" (#242): Pan hides the real Steam Controller and Arcadia emulates it, for games (Xbox
    /// pad) and for Steam (a copy). Off, nothing is emulated and every program sees the real controllers. Off by default:
    /// a fresh install has none of Pan's parts, so "on" would only show warnings about missing parts. "Treiber installieren"
    /// switches it on. A saved <c>settings.json</c> always carries the value, so existing installs keep theirs.
    /// </summary>
    public bool PanEnabled { get; init; }

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

    /// <summary>
    /// Each tab's filter (#44), by <see cref="LauncherInfo.Id"/>; a tab without an entry is unfiltered. Kept while its
    /// launcher is switched off, so the filter is back when it returns. Sort and page size stay global.
    /// </summary>
    public IReadOnlyDictionary<string, SavedFilter> LibraryFilters { get; init; } = new Dictionary<string, SavedFilter>();

    /// <summary>The <see cref="LauncherInfo.Id"/> of the tab Arcadia opens with; null for the first tab.</summary>
    public string? LastTabLauncherId { get; init; }

    /// <summary>The category "Lokalisierung" (#553): the on-screen keyboard's default layout and quick selection, the clock.</summary>
    public LocalizationSettings Localization { get; init; } = new();

    /// <summary>
    /// The layout older versions saved when R3 picked one; read once to become "Standard-Layout" (#553, A4), never written
    /// again: an R3 pick holds only while the keyboard is open now.
    /// </summary>
    [JsonPropertyName("KeyboardLayout")]
    [JsonConverter(typeof(LocalizationSettings.Lenient.Text))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyKeyboardLayout { get; init; }

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
    /// Which levels "Letzte Meldungen" shows (#433): all on by default, also in a settings.json written before them. The
    /// filter only hides, it never drops an entry.
    /// </summary>
    public bool ListsInfoMessages { get; init; } = true;

    public bool ListsWarnings { get; init; } = true;

    public bool ListsErrors { get; init; } = true;

    /// <summary>The status modal's order (#166): "Neueste zuerst" when true, else "Dringlichkeit", for every opening.</summary>
    public bool SortsStatusByNewest { get; init; }

    /// <summary>"Reihenfolge für Artwork" and "Bei mehreren Treffern" in "Datenquellen" (#489, #491).</summary>
    public ArtworkSettings Artwork { get; init; } = new();

    /// <summary>
    /// "Von Hand gesetzte Artworks überschreiben" (#13, #490): "Alle Artworks neu laden" replaces images set by hand too
    /// (<c>ArtworkService.ReloadAll</c>). Off by default; its row is in the category "Datenquellen" (#489).
    /// </summary>
    public bool OverwritesArtworkSetByHand { get; init; }

    /// <summary>"Reihenfolge für Metadaten" in "Datenquellen" (#494).</summary>
    public MetadataSettings Metadata { get; init; } = new();

    /// <summary>The quick selection of each game's "Übersicht" (#500).</summary>
    public QuickSelectionSettings QuickSelection { get; init; } = new();

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
        Artwork = (Artwork ?? new ArtworkSettings()).WithValidValues(),
        Metadata = (Metadata ?? new MetadataSettings()).WithValidValues(),
    };

    /// <summary>Replaces a quick selection written as <c>null</c> by hand with an empty one, so every game shows the defaults.</summary>
    public AppSettings WithValidQuickSelection() => this with { QuickSelection = QuickSelection ?? new() };

    /// <summary>Like <see cref="WithValidLibraryView"/> for "Lokalisierung", with one log line per value replaced.</summary>
    public AppSettings WithValidLocalization(Action<string> log)
    {
        var localization = Localization ?? new();
        if (LegacyKeyboardLayout is { } legacy && localization.KeyboardLayout is null)
        {
            localization = localization with { KeyboardLayout = legacy };
        }

        return this with { Localization = localization.WithValidValues(log), LegacyKeyboardLayout = null };
    }

    /// <summary>The filter saved for the launcher's tab, without what this version doesn't know; none for no entry.</summary>
    public GameFilter FilterOf(string launcherId) =>
        (LibraryFilters ?? new Dictionary<string, SavedFilter>()).GetValueOrDefault(launcherId)?.ToFilter() ?? GameFilter.None;

    /// <summary>Saves the filter of the launcher's tab; no filter removes its entry.</summary>
    public AppSettings WithFilter(string launcherId, GameFilter filter)
    {
        var filters = new Dictionary<string, SavedFilter>(LibraryFilters ?? new Dictionary<string, SavedFilter>(), StringComparer.Ordinal);
        if (SavedFilter.From(filter) is { } saved)
        {
            filters[launcherId] = saved;
        }
        else
        {
            filters.Remove(launcherId);
        }

        return this with { LibraryFilters = filters };
    }

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

    /// <summary>Sets one model's layout in <see cref="ControllerLayouts"/>; null puts it back on its own kind.</summary>
    public AppSettings WithControllerLayout(string modelKey, string? layout)
    {
        var layouts = new Dictionary<string, string>(ControllerLayouts ?? [], StringComparer.Ordinal);
        if (layout is null)
        {
            layouts.Remove(modelKey);
        }
        else
        {
            layouts[modelKey] = layout;
        }

        return this with { ControllerLayouts = layouts };
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
                if (!File.Exists(Path))
                {
                    return new AppSettings();
                }

                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings();
                if (loaded.SchemaVersion > AppSettings.CurrentSchemaVersion)
                {
                    // Written by a newer Arcadia (the player went back a version): what this one doesn't know would be
                    // lost with the next save, so the newer file is kept beside it.
                    KeepCopy($".v{loaded.SchemaVersion}");
                }

                return loaded with { SchemaVersion = AppSettings.CurrentSchemaVersion };
            }
            catch (JsonException)
            {
                KeepCopy(".broken");
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

    private void KeepCopy(string suffix)
    {
        try
        {
            File.Copy(Path, Path + suffix, overwrite: true);
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
            throw new IOException($"{System.IO.Path.GetFileName(Path)} ließ sich beim Start nicht lesen und wird deshalb nicht überschrieben."); // not a UI text (logged)
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";

        // On disk before the move: otherwise a power cut right after it can leave an empty settings.json behind the
        // new name. The file is a few KB, so the flush stays in the milliseconds the UI thread can spare for a change
        // the player made, and the caller still learns synchronously that it wasn't saved.
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(file, settings, Json);
            file.Flush(flushToDisk: true);
        }

        File.Move(temp, Path, overwrite: true);
    }
}
