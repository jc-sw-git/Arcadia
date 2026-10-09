using System.Text.Json;
using Arcadia.Core.Models;

namespace Arcadia.Core.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// Run SISR so the Steam Controller (and anything else using Steam Input) works as a
    /// system-wide gamepad — in Arcadia and in games that were not started through Steam.
    /// </summary>
    public bool SteamControllerSupport { get; init; }

    /// <summary>Per-game override of <see cref="SteamControllerSupport"/>, keyed by <see cref="Game.Key"/>.</summary>
    public Dictionary<string, bool> ControllerSupportOverrides { get; init; } = [];

    /// <summary>When Arcadia last asked GitHub for a newer SISR.</summary>
    public DateTimeOffset? LastSisrUpdateCheck { get; init; }

    /// <summary>When Arcadia last installed the usbip driver; a Windows restart is due after that.</summary>
    public DateTimeOffset? UsbipInstalledAt { get; init; }

    /// <summary>
    /// Whether SISR should run while <paramref name="game"/> runs. Without an override it runs for
    /// every game except those from the Steam launcher, which use Steam Input natively — running
    /// SISR there would double every input.
    /// </summary>
    public bool ControllerSupportFor(Game game) =>
        SteamControllerSupport
        && (ControllerSupportOverrides.TryGetValue(game.Key, out var enabled) ? enabled : game.LauncherId != KnownLaunchers.Steam.Id);

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

    /// <summary>Missing or unreadable files yield the defaults rather than an error.</summary>
    public AppSettings Load()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
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
