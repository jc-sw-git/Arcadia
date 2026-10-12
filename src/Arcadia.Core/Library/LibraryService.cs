using Arcadia.Core.Launchers;
using Arcadia.Core.Models;

namespace Arcadia.Core.Library;

/// <summary>The games of one launcher, as shown in its tab.</summary>
public sealed record LauncherLibrary(LauncherInfo Launcher, IReadOnlyList<Game> Games);

/// <summary>Runs all scanners and collects the launchers that actually have games.</summary>
public sealed class LibraryService(IEnumerable<ILauncherScanner> scanners)
{
    private readonly IReadOnlyList<ILauncherScanner> _scanners = scanners.ToList();

    public async Task<IReadOnlyList<LauncherLibrary>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(_scanners.Select(async scanner =>
        {
            try
            {
                return new LauncherLibrary(scanner.Launcher, await scanner.ScanAsync(cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One broken launcher must not take down the whole library.
                System.Diagnostics.Trace.TraceWarning($"Scanning {scanner.Launcher.DisplayName} failed: {ex}");
                return new LauncherLibrary(scanner.Launcher, []);
            }
        }));

        // Keep the order of KnownLaunchers so tabs don't jump around between runs.
        return results
            .Where(r => r.Games.Count > 0)
            .OrderBy(r => IndexOf(r.Launcher))
            .ToList();
    }

    private static int IndexOf(LauncherInfo launcher)
    {
        for (var i = 0; i < KnownLaunchers.All.Count; i++)
        {
            if (KnownLaunchers.All[i].Id == launcher.Id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
