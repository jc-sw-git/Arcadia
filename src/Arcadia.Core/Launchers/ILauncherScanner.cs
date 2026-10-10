using Arcadia.Core.Models;

namespace Arcadia.Core.Launchers;

/// <summary>Reads the games of one launcher: installed ones and, where the launcher reveals them, owned ones.</summary>
public interface ILauncherScanner
{
    LauncherInfo Launcher { get; }

    /// <summary>
    /// Returns one entry per game; owned but uninstalled games have <see cref="Game.IsInstalled"/>
    /// set to false. A launcher that is not installed yields an empty list,
    /// not an exception.
    /// </summary>
    Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default);
}
