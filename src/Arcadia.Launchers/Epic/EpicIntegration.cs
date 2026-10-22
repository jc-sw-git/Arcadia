using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>The Epic Games Launcher: library, starts through the launcher, install progress, running games.</summary>
public sealed class EpicIntegration(TimeProvider time) : ILauncherIntegration
{
    public LauncherInfo Launcher => KnownLaunchers.Epic;

    public ILauncherScanner Scanner { get; } = new EpicScanner();

    public ILauncherClient? Client { get; } = new EpicLauncherClient(time);

    public IInstallMonitor? InstallMonitor { get; } = new EpicInstallMonitor();

    public IRunningGamesMonitor? RunningGames { get; } = new EpicRunningGames();

    public IReadOnlyCollection<string> ProgramNames { get; } = ["EpicGamesLauncher.exe", "EpicWebHelper.exe"];

    public string? Locate() => EpicLauncherClient.FindLauncherExecutable();
}
