using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Reads the Epic library: installed games from the install manifests (one JSON file per game in
/// <c>%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>) plus the owned but
/// uninstalled games from the launcher's catalog cache (<see cref="EpicCatalog"/>). Last played and
/// pending updates come from the launcher's logs (<see cref="EpicLauncherLog"/>).
/// </summary>
public sealed class EpicScanner(
    string? manifestDirectory = null,
    string? catalogPath = null,
    string? logDirectory = null,
    EpicLastPlayedStore? lastPlayedStore = null) : ILauncherScanner
{
    private readonly string _manifestDirectory = manifestDirectory ?? DefaultManifestDirectory();
    private readonly string _catalogPath = catalogPath ?? EpicCatalog.DefaultPath();
    private readonly string _logDirectory = logDirectory ?? DefaultLogDirectory();
    private readonly EpicLastPlayedStore _lastPlayedStore = lastPlayedStore ?? EpicLastPlayedStore.CreateDefault();

    // Epic's rotated logs never change again; only the current one is parsed on every scan.
    private readonly Dictionary<string, (long Length, DateTime Written, EpicLogFacts Facts)> _parsedLogs = new(StringComparer.OrdinalIgnoreCase);

    public LauncherInfo Launcher => KnownLaunchers.Epic;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<Game> Scan(CancellationToken cancellationToken)
    {
        var logs = ReadLogs(cancellationToken);
        var installedApps = logs.LastOrDefault(facts => facts.InstalledApps is not null)?.InstalledApps;
        var installed = ScanManifests(installedApps, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var lastPlayed = _lastPlayedStore.Merge(logs.SelectMany(facts => facts.Sessions));
        return WithLastPlayed(Merge(installed, EpicCatalog.Load(_catalogPath)), lastPlayed);
    }

    public static IReadOnlyList<Game> WithLastPlayed(IReadOnlyList<Game> games, IReadOnlyDictionary<string, DateTimeOffset> lastPlayed) =>
        games.Select(game => lastPlayed.TryGetValue(game.Id, out var played) ? game with { LastPlayedAt = played } : game).ToList();

    /// <summary>
    /// True if the launcher's last report says the installed version has an update. A report about
    /// another version is out of date (the update was installed since), so it doesn't count.
    /// </summary>
    public static bool HasUpdate(IReadOnlyDictionary<string, EpicInstalledAppState>? installedApps, string appName, string? installedVersion) =>
        installedApps is not null
        && installedApps.TryGetValue(appName, out var state)
        && state.HasUpdate
        && string.Equals(state.InstalledVersion, installedVersion, StringComparison.Ordinal);

    /// <summary>Facts from every Epic log, oldest first.</summary>
    private List<EpicLogFacts> ReadLogs(CancellationToken cancellationToken)
    {
        var facts = new List<EpicLogFacts>();
        var files = EpicLauncherLog.Files(_logDirectory);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo file;
            try
            {
                file = new FileInfo(path);
                file.Refresh();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            lock (_parsedLogs)
            {
                if (!_parsedLogs.TryGetValue(path, out var cached) || cached.Length != file.Length || cached.Written != file.LastWriteTimeUtc)
                {
                    cached = (file.Length, file.LastWriteTimeUtc, EpicLauncherLog.ParseFile(path));
                    _parsedLogs[path] = cached;
                }

                facts.Add(cached.Facts);
            }
        }

        lock (_parsedLogs)
        {
            foreach (var gone in _parsedLogs.Keys.Except(files, StringComparer.OrdinalIgnoreCase).ToList())
            {
                _parsedLogs.Remove(gone);
            }
        }

        return facts;
    }

    /// <summary>
    /// Adds every catalog game that is not installed. Installed games are matched by catalog item
    /// id and, for manifests without one, by app name.
    /// </summary>
    public static IReadOnlyList<Game> Merge(IReadOnlyList<Game> installed, IReadOnlyList<EpicCatalogItem> catalog)
    {
        var installedIds = new HashSet<string>(StringComparer.Ordinal);
        var installedApps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var game in installed)
        {
            installedApps.Add(game.Id);
            if (game.Launch is LaunchTarget.ProtocolUri uri && CatalogItemIdOf(uri.Uri) is { } id)
            {
                installedIds.Add(id);
            }
        }

        var games = new List<Game>(installed);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in catalog)
        {
            if (installedIds.Contains(item.CatalogItemId) || installedApps.Contains(item.AppName) || !seen.Add(item.AppName))
            {
                continue;
            }

            games.Add(new Game
            {
                Id = item.AppName,
                LauncherId = KnownLaunchers.Epic.Id,
                Title = item.Title,
                Launch = new LaunchTarget.ProtocolUri(LaunchUri(item.Namespace, item.CatalogItemId, item.AppName)),
                IsInstalled = false,
                Install = new LaunchTarget.ProtocolUri(InstallUri(item.Namespace, item.CatalogItemId, item.AppName)),
                OpenInLauncher = new LaunchTarget.ProtocolUri(LibraryUri),
            });
        }

        return games;
    }

    private IReadOnlyList<Game> ScanManifests(IReadOnlyDictionary<string, EpicInstalledAppState>? installedApps, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_manifestDirectory))
        {
            return [];
        }

        var games = new List<Game>();
        foreach (var file in Directory.EnumerateFiles(_manifestDirectory, "*.item"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (ParseManifest(json, InstallDateOf, installedApps) is { } game)
            {
                games.Add(game);
            }
        }

        return games;
    }

    /// <summary>
    /// Turns one manifest into a <see cref="Game"/>. Returns null for DLCs, incomplete installs
    /// and files that are not valid manifests.
    /// </summary>
    /// <param name="installDateOf">Looks up the install date for an install directory; Epic does not store one.</param>
    /// <param name="installedApps">The launcher's last report on installed apps, for pending updates.</param>
    public static Game? ParseManifest(string json, Func<string, DateTimeOffset?>? installDateOf = null, IReadOnlyDictionary<string, EpicInstalledAppState>? installedApps = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var title = GetString(root, "DisplayName");
            var appName = GetString(root, "AppName");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(appName))
            {
                return null;
            }

            // DLC manifests point at their base game; they are not separate library entries.
            var mainGame = GetString(root, "MainGameAppName");
            if (!string.IsNullOrEmpty(mainGame) && !string.Equals(mainGame, appName, StringComparison.Ordinal))
            {
                return null;
            }

            if (root.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True)
            {
                return null;
            }

            var catalogNamespace = GetString(root, "CatalogNamespace") ?? string.Empty;
            var catalogItemId = GetString(root, "CatalogItemId") ?? string.Empty;
            var installLocation = GetString(root, "InstallLocation");
            var launchExecutable = GetString(root, "LaunchExecutable");
            long? installSize = root.TryGetProperty("InstallSize", out var size) && size.TryGetInt64(out var bytes) ? bytes : null;

            return new Game
            {
                Id = appName,
                LauncherId = KnownLaunchers.Epic.Id,
                Title = title,
                Launch = new LaunchTarget.ProtocolUri(LaunchUri(catalogNamespace, catalogItemId, appName)),
                OpenInLauncher = new LaunchTarget.ProtocolUri(LibraryUri),
                InstallDirectory = installLocation,
                ExecutablePath = installLocation is not null && !string.IsNullOrEmpty(launchExecutable)
                    ? Path.Combine(installLocation, launchExecutable)
                    : null,
                InstallSizeBytes = installSize,
                InstalledAt = installLocation is null ? null : installDateOf?.Invoke(installLocation),
                UpdateAvailable = HasUpdate(installedApps, appName, GetString(root, "AppVersionString")),
            };
        }
    }

    public static string LaunchUri(string catalogNamespace, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{AppPath(catalogNamespace, catalogItemId, appName)}?action=launch&silent=true";

    /// <summary>
    /// Epic has no link to a single game's page in the library, so this opens the library. It has
    /// no uninstall link either; games are uninstalled there.
    /// </summary>
    public const string LibraryUri = "com.epicgames.launcher://store/library";

    /// <summary>Opens the launcher's install dialog for the game.</summary>
    public static string InstallUri(string catalogNamespace, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{AppPath(catalogNamespace, catalogItemId, appName)}?action=install";

    // The ids come from files any local program can write; escaping keeps them from adding
    // their own path segments or query parameters to a URI that the shell executes.
    private static string AppPath(string catalogNamespace, string catalogItemId, string appName) =>
        $"{Uri.EscapeDataString(catalogNamespace)}%3A{Uri.EscapeDataString(catalogItemId)}%3A{Uri.EscapeDataString(appName)}";

    private static string? CatalogItemIdOf(string uri)
    {
        const string prefix = "com.epicgames.launcher://apps/";
        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = uri[prefix.Length..].Split('?')[0].Split("%3A");
        return parts.Length == 3 && parts[1].Length > 0 ? Uri.UnescapeDataString(parts[1]) : null;
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? InstallDateOf(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? new DateTimeOffset(Directory.GetCreationTimeUtc(directory), TimeSpan.Zero) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string DefaultLogDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EpicGamesLauncher", "Saved", "Logs");

    private static string DefaultManifestDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");
}
