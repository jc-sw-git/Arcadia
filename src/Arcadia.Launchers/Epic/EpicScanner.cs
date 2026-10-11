using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Core.Models;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Reads the Epic library: installed games from the install manifests (one JSON file per game in
/// <c>%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>) plus the owned but
/// uninstalled games from the launcher's catalog cache (<see cref="EpicCatalog"/>).
/// </summary>
public sealed class EpicScanner(string? manifestDirectory = null, string? catalogPath = null) : ILauncherScanner
{
    private readonly string _manifestDirectory = manifestDirectory ?? DefaultManifestDirectory();
    private readonly string _catalogPath = catalogPath ?? EpicCatalog.DefaultPath();

    public LauncherInfo Launcher => KnownLaunchers.Epic;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<Game> Scan(CancellationToken cancellationToken)
    {
        var installed = ScanManifests(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Merge(installed, EpicCatalog.Load(_catalogPath));
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
            });
        }

        return games;
    }

    private IReadOnlyList<Game> ScanManifests(CancellationToken cancellationToken)
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

            if (ParseManifest(json, InstallDateOf) is { } game)
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
    public static Game? ParseManifest(string json, Func<string, DateTimeOffset?>? installDateOf = null)
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
                InstallDirectory = installLocation,
                ExecutablePath = installLocation is not null && !string.IsNullOrEmpty(launchExecutable)
                    ? Path.Combine(installLocation, launchExecutable)
                    : null,
                InstallSizeBytes = installSize,
                InstalledAt = installLocation is null ? null : installDateOf?.Invoke(installLocation),
            };
        }
    }

    public static string LaunchUri(string catalogNamespace, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{AppPath(catalogNamespace, catalogItemId, appName)}?action=launch&silent=true";

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

    private static string DefaultManifestDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");
}
