using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// "Steam Controller Kompatibilität": run SISR so the Steam Controller works as
    /// a system-wide Xbox pad, in Arcadia and in games that were not started through Steam.
    /// </summary>
    public bool SteamControllerSupport { get; init; }

    /// <summary>Per-game override of <see cref="SteamControllerSupport"/>, keyed by <see cref="Game.Key"/>.</summary>
    public Dictionary<string, bool> ControllerSupportOverrides { get; init; } = [];

    /// <summary>When Arcadia last asked GitHub for a newer SISR.</summary>
    public DateTimeOffset? LastSisrUpdateCheck { get; init; }

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
    /// Whether SISR should run while <paramref name="game"/> runs. Without an override it runs for
    /// every game except those from the Steam launcher, which use Steam Input natively — running
    /// SISR there would double every input.
    /// </summary>
    public bool ControllerSupportFor(Game game) =>
        SteamControllerSupport
        && (ControllerSupportOverrides.TryGetValue(game.Key, out var enabled) ? enabled : DefaultControllerSupportFor(game));

    /// <summary>The mode a game starts with: "Steam Input" for Steam games, "Xbox Controller (SISR)" for all others.</summary>
    public static bool DefaultControllerSupportFor(Game game) => game.LauncherId != KnownLaunchers.Steam.Id;

    /// <summary>
    /// Sets the mode for <paramref name="game"/>. Only a choice that differs from the game's default
    /// is stored, so untouched games follow the default if it ever changes.
    /// </summary>
    public AppSettings WithControllerSupportChoice(Game game, bool useSisr) =>
        WithControllerSupportOverride(game, useSisr == DefaultControllerSupportFor(game) ? null : useSisr);

    /// <summary>Null means "use the default" for this game.</summary>
    public bool? ControllerSupportOverrideFor(Game game) =>
        ControllerSupportOverrides.TryGetValue(game.Key, out var enabled) ? enabled : null;

    public AppSettings WithControllerSupportOverride(Game game, bool? enabled)
    {
        var overrides = new Dictionary<string, bool>(ControllerSupportOverrides);
        if (enabled is { } value)
        {
            overrides[game.Key] = value;
        }
        else
        {
            overrides.Remove(game.Key);
        }

        return this with { ControllerSupportOverrides = overrides };
    }
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
