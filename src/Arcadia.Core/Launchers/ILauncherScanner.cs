using Arcadia.Core.Models;

namespace Arcadia.Core.Launchers;

/// <summary>Reads the installed games of one launcher.</summary>
public interface ILauncherScanner
{
    LauncherInfo Launcher { get; }

    /// <summary>
    /// Returns the installed games. A launcher that is not installed yields an empty list,
    /// not an exception.
    /// </summary>
    Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default);
}
