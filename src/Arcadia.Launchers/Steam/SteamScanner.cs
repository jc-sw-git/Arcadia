using System.Globalization;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam.Account;

namespace Arcadia.Launchers.Steam;

/// <summary>Playtime and last start of one app, from the logged-in account's <c>localconfig.vdf</c>.</summary>
public sealed record SteamPlayStats(DateTimeOffset? LastPlayedAt, TimeSpan? Playtime);

/// <summary>
/// Reads the installed Steam apps: every library from <c>steamapps/libraryfolders.vdf</c>, one
/// <c>appmanifest_&lt;appid&gt;.acf</c> per installed app, playtime and last start from the
/// account's <c>localconfig.vdf</c>, each app's type from <c>appcache/appinfo.vdf</c> (#141). Games, software, tools
/// and soundtracks are delivered (the launcher's switches decide what is shown); redistributables, runtimes, DLC and
/// the like are left out. Owned but uninstalled games need a Steam login (#83): Steam's local package cache keeps
/// refunded games.
/// </summary>
/// <param name="locate">Finds Steam; defaults to <see cref="SteamInstallation.Find"/>.</param>
/// <param name="account">The Steam account signed in to Arcadia, whose games are added as not installed (#83); null for none.</param>
public sealed class SteamScanner(Func<SteamInstallation?>? locate = null, SteamOnlineAccount? account = null) : ILauncherScanner
{
    // EAppState bits in the manifest's StateFlags (verified on the owner's PC, 2026-10-12:
    // 4 = installed, 6 = installed with a pending update). Bit 64 (68/70) was only seen on
    // manifests nobody owns any more (see IsOwnedByNobody); only bit 2 counts as an update.
    private const int UpdateRequired = 2;
    private const int FullyInstalled = 4;
    private const int UpdateRunning = 256;
    private const int UpdateStarted = 1024;

    /// <summary>
    /// Apps Steam installs next to games that no library category shows: runtimes and redistributables. They never appear
    /// (#141). Names are matched too, because Proton and runtimes get new ids per version.
    /// </summary>
    private static readonly HashSet<uint> RuntimeAppIds =
    [
        228980, // Steamworks Common Redistributables
        1070560, // Steam Linux Runtime 1.0 (scout)
        1391110, // Steam Linux Runtime 2.0 (soldier)
        1628350, // Steam Linux Runtime 3.0 (sniper)
        1493710, // Proton Experimental
        2180100, // Proton Hotfix
    ];

    private static readonly string[] RuntimeNamePrefixes = ["Proton ", "Steam Linux Runtime", "Steamworks Common"];

    /// <summary>Tools known without <c>appinfo.vdf</c>: SDKs, SteamVR, the Steamworks test app.</summary>
    private static readonly HashSet<uint> ToolAppIds =
    [
        218, // Source SDK Base 2007
        243730, // Source SDK Base 2013 Singleplayer
        243750, // Source SDK Base 2013 Multiplayer
        250820, // SteamVR
        480, // Spacewar (Steamworks test app)
    ];

    private static readonly string[] ToolNamePrefixes = ["Source SDK Base"];

    private readonly Func<SteamInstallation?> _locate = locate ?? SteamInstallation.Find;
    private readonly AppTypeCache _appTypes = new();

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

        var manifests = LibraryPaths(steam.RootPath, ReadText(Path.Combine(steam.RootPath, "steamapps", "libraryfolders.vdf")))
            .SelectMany(library => ManifestFiles(library).Select(file => (Library: library, File: file)))
            .ToList();

        // Every scan asks for a fresh list; the account fetches it in the background and asks for a rescan when it changed.
        account?.FetchSoon();
        var owned = account?.OwnedGamesFor(steam.ActiveUserId is { } active and not 0 ? active : steam.LastLoggedInUserId()) ?? [];

        // The manifest's file name carries the app id, so the types are read before the manifests, in one pass.
        var appTypes = _appTypes.Read(
            Path.Combine(steam.RootPath, "appcache", "appinfo.vdf"),
            manifests.Select(m => AppIdOfManifestFile(m.File)).OfType<uint>().Concat(owned.Select(game => game.AppId)).ToHashSet());

        var games = new List<Game>();
        var installed = new HashSet<uint>();
        foreach (var (library, manifest) in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ParseManifest(ReadText(manifest), library, InstallDateOf, appTypes) is { } game
                && installed.Add(uint.Parse(game.Id, CultureInfo.InvariantCulture)))
            {
                games.Add(WithPlayStats(game, stats));
            }
        }

        if (owned.Count > 0)
        {
            var hidden = SteamHiddenGames.Read(steam.UserDataDirectory);
            games.AddRange(owned
                .Where(game => !installed.Contains(game.AppId) && !hidden.Contains(game.AppId))
                .Select(game => Uninstalled(game, appTypes))
                .OfType<Game>());
        }

        return games;
    }

    /// <summary>
    /// An owned game that isn't installed (#83): A installs it through <c>steam://install</c>, which opens Steam's install
    /// dialog; playtime and last start come from Steam's web API. Null for a type that never appears (runtimes, DLC).
    /// </summary>
    internal static Game? Uninstalled(SteamOwnedGame owned, IReadOnlyDictionary<uint, string>? appTypes)
    {
        if (TypeOf(owned.AppId, owned.Name, appTypes?.GetValueOrDefault(owned.AppId)) is not { } type)
        {
            return null;
        }

        return new Game
        {
            Id = owned.AppId.ToString(CultureInfo.InvariantCulture),
            LauncherId = KnownLaunchers.Steam.Id,
            Title = owned.Name,
            Type = type,
            IsInstalled = false,
            Launch = new LaunchTarget.ProtocolUri(RunUri(owned.AppId)),
            Install = new LaunchTarget.ProtocolUri(InstallUri(owned.AppId)),
            OpenInLauncher = new LaunchTarget.ProtocolUri(DetailsUri(owned.AppId)),
            Playtime = owned.PlaytimeMinutes > 0 ? TimeSpan.FromMinutes(owned.PlaytimeMinutes) : null,
            LastPlayedAt = owned.LastPlayedUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(owned.LastPlayedUnix) : null,
        };
    }

    /// <summary>The app id in <c>appmanifest_&lt;appid&gt;.acf</c>; null for another name.</summary>
    internal static uint? AppIdOfManifestFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string prefix = "appmanifest_";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
                ? appId
                : null;
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
    /// Turns one <c>appmanifest_*.acf</c> into a <see cref="Game"/>. Returns null for runtimes, redistributables and apps
    /// of a type Steam shows in no library category (DLC, video, config …), apps no account owns any more, apps that are
    /// still being installed for the first time and files that are no valid manifests.
    /// </summary>
    /// <param name="library">The library folder the manifest belongs to (the folder holding <c>steamapps</c>).</param>
    /// <param name="installDateOf">Looks up the install date for an install directory; Steam does not store one.</param>
    /// <param name="appTypes">The apps' types from <c>appinfo.vdf</c>; an app missing there is typed by its id and name.</param>
    public static Game? ParseManifest(
        string? acf,
        string library,
        Func<string, DateTimeOffset?>? installDateOf = null,
        IReadOnlyDictionary<uint, string>? appTypes = null)
    {
        if (TryParse(acf)?.GetMap("AppState") is not { } app
            || !uint.TryParse(app.GetString("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
            || app.GetString("name") is not { Length: > 0 } name
            || TypeOf(appId, name, appTypes?.GetValueOrDefault(appId)) is not { } type
            || IsOwnedByNobody(app.GetString("LastOwner"))
            || InstallDirectoryOf(library, app.GetString("installdir"), type) is not { } installDirectory)
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
            Type = type,
            Launch = new LaunchTarget.ProtocolUri(RunUri(appId)),
            OpenInLauncher = new LaunchTarget.ProtocolUri(DetailsUri(appId)),
            Uninstall = new LaunchTarget.ProtocolUri(UninstallUri(appId)),
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

    /// <summary>
    /// True for runtimes, redistributables, SDKs and soundtracks, which Steam installs like games; known by id and name,
    /// without <c>appinfo.vdf</c>. The install monitor doesn't follow them: Steam updates runtimes on its own.
    /// </summary>
    public static bool IsTool(uint appId, string name) =>
        IsRuntime(appId, name) || FallbackType(appId, name) is GameType.Tool or GameType.Soundtrack;

    /// <summary>
    /// The app's type (#141): from Steam's own type in <c>appinfo.vdf</c> when known, else by id and name as before
    /// <c>appinfo.vdf</c> was read. Null for what is never shown: runtimes and redistributables, and types Steam shows in
    /// no library category (DLC, video, config …). Demos count as games, as Steam shows them (owner, 2026-10-26).
    /// </summary>
    public static GameType? TypeOf(uint appId, string name, string? steamType)
    {
        if (IsRuntime(appId, name))
        {
            return null;
        }

        return steamType?.ToLowerInvariant() switch
        {
            null => FallbackType(appId, name),
            "game" or "demo" => GameType.Game,
            "application" => GameType.Software,
            "tool" => GameType.Tool,
            "music" => GameType.Soundtrack,
            _ => null,
        };
    }

    private static bool IsRuntime(uint appId, string name) =>
        RuntimeAppIds.Contains(appId) || RuntimeNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    // Without appinfo.vdf software can't be told from a game; tools and soundtracks are known as they were before #141.
    private static GameType FallbackType(uint appId, string name) =>
        name.EndsWith("Soundtrack", StringComparison.OrdinalIgnoreCase) ? GameType.Soundtrack
        : ToolAppIds.Contains(appId) || ToolNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? GameType.Tool
        : GameType.Game;

    // Steam keeps the manifest and files of an app whose license is gone, writes LastOwner 0 and
    // hides it from the library (owner's PC, 2026-10-20: the Half-Life 2 episodes, merged into
    // Half-Life 2, and Plan B: Terraform, which the owner never bought). A missing LastOwner
    // (older Steam) proves nothing, so only an explicit 0 drops the app.
    private static bool IsOwnedByNobody(string? lastOwner) => lastOwner == "0";

    public static string RunUri(uint appId) => $"steam://rungameid/{appId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The game's page in the Steam library.</summary>
    public static string DetailsUri(uint appId) => $"steam://nav/games/details/{appId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Opens Steam's install dialog; ignored for an installed game (docs/launchers/steam.md).</summary>
    public static string InstallUri(uint appId) => $"steam://install/{appId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Steam asks for confirmation itself before it deletes anything.</summary>
    public static string UninstallUri(uint appId) => $"steam://uninstall/{appId.ToString(CultureInfo.InvariantCulture)}";

    // A first-time install has no build yet and isn't playable; an installed game being updated
    // keeps its build id and stays in the library, even while Steam clears FullyInstalled.
    private static bool IsInstalled(int flags, string? buildId) =>
        (flags & FullyInstalled) != 0
        || ((flags & (UpdateRequired | UpdateRunning | UpdateStarted)) != 0 && ParseLong(buildId) is > 0);

    // The later of the account's and the manifest's last start: Steam needn't write both at the same moment, and right
    // after a game Arcadia rescans to sort by it (#95).
    private static Game WithPlayStats(Game game, IReadOnlyDictionary<uint, SteamPlayStats> stats) =>
        stats.TryGetValue(uint.Parse(game.Id, CultureInfo.InvariantCulture), out var played)
            ? game with { LastPlayedAt = Later(played.LastPlayedAt, game.LastPlayedAt), Playtime = played.Playtime }
            : game;

    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) => a > b || b is null ? a : b;

    // installdir comes from a file any local program can write; it must stay a single folder
    // name inside steamapps/common (or steamapps/music) so the path can't point anywhere else.
    private static string? InstallDirectoryOf(string library, string? installDir, GameType type)
    {
        if (string.IsNullOrWhiteSpace(installDir)
            || installDir is "." or ".."
            || installDir.IndexOfAny(['/', '\\', ':']) >= 0
            || installDir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        // Steam installs soundtracks into steamapps/music (owner's PC, 2026-10-27), under the same folder name as their
        // game's in common ("RSDragonwilds"): music wins, so a soundtrack never claims its game's folder.
        var common = Path.Combine(library, "steamapps", "common", installDir);
        var music = Path.Combine(library, "steamapps", "music", installDir);
        return type == GameType.Soundtrack && Directory.Exists(music) ? music : common;
    }

    internal static IEnumerable<string> ManifestFiles(string library)
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

    internal static string? ReadText(string path)
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

    /// <summary>
    /// The types read last from <c>appinfo.vdf</c>, kept while the file and the installed apps stay the same, so a rescan
    /// (every time Arcadia comes to the front) doesn't read the file again. A file that can't be read is said once in the
    /// log, and the apps are typed by id and name.
    /// </summary>
    internal sealed class AppTypeCache(Action<string>? warn = null)
    {
        private readonly Action<string> _warn = warn ?? (message => System.Diagnostics.Trace.TraceWarning(message));
        private readonly Lock _lock = new();
        private (string Path, long Length, DateTime Written)? _stamp;
        private IReadOnlySet<uint> _asked = new HashSet<uint>();
        private IReadOnlyDictionary<uint, string>? _types;

        // Whether the last read failed, so a file that stays missing or broken is logged once, not on every rescan.
        private bool _isUnreadable;

        public IReadOnlyDictionary<uint, string>? Read(string path, IReadOnlySet<uint> appIds)
        {
            lock (_lock)
            {
                (string, long, DateTime)? stamp;
                try
                {
                    var file = new FileInfo(path);
                    stamp = file.Exists ? (path, file.Length, file.LastWriteTimeUtc) : null;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    stamp = null;
                }

                if (stamp is not null && stamp == _stamp && appIds.IsSubsetOf(_asked))
                {
                    return _types;
                }

                _stamp = stamp;
                _asked = appIds;
                _types = stamp is null ? null : TryRead(path, appIds);
                if (_types is null && !_isUnreadable)
                {
                    _warn("Steam: appinfo.vdf nicht lesbar, Werkzeuge und Soundtracks werden am Namen erkannt, Software als Spiel");
                }

                _isUnreadable = _types is null;

                return _types;
            }
        }

        private static IReadOnlyDictionary<uint, string>? TryRead(string path, IReadOnlySet<uint> appIds)
        {
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var types = SteamAppInfo.ReadTypes(path, appIds);
                System.Diagnostics.Trace.TraceInformation($"Steam: Typen von {types.Count} der {appIds.Count} installierten Apps aus appinfo.vdf in {watch.ElapsedMilliseconds} ms gelesen");
                return types;
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Steam: appinfo.vdf: {e.Message}");
                return null;
            }
        }
    }
}
