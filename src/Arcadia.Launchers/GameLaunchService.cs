using System.ComponentModel;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers;

/// <summary>How a start request ended, as far as Arcadia can tell.</summary>
public enum LaunchOutcome
{
    /// <summary>The request went to the launcher; its progress was reported as <see cref="LaunchStage"/>s.</summary>
    Requested,

    /// <summary>Another start was still running; this one was ignored.</summary>
    AlreadyLaunching,

    /// <summary>The start was cancelled.</summary>
    Cancelled,

    /// <summary>A known failure, e.g. a missing file or a denied process start; the message is meant for the user.</summary>
    Failed,

    /// <summary>An error nobody expected; it is logged with its stack trace.</summary>
    FailedUnexpectedly,
}

public sealed record LaunchResult(LaunchOutcome Outcome, Exception? Error = null);

/// <summary>Routes a start request to the client of the game's launcher.</summary>
public sealed class GameLaunchService(LauncherRegistry launchers)
{
    private readonly DirectLauncherClient _fallback = new();
    private int _launching;

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = launchers.Find(game.LauncherId)?.Client ?? _fallback;
        return client.StartAsync(game, progress, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="prepare"/> (e.g. pausing Steam Controller Support), then starts the
    /// game. Never throws: launcher clients may come from plugins, and a failed start must leave
    /// Arcadia running and ready for the next one. Only one start runs at a time.
    /// </summary>
    public async Task<LaunchResult> TryStartAsync(
        Game game,
        Func<CancellationToken, Task>? prepare = null,
        IProgress<LaunchStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _launching, 1) == 1)
        {
            return new(LaunchOutcome.AlreadyLaunching);
        }

        try
        {
            if (prepare is not null)
            {
                await prepare(cancellationToken);
            }

            await StartAsync(game, progress, cancellationToken);
            return new(LaunchOutcome.Requested);
        }
        catch (OperationCanceledException)
        {
            return new(LaunchOutcome.Cancelled);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Log?.Invoke($"Start von {game.Key} fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
            return new(LaunchOutcome.Failed, ex);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Start von {game.Key} unerwartet fehlgeschlagen: {ex}");
            return new(LaunchOutcome.FailedUnexpectedly, ex);
        }
        finally
        {
            Volatile.Write(ref _launching, 0);
        }
    }

    /// <summary>Failures of starting a process or reaching a file, whose message tells the user enough.</summary>
    private static bool IsExpected(Exception ex) =>
        ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException;

    /// <summary>Hands an uninstalled game to its launcher's install dialog.</summary>
    /// <returns>False if Arcadia has no way to install the game (demo data, unsupported launcher).</returns>
    public static bool Install(Game game) => Open(game.Install);

    /// <summary>Hands a launcher action (install, uninstall, show the game, …) to the launcher.</summary>
    /// <returns>False if there is nothing to open (no such action, or demo data).</returns>
    public static bool Open(LaunchTarget? target) => target is not null && DirectLauncherClient.Start(target);
}
