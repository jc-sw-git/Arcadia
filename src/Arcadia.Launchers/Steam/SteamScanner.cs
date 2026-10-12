using System.Globalization;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>Playtime and last start of one app, from the logged-in account's <c>localconfig.vdf</c>.</summary>
public sealed record SteamPlayStats(DateTimeOffset? LastPlayedAt, TimeSpan? Playtime);

/// <summary>
/// Reads the installed Steam games: every library from <c>steamapps/libraryfolders.vdf</c>, one
/// <c>appmanifest_&lt;appid&gt;.acf</c> per installed app, playtime and last start from the
/// account's <c>localconfig.vdf</c>. Tools and redistributables are left out.
/// </summary>
/// <param name="locate">Finds Steam; defaults to <see cref="SteamInstallation.Find"/>.</param>
public sealed class SteamScanner(Func<SteamInstallation?>? locate = null) : ILauncherScanner
{
    // EAppState bits in the manifest's StateFlags (verified on the owner's PC, 2026-10-12:
    // 4 = installed, 6 = installed with a pending update, 68/70 = the same while the app runs).
    private const int UpdateRequired = 2;
    private const int FullyInstalled = 4;
    private const int UpdateRunning = 256;
    private const int UpdateStarted = 1024;

    /// <summary>
    /// Apps Steam installs next to games that are no games: runtimes, redistributables, SDKs.
    /// Names are matched too (<see cref="IsTool"/>), because Proton and runtimes get new ids per version.
    /// </summary>
    private static readonly HashSet<uint> ToolAppIds =
    [
        218, // Source SDK Base 2007
        228980, // Steamworks Common Redistributables
        243730, // Source SDK Base 2013 Singleplayer
        243750, // Source SDK Base 2013 Multiplayer
        250820, // SteamVR
        480, // Spacewar (Steamworks test app)
        1070560, // Steam Linux Runtime 1.0 (scout)
        1391110, // Steam Linux Runtime 2.0 (soldier)
        1628350, // Steam Linux Runtime 3.0 (sniper)
        1493710, // Proton Experimental
        2180100, // Proton Hotfix
    ];

    private static readonly string[] ToolNamePrefixes = ["Proton ", "Steam Linux Runtime", "Steamworks Common", "Source SDK Base"];

    private readonly Func<SteamInstallation?> _locate = locate ?? SteamInstallation.Find;

    public LauncherInfo Launcher => KnownLaunchers.Steam;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<Game> Scan(CancellationToken cancellationToken)
    {
        if (_locate() is not { } steam)
        {
            return [];
        }

        var stats = steam.UserDataDirectory is { } userData
            ? ReadPlayStats(ReadText(Path.Combine(userData, "config", "localconfig.vdf")))
            : new Dictionary<uint, SteamPlayStats>();

        var games = new List<Game>();
        var seen = new HashSet<uint>();
        foreach (var library in LibraryPaths(steam.RootPath, ReadText(Path.Combine(steam.RootPath, "steamapps", "libraryfolders.vdf"))))
        {
            foreach (var manifest in ManifestFiles(library))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ParseManifest(ReadText(manifest), library, InstallDateOf) is { } game
                    && seen.Add(uint.Parse(game.Id, CultureInfo.InvariantCulture)))
                {
                    games.Add(WithPlayStats(game, stats));
                }
            }
        }

        return games;
    }

    /// <summary>
    /// The library folders Steam knows, normalized and without duplicates. Steam's own folder is
    /// always one, even if <c>libraryfolders.vdf</c> is missing or broken.
    /// </summary>
    public static IReadOnlyList<string> LibraryPaths(string steamRoot, string? libraryFoldersVdf)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (NormalizePath(path) is { } normalized && seen.Add(normalized))
            {
                paths.Add(normalized);
            }
        }

        Add(steamRoot);
        if (TryParse(libraryFoldersVdf)?.GetMap("libraryfolders") is { } folders)
        {
            foreach (var (_, value) in folders)
            {
                Add((value as VdfMap)?.GetString("path"));
            }
        }

        return paths;
    }

    /// <summary>
    /// Turns one <c>appmanifest_*.acf</c> into a <see cref="Game"/>. Returns null for tools, apps
    /// that are still being installed for the first time and files that are no valid manifests.
    /// </summary>
    /// <param name="library">The library folder the manifest belongs to (the folder holding <c>steamapps</c>).</param>
    /// <param name="installDateOf">Looks up the install date for an install directory; Steam does not store one.</param>
    public static Game? ParseManifest(string? acf, string library, Func<string, DateTimeOffset?>? installDateOf = null)
    {
        if (TryParse(acf)?.GetMap("AppState") is not { } app
            || !uint.TryParse(app.GetString("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
            || app.GetString("name") is not { Length: > 0 } name
            || IsTool(appId, name)
            || InstallDirectoryOf(library, app.GetString("installdir")) is not { } installDirectory)
        {
            return null;
        }

        var flags = ParseInt(app.GetString("StateFlags")) ?? 0;
        if (!IsInstalled(flags, app.GetString("buildid")))
        {
            return null;
        }

        var lastPlayed = ParseUnixTime(app.GetString("LastPlayed"));
        return new Game
        {
            Id = appId.ToString(CultureInfo.InvariantCulture),
            LauncherId = KnownLaunchers.Steam.Id,
            Title = name,
            Launch = new LaunchTarget.ProtocolUri(RunUri(appId)),
            InstallDirectory = installDirectory,
            InstallSizeBytes = ParseLong(app.GetString("SizeOnDisk")) is > 0 and var size ? size : null,
            InstalledAt = installDateOf?.Invoke(installDirectory),
            LastPlayedAt = lastPlayed,
            UpdateAvailable = (flags & UpdateRequired) != 0,
        };
    }

    /// <summary>
    /// Reads <c>UserLocalConfigStore/Software/Valve/Steam/apps</c>. The file also holds app
    /// tickets elsewhere; only this block is read, and nothing of it is logged.
    /// </summary>
    public static IReadOnlyDictionary<uint, SteamPlayStats> ReadPlayStats(string? localConfigVdf)
    {
        var stats = new Dictionary<uint, SteamPlayStats>();
        var apps = TryParse(localConfigVdf)?.GetMap("UserLocalConfigStore")?.GetMap("Software")?.GetMap("Valve")?.GetMap("Steam")?.GetMap("apps");
        foreach (var (key, value) in apps ?? [])
        {
            if (value is VdfMap app && uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
            {
                var minutes = ParseLong(app.GetString("Playtime"));
                stats[appId] = new SteamPlayStats(
                    ParseUnixTime(app.GetString("LastPlayed")),
                    minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : null);
            }
        }

        return stats;
    }

    /// <summary>True for runtimes, redistributables, SDKs and soundtracks, which Steam installs like games.</summary>
    public static bool IsTool(uint appId, string name) =>
        ToolAppIds.Contains(appId)
        || ToolNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        || name.EndsWith("Soundtrack", StringComparison.OrdinalIgnoreCase);

    public static string RunUri(uint appId) => $"steam://rungameid/{appId.ToString(CultureInfo.InvariantCulture)}";

    // A first-time install has no build yet and isn't playable; an installed game being updated
    // keeps its build id and stays in the library, even while Steam clears FullyInstalled.
    private static bool IsInstalled(int flags, string? buildId) =>
        (flags & FullyInstalled) != 0
        || ((flags & (UpdateRequired | UpdateRunning | UpdateStarted)) != 0 && ParseLong(buildId) is > 0);

    private static Game WithPlayStats(Game game, IReadOnlyDictionary<uint, SteamPlayStats> stats) =>
        stats.TryGetValue(uint.Parse(game.Id, CultureInfo.InvariantCulture), out var played)
            ? game with { LastPlayedAt = played.LastPlayedAt ?? game.LastPlayedAt, Playtime = played.Playtime }
            : game;

    // installdir comes from a file any local program can write; it must stay a single folder
    // name inside steamapps/common so the path can't point anywhere else.
    private static string? InstallDirectoryOf(string library, string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)
            || installDir is "." or ".."
            || installDir.IndexOfAny(['/', '\\', ':']) >= 0
            || installDir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return Path.Combine(library, "steamapps", "common", installDir);
    }

    private static IEnumerable<string> ManifestFiles(string library)
    {
        var steamApps = Path.Combine(library, "steamapps");
        try
        {
            return Directory.Exists(steamApps) ? Directory.GetFiles(steamApps, "appmanifest_*.acf") : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Steam writes paths like <c>c:/program files (x86)/steam</c>; compare them as Windows does.</summary>
    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static VdfMap? TryParse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            return TextVdf.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;

    // 0 means "never"; anything outside the range DateTimeOffset can hold is a broken file.
    private static DateTimeOffset? ParseUnixTime(string? value) =>
        ParseLong(value) is > 0 and < 253402300800 and var seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    private static DateTimeOffset? InstallDateOf(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? new DateTimeOffset(Directory.GetCreationTimeUtc(directory), TimeSpan.Zero) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
