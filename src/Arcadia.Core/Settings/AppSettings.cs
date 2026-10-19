using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// "Steam Controller als Xbox 360 Gamepad verwenden": run SISR so the Steam Controller works as
    /// a system-wide Xbox 360 pad, in Arcadia and in games that were not started through Steam.
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

    /// <summary>The <see cref="LauncherInfo.Id"/> of the tab Arcadia opens with; null for the first tab.</summary>
    public string? LastTabLauncherId { get; init; }

    /// <summary>The on-screen keyboard's layout the user picked last; null for the default (German).</summary>
    public string? KeyboardLayout { get; init; }

    /// <summary>
    /// Replaces view values this version doesn't know — written by a newer Arcadia or edited by
    /// hand — with the defaults, instead of failing on them later.
    /// </summary>
    public AppSettings WithValidLibraryView() => this with
    {
        LibrarySort = Enum.IsDefined(LibrarySort.Field) && Enum.IsDefined(LibrarySort.Direction) ? LibrarySort : SortOrder.Default,
        PreferredPageSize = PageSizes.Options.Contains(PreferredPageSize) ? PreferredPageSize : PageSizes.Default,
    };

    /// <summary>
    /// Whether SISR should run while <paramref name="game"/> runs. Without an override it runs for
    /// every game except those from the Steam launcher, which use Steam Input natively — running
    /// SISR there would double every input.
    /// </summary>
    public bool ControllerSupportFor(Game game) =>
        SteamControllerSupport
        && (ControllerSupportOverrides.TryGetValue(game.Key, out var enabled) ? enabled : DefaultControllerSupportFor(game));

    /// <summary>The mode a game starts with: "Steam Input" for Steam games, "Xbox 360 Controller (SISR)" for all others.</summary>
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
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Path { get; } = path;

    public static SettingsStore CreateDefault() => new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arcadia", "settings.json"));

    /// <summary>
    /// Missing or unreadable files yield the defaults rather than an error. A file that can't be
    /// parsed is kept as <c>settings.json.broken</c>, because the next save would overwrite it.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
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
            return new AppSettings();
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

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, Path, overwrite: true);
    }
}
