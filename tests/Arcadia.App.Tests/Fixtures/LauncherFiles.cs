using Arcadia.ControllerSupport;
using Arcadia.Launchers;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// The files of Steam and Epic in a temp folder of the test, for <see cref="ArcadiaDriver.StartWithLaunchersAsync"/>: the real
/// integrations read them as they read the owner's PC, without a launcher installed and on every platform. Neither launcher
/// exists until the test installs it.
/// </summary>
internal sealed class LauncherFiles : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-launcher-files-").FullName;

    public LauncherFiles()
    {
        Steam = new SteamFolder(Path.Combine(_root, "Steam"));
        Epic = new EpicFolder(Path.Combine(_root, "Epic"));
    }

    public SteamFolder Steam { get; }

    public EpicFolder Epic { get; }

    /// <summary>A path where a test can put anything else, e.g. a Steam library on another drive.</summary>
    public string PathFor(string name) => Path.Combine(_root, name);

    /// <summary>Started with <c>--demo</c>: demo libraries for the launchers without an integration.</summary>
    public bool Demo { get; init; }

    /// <summary>Steam Controller Support on a Windows PC with this Steam (<see cref="SteamPc"/>); else not available.</summary>
    public bool OnWindows { get; init; }

    public SteamPc? ControllerSupport => OnWindows ? field ??= new SteamPc(Steam) : null;

    public LauncherFolders Folders => new(Steam.Root, Epic.Folders);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A scan still reading; the temp folder is cleaned up by the system.
        }
    }
}
