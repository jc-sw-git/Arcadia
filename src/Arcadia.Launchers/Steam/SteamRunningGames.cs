using System.Globalization;
using System.Runtime.InteropServices;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Arcadia.Launchers.Steam;

/// <summary>What <see cref="SteamRunningGames"/> reads from Steam's registry; replaced in tests.</summary>
public interface ISteamAppsRegistry
{
    /// <summary><c>RunningAppID</c>: the game Steam started last, while it runs; else null.</summary>
    string? RunningAppId();

    /// <summary>Every app Steam keeps a key for under <c>Apps</c>; there may be thousands.</summary>
    IReadOnlyList<string> AppIds();

    /// <summary>Whether <c>Apps\&lt;id&gt;\Running</c> is 1.</summary>
    bool IsRunning(string appId);

    /// <summary>When Steam last wrote <c>Apps\&lt;id&gt;</c>: while the app runs, when it started.</summary>
    DateTimeOffset? WrittenAt(string appId);
}

/// <summary>
/// Steam's running games, from the registry: Steam sets <c>HKCU\Software\Valve\Steam\Apps\&lt;id&gt;\Running</c> to 1
/// while a game runs and <c>RunningAppID</c> to the game it started last (see docs/launchers/steam/running-games.md). Reading every
/// app's key is too much for each poll, so only a poll after a pause reads them all; the others read RunningAppID and
/// the games already running. A game that starts meanwhile becomes RunningAppID. The starts Steam aborts come from its
/// console log (<see cref="SteamConsoleLog"/>, #277).
/// </summary>
/// <param name="consoleLog">Steam's console log, or null where it isn't read (tests of the registry alone).</param>
public sealed partial class SteamRunningGames(TimeProvider time, ISteamAppsRegistry? registry = null, SteamConsoleLog? consoleLog = null) : IRunningGamesMonitor
{
    /// <summary>Polls further apart than this (Arcadia just started, or the PC slept) read every app again.</summary>
    public static readonly TimeSpan FullReadAfter = TimeSpan.FromSeconds(10);

    private readonly ISteamAppsRegistry _registry = registry ?? new SystemRegistry();
    private Dictionary<string, DateTimeOffset?> _running = new(StringComparer.Ordinal);
    private DateTimeOffset? _lastPoll;

    public string LauncherId => KnownLaunchers.Steam.Id;

    public IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var readAll = _lastPoll is not { } last || now - last > FullReadAfter;
        _lastPoll = now;

        var running = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        foreach (var appId in readAll ? _registry.AppIds() : [.. _running.Keys])
        {
            if (_registry.IsRunning(appId))
            {
                running[appId] = StartOf(appId);
            }
        }

        // RunningAppID alone counts: Arcadia relies on it for the Steam Controller already, the Running flag is younger knowledge.
        if (_registry.RunningAppId() is { } current)
        {
            running.TryAdd(current, StartOf(current));
        }

        _running = running;
        return running.Select(game => new RunningGame(game.Key, game.Value)).ToList();
    }

    public IReadOnlyList<RefusedStart> RefusedStarts() => consoleLog?.ReadRefusals() ?? [];

    // Read once per run: Steam may write the key again while the game runs.
    private DateTimeOffset? StartOf(string appId) => _running.TryGetValue(appId, out var known) ? known : _registry.WrittenAt(appId);

    private sealed partial class SystemRegistry : ISteamAppsRegistry
    {
        private const string AppsKey = @"Software\Valve\Steam\Apps";

        public string? RunningAppId() => SteamInstallation.RunningAppId()?.ToString(CultureInfo.InvariantCulture);

        public IReadOnlyList<string> AppIds()
        {
            if (!OperatingSystem.IsWindows())
            {
                return [];
            }

            using var apps = Registry.CurrentUser.OpenSubKey(AppsKey);
            return apps?.GetSubKeyNames().Where(name => name.Length > 0 && name.All(char.IsAsciiDigit)).ToList() ?? [];
        }

        public bool IsRunning(string appId)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var app = Registry.CurrentUser.OpenSubKey($@"{AppsKey}\{appId}");
            return app?.GetValue("Running") is int running && running != 0;
        }

        public DateTimeOffset? WrittenAt(string appId)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            using var app = Registry.CurrentUser.OpenSubKey($@"{AppsKey}\{appId}");
            return app is not null
                && RegQueryInfoKey(app.Handle, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, out var written) == 0
                ? DateTimeOffset.FromFileTime(written)
                : null;
        }

        // Only the last write time is wanted; every other output is left out (null).
        [LibraryImport("advapi32.dll", EntryPoint = "RegQueryInfoKeyW")]
        private static partial int RegQueryInfoKey(
            SafeRegistryHandle key, nint className, nint classLength, nint reserved, nint subKeys, nint maxSubKeyLength,
            nint maxClassLength, nint values, nint maxValueNameLength, nint maxValueLength, nint securityDescriptorLength, out long lastWriteTime);
    }
}
