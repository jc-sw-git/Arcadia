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
        .Select(g => Normalize(g.InstallDirectory!))

        // A drive root (bad launcher data) would make every program on that drive a game.
        .Where(folder => folder.Length > 0 && !(folder.Length == 2 && folder[1] == ':'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList());

    /// <summary>Whether <paramref name="executablePath"/> lies inside one of the folders (case-insensitive, as on Windows).</summary>
    public bool Contain(string? executablePath)
    {
        if (executablePath is null)
        {
            return false;
        }

        var path = Normalize(executablePath);
        return _folders.Exists(folder =>
            path.Length > folder.Length
            && path[folder.Length] == '\\'
            && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// One spelling per Windows path: launcher data writes "/" and doubled separators, Windows may report
    /// "\\?\" long paths, and a folder may hold "." or ".." steps. Path.GetFullPath would do this only on
    /// Windows and would read the drive's current folder; this stays a pure string operation.
    /// Junctions and 8.3 short names would need the file system and still don't match.
    /// </summary>
    private static string Normalize(string path)
    {
        path = path.Replace('/', '\\');
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[4..];
        }

        // A network path keeps its leading "\\".
        var prefix = path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" : string.Empty;
        var parts = new List<string>();
        foreach (var part in path[prefix.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (parts.Count > 1)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return prefix + string.Join('\\', parts);
    }
}
