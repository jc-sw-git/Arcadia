using Arcadia.Core.Library;
using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>
/// The PC's processes for <see cref="ProcessGames"/>. Only on Windows: elsewhere no game runs that Arcadia could start,
/// and without paths every new process would look like the game just started.
/// </summary>
public sealed class SystemProcessTable : IProcessTable
{
    public IReadOnlyCollection<int> Ids() => OperatingSystem.IsWindows() ? ProcessWatch.RunningProcessIds() : [];

    public ProcessDetails? Describe(int processId) =>
        ProcessWatch.IsService(processId) == false
            ? new ProcessDetails(ProcessWatch.PathOf(processId), ProcessWatch.StartTimeOf(processId))
            : null;
}
