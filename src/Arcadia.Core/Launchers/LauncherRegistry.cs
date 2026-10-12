using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

/// <summary>
/// The one place launchers are registered. Scanning, starting and install tracking read the
/// launchers from here and only see the enabled ones.
/// </summary>
public sealed class LauncherRegistry
{
    private readonly IReadOnlyList<ILauncherIntegration> _all;
    private readonly Func<string, bool> _isEnabled;

    /// <param name="integrations">In tab order.</param>
    /// <param name="isEnabled">Asked on every read with the launcher id, so a change applies at once.</param>
    public LauncherRegistry(IEnumerable<ILauncherIntegration> integrations, Func<string, bool> isEnabled)
    {
        _isEnabled = isEnabled;

        // A broken integration is left out rather than stopping Arcadia; later it may come from a plugin.
        var accepted = new List<ILauncherIntegration>();
        var rejected = new List<string>();
        foreach (var integration in integrations)
        {
            var id = integration.Launcher.Id;
            if (accepted.Any(i => i.Launcher.Id == id))
            {
                rejected.Add($"Launcher „{id}“ ist doppelt registriert.");
            }
            else if (integration.Scanner.Launcher.Id != id
                || (integration.Client is { LauncherId: var clientId } && clientId != id)
                || (integration.InstallMonitor is { LauncherId: var monitorId } && monitorId != id))
            {
                // Games, tabs and installs are matched by launcher id; a part with another id would never match.
                rejected.Add($"Launcher „{id}“ enthält Teile eines anderen Launchers.");
            }
            else
            {
                accepted.Add(integration);
            }
        }

        _all = accepted;
        Rejected = rejected;
    }

    /// <summary>Why integrations were left out, for the log; empty if all were accepted.</summary>
    public IReadOnlyList<string> Rejected { get; }

    /// <summary>Every registered launcher, enabled or not, e.g. for the launcher selection.</summary>
    public IReadOnlyList<ILauncherIntegration> All => _all;

    /// <summary>The launchers the user has enabled, in tab order.</summary>
    public IReadOnlyList<ILauncherIntegration> Enabled => _all.Where(i => _isEnabled(i.Launcher.Id)).ToList();

    /// <summary>The enabled launcher with this id, or null.</summary>
    public ILauncherIntegration? Find(string launcherId) =>
        _all.FirstOrDefault(i => i.Launcher.Id == launcherId && _isEnabled(launcherId));
}
