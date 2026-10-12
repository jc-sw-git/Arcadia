using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>A fictional library for one launcher (<c>--demo</c>); its games can't be started or installed.</summary>
public sealed class DemoIntegration(LauncherInfo launcher, int gameCount, DateTimeOffset now) : ILauncherIntegration
{
    public LauncherInfo Launcher => launcher;

    public ILauncherScanner Scanner { get; } = new DemoScanner(launcher, gameCount, now);

    public ILauncherClient? Client => null;

    public IInstallMonitor? InstallMonitor => null;

    public string? Locate() => "Demo-Daten";
}
