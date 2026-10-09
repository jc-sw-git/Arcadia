using Arcadia.Core.Models;

namespace Arcadia.Core.Launchers;

/// <summary>What happens while a game is being started, for status messages in the UI.</summary>
public enum LaunchStage
{
    /// <summary>The launcher was not running and is being started in the background.</summary>
    StartingLauncher,

    /// <summary>The launcher is running; Arcadia should take the foreground back.</summary>
    LauncherReady,

    /// <summary>The start request was handed to the launcher or the executable.</summary>
    Starting,

    /// <summary>The game's process was seen.</summary>
    Running,

    /// <summary>Nothing to start (demo data).</summary>
    NotStartable,

    /// <summary>The game's process did not appear in time; the launcher may need attention.</summary>
    NotConfirmed,
}

/// <summary>Starts games of one launcher, including whatever the launcher needs first.</summary>
public interface ILauncherClient
{
    /// <summary>The launcher this client handles, or null for the fallback client.</summary>
    string? LauncherId { get; }

    Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default);
}
