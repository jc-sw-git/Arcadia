using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam: the installed games of all libraries. Games start through <c>steam://rungameid</c>,
/// which starts Steam itself when needed, so there is no client of its own. Installs, updates and
/// uninstalls are followed through the app manifests, running games through Steam's registry.
/// </summary>
public sealed class SteamIntegration(TimeProvider time) : ILauncherIntegration
{
    public LauncherInfo Launcher => KnownLaunchers.Steam;

    public ILauncherScanner Scanner { get; } = new SteamScanner();

    public ILauncherClient? Client => null;

    public IInstallMonitor? InstallMonitor { get; } = new SteamInstallMonitor();

    public IRunningGamesMonitor? RunningGames { get; } = new SteamRunningGames(time);

    public IReadOnlyCollection<string> ProgramNames { get; } = ["steam.exe", "steamwebhelper.exe"];

    public string? Locate() => SteamInstallation.Find()?.RootPath;
}
