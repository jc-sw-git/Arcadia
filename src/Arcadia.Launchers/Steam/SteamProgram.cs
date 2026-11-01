using System.Globalization;
using Arcadia.Launchers.Abstractions;
using Arcadia.Platform;

namespace Arcadia.Launchers.Steam;

/// <summary>Steam the program: closed and started again for the Steam Controller's drivers (#296).</summary>
/// <param name="locate">Finds Steam; the acceptance tests point it at a Steam folder of their own.</param>
/// <param name="isRunning">Whether Steam runs; <see cref="SteamInstallation.IsRunning"/> unless a test says.</param>
public sealed class SteamProgram(Func<SteamInstallation?> locate, Func<bool>? isRunning = null) : ILauncherProgram
{
    /// <summary>How long Steam may take to exit.</summary>
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    private readonly Func<bool> _isRunning = isRunning ?? (() => SteamInstallation.IsRunning);

    public string? Folder => locate()?.RootPath;

    public bool IsRunning => _isRunning();

    // Steam's process comes long before its window: the web helper, a signed-in user and a complete login.
    public bool IsReady =>
        _isRunning() && ProcessWatch.IsRunning("steamwebhelper")
        && locate() is { ActiveUserId: not (null or 0) } steam
        && SteamLoginLog.IsLatestLoginComplete(steam.LoginLogPath) is not false;

    public RunningLauncherGame? RunningGame() =>
        SteamInstallation.RunningAppId() is { } appId && SteamInstallation.RunningGameName() is { } name
            ? new(appId.ToString(CultureInfo.InvariantCulture), name)
            : null;

    public async Task<bool> CloseAsync(TimeProvider time, CancellationToken cancellationToken = default)
    {
        if (!_isRunning())
        {
            return true;
        }

        return await SteamInstallation.ShutdownAsync(ExitTimeout, time, cancellationToken);
    }

    public void Start() => locate()?.StartSilently();

    public int ShortcutsTo(string executable) => locate()?.AllShortcutsPaths().Count(file => HasShortcutTo(file, executable)) ?? 0;

    public void RemoveShortcutsTo(string executable)
    {
        foreach (var file in locate()?.AllShortcutsPaths() ?? [])
        {
            SteamShortcuts.RemoveByExecutable(file, executable);
        }
    }

    private static bool HasShortcutTo(string shortcuts, string executable)
    {
        try
        {
            return SteamShortcuts.FindByExecutable(SteamShortcuts.Read(shortcuts), executable) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            // An unreadable file can't be cleaned either; Steam rewrites it.
            return false;
        }
    }
}
