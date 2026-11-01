using System.Collections.Concurrent;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>The games of one launcher, as shown in its tab.</summary>
/// <param name="IsFound">False if the launcher is active but no longer installed on this PC: its tab says so (#136).</param>
/// <param name="ScanFailed">
/// True if its last scan threw (a locked file, a missing network drive): <paramref name="Games"/> are then the ones read last
/// since Arcadia started, and a tab without any says the scan failed instead of "keine Spiele" (#369).
/// </param>
public sealed record LauncherLibrary(LauncherInfo Launcher, IReadOnlyList<Game> Games, bool IsFound = true, bool ScanFailed = false);

/// <summary>Looks for the launchers on this PC and runs the scanners of the active ones.</summary>
public sealed class LibraryService(LauncherRegistry launchers, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _scannedAt = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<Game>> _lastRead = new(StringComparer.Ordinal);

    public LauncherRegistry Launchers => launchers;

    /// <returns>
    /// Every active launcher in tab order, also one without games or one that is no longer found: it keeps its tab, which
    /// says so (#136).
    /// </returns>
    public async Task<IReadOnlyList<LauncherLibrary>> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Launchers come and go between scans; without a choice of the user, the ones found decide which are active.
        await Task.Run(launchers.Locate, cancellationToken);
        return await Task.WhenAll(launchers.Enabled.Select(integration => ScanAsync(integration, cancellationToken)));
    }

    /// <summary>
    /// Reads one active launcher again, e.g. "Neu einlesen" on its page (#137), without scanning the others; null if it
    /// isn't active.
    /// </summary>
    public async Task<LauncherLibrary?> LoadAsync(string launcherId, CancellationToken cancellationToken = default)
    {
        // Its folder may just have been chosen (#138), so it is looked for again first.
        await Task.Run(launchers.Locate, cancellationToken);
        return launchers.Enabled.FirstOrDefault(i => i.Launcher.Id == launcherId) is { } integration
            ? await ScanAsync(integration, cancellationToken)
            : null;
    }

    private async Task<LauncherLibrary> ScanAsync(ILauncherIntegration integration, CancellationToken cancellationToken)
    {
        // With the name and keyword the user gave it (#137).
        var launcher = launchers.Info(integration.Launcher.Id);
        if (!launchers.IsFound(launcher.Id))
        {
            return new LauncherLibrary(launcher, [], IsFound: false);
        }

        try
        {
            var games = await integration.Scanner.ScanAsync(cancellationToken);
            _scannedAt[launcher.Id] = _time.GetUtcNow();
            _lastRead[launcher.Id] = games;
            return new LauncherLibrary(launcher, games);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One broken launcher must not take down the whole library, and a passing error (Steam writing a file) must not
            // empty a tab that showed games a moment ago: it keeps them and is marked as failed.
            System.Diagnostics.Trace.TraceWarning($"Scanning {launcher.DisplayName} failed: {ex}");
            return new LauncherLibrary(launcher, _lastRead.GetValueOrDefault(launcher.Id) ?? [], ScanFailed: true);
        }
    }

    /// <summary>When the launcher's games were last read; null if they never were since Arcadia started.</summary>
    public DateTimeOffset? ScannedAt(string launcherId) => _scannedAt.TryGetValue(launcherId, out var at) ? at : null;
}
