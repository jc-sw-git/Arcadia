using System.Text.Json;
using Arcadia.Core.Launchers;
using Arcadia.Core.Models;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Reads the Epic Games Launcher's install manifests. Epic writes one JSON file per installed
/// game to <c>%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>.
/// </summary>
public sealed class EpicScanner(string? manifestDirectory = null) : ILauncherScanner
{
    private readonly string _manifestDirectory = manifestDirectory ?? DefaultManifestDirectory();

    public LauncherInfo Launcher => KnownLaunchers.Epic;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<Game> Scan(CancellationToken cancellationToken)
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
        $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=launch&silent=true";

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
