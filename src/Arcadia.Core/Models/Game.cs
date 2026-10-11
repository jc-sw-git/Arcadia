namespace Arcadia.Core.Models;

/// <summary>A single game discovered by a launcher scanner.</summary>
public sealed record Game
{
    /// <summary>Launcher-native identifier (Epic AppName, Steam AppID, GOG product id, …).</summary>
    public required string Id { get; init; }

    /// <summary>The <see cref="LauncherInfo.Id"/> of the launcher the game belongs to.</summary>
    public required string LauncherId { get; init; }

    public required string Title { get; init; }

    public required LaunchTarget Launch { get; init; }

    /// <summary>
    /// False for games the user owns but has not installed. Install directory, size and install
    /// date are then unknown, and the primary action installs the game instead of starting it.
    /// </summary>
    public bool IsInstalled { get; init; } = true;

    /// <summary>How to ask the launcher to install the game; null if Arcadia can't trigger it.</summary>
    public LaunchTarget? Install { get; init; }

    public string? InstallDirectory { get; init; }

    /// <summary>
    /// The game's main executable, if known. Used to detect that a game started through its
    /// launcher is actually running.
    /// </summary>
    public string? ExecutablePath { get; init; }

    public DateTimeOffset? InstalledAt { get; init; }

    /// <summary>Null when the launcher does not report it or the game was never played.</summary>
    public DateTimeOffset? LastPlayedAt { get; init; }

    /// <summary>Total playtime as reported by the launcher, if it reports one.</summary>
    public TimeSpan? Playtime { get; init; }

    public long? InstallSizeBytes { get; init; }

    /// <summary>
    /// True when the launcher has an update pending. The primary action then starts the update
    /// in the launcher instead of the game.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>Stable key across scans: launcher id plus native id.</summary>
    public string Key => KeyOf(LauncherId, Id);

    /// <summary>The <see cref="Key"/> of a game known only by launcher and native id.</summary>
    public static string KeyOf(string launcherId, string id) => $"{launcherId}:{id}";
}
