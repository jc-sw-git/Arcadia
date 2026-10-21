using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// The install folders of the library's installed games. A process started from one of them is
/// that game, however it was started (from Arcadia, from its launcher, through a bootstrapper).
/// </summary>
public sealed class GameFolders
{
    // By normalized folder; a lookup walks up the program's path, so it costs the same with 50 games or 5,000.
    private readonly Dictionary<string, string> _folders;
    private readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private GameFolders(Dictionary<string, string> folders)
    {
        _folders = folders;
        _lookup = folders.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public static GameFolders Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public static GameFolders Of(IEnumerable<Game> games)
    {
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games.Where(g => g.IsInstalled && !string.IsNullOrWhiteSpace(g.InstallDirectory)))
        {
            // A drive root (bad launcher data) would make every program on that drive a game.
            var folder = Normalize(game.InstallDirectory!);
            if (folder.Length > 0 && !(folder.Length == 2 && folder[1] == ':'))
            {
                folders.TryAdd(folder, game.Key);
            }
        }

        return new(folders);
    }

    /// <summary>Whether <paramref name="executablePath"/> lies inside one of the folders (case-insensitive, as on Windows).</summary>
    public bool Contain(string? executablePath) => GameOf(executablePath) is not null;

    /// <summary>
    /// The <see cref="Game.Key"/> of the game whose folder holds <paramref name="executablePath"/>, or null. Of folders
    /// inside each other, the innermost one's game.
    /// </summary>
    public string? GameOf(string? executablePath)
    {
        if (executablePath is null || _folders.Count == 0)
        {
            return null;
        }

        var path = Normalize(executablePath);
        for (var end = path.LastIndexOf('\\'); end > 0; end = path.LastIndexOf('\\', end - 1))
        {
            if (_lookup.TryGetValue(path.AsSpan(0, end), out var gameKey))
            {
                return gameKey;
            }
        }

        return null;
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
