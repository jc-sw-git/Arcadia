using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>The games of one launcher, as shown in its tab.</summary>
public sealed record LauncherLibrary(LauncherInfo Launcher, IReadOnlyList<Game> Games);

/// <summary>Runs the scanners of the enabled launchers and collects the launchers that actually have games.</summary>
public sealed class LibraryService(LauncherRegistry launchers)
{
    /// <returns>In the registry's order, so tabs don't jump around between runs.</returns>
    public async Task<IReadOnlyList<LauncherLibrary>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(launchers.Enabled.Select(async integration =>
        {
            try
            {
                return new LauncherLibrary(integration.Launcher, await integration.Scanner.ScanAsync(cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One broken launcher must not take down the whole library.
                System.Diagnostics.Trace.TraceWarning($"Scanning {integration.Launcher.DisplayName} failed: {ex}");
                return new LauncherLibrary(integration.Launcher, []);
            }
        }));

        return results.Where(r => r.Games.Count > 0).ToList();
    }
}
