using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam: the installed games of all libraries. Games start through <c>steam://rungameid</c>,
/// which starts Steam itself when needed, so there is no client of its own.
/// </summary>
public sealed class SteamIntegration : ILauncherIntegration
{
    public LauncherInfo Launcher => KnownLaunchers.Steam;

    public ILauncherScanner Scanner { get; } = new SteamScanner();

    public ILauncherClient? Client => null;

    public IInstallMonitor? InstallMonitor => null;

    public string? Locate() => SteamInstallation.Find()?.RootPath;
}
