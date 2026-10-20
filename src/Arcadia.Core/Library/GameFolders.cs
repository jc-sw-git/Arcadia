using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// The install folders of the library's installed games. A process started from one of them is
/// that game, however it was started (from Arcadia, from its launcher, through a bootstrapper).
/// </summary>
public sealed class GameFolders
{
    private readonly List<string> _folders;

    private GameFolders(List<string> folders) => _folders = folders;

    public static GameFolders Empty { get; } = new([]);

    public static GameFolders Of(IEnumerable<Game> games) => new(games
        .Where(g => g.IsInstalled && !string.IsNullOrWhiteSpace(g.InstallDirectory))
        .Select(g => g.InstallDirectory!.TrimEnd('\\', '/'))

        // A drive root (bad launcher data) would make every program on that drive a game.
        .Where(folder => folder.Length > 0 && !(folder.Length == 2 && folder[1] == ':'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList());

    /// <summary>Whether <paramref name="executablePath"/> lies inside one of the folders (case-insensitive, as on Windows).</summary>
    public bool Contain(string? executablePath) =>
        executablePath is not null && _folders.Exists(folder =>
            executablePath.Length > folder.Length
            && executablePath[folder.Length] is '\\' or '/'
            && executablePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
}
