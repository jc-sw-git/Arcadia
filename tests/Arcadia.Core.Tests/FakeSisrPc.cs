using System.Globalization;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Steam;

namespace Arcadia.Tests.Sisr;

/// <summary>
/// A PC an older Arcadia left SISR on (#214), in a temp folder: SISR's folders, Steam's shortcuts.vdf per account and
/// SISR's processes. Shared with the acceptance tests.
/// </summary>
public sealed class FakeSisrPc : IDisposable
{
    public const uint Account = 111;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-sisr-" + Guid.NewGuid());

    public FakeSisrSystem System { get; } = new();

    public string LocalAppData => Path.Combine(_root, "Local");

    public string Roaming => Path.Combine(_root, "Roaming");

    public string Root => _root;

    public string SteamRoot => Path.Combine(_root, "Steam");

    public string SisrExe => Path.Combine(LocalAppData, "SISR", "SISR.exe");

    public string LogDirectory => Path.Combine(Roaming, "Arcadia", "logs");

    public string ArcadiaViiper => Path.Combine(LocalAppData, "Arcadia", "current", "viiper.exe");

    public string CefFlag => Path.Combine(SteamRoot, ".cef-enable-remote-debugging");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>SISR as its installer and an older Arcadia left it: the program, its marker in Steam and the CEF flag.</summary>
    public FakeSisrPc WithSisr(bool program = true, bool marker = true)
    {
        if (program)
        {
            Touch(SisrExe);
        }

        if (marker)
        {
            SteamShortcuts.Add(Shortcuts(Account), "SISR Marker", SisrExe, "--marker");
            Touch(CefFlag);
        }

        return this;
    }

    public SisrInstallation Installation() => Installation(ArcadiaViiper);

    public SisrInstallation Installation(string? arcadiaViiper) => new(
        new SisrPaths(LocalAppData, Roaming, Path.Combine(_root, "Programs"), Path.Combine(_root, "Desktop"), [LogDirectory], arcadiaViiper),
        new SteamInstallation(SteamRoot, Account),
        System);

    public string Shortcuts(uint account)
    {
        var config = Path.Combine(SteamRoot, "userdata", account.ToString(CultureInfo.InvariantCulture), "config");
        Directory.CreateDirectory(config);
        return Path.Combine(config, "shortcuts.vdf");
    }

    public static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }
}

/// <summary>SISR's and VIIPER's processes and SISR's uninstall entry, as <see cref="WindowsSisrSystem"/> sees them.</summary>
public sealed class FakeSisrSystem : ISisrSystem
{
    private readonly Lock _lock = new();

    public List<(string Name, SisrProcess Process)> Running { get; } = [];

    public HashSet<int> Unstoppable { get; } = [];

    public List<int> Stopped { get; } = [];

    public List<string> ViiperUninstalls { get; } = [];

    public bool HasUninstallEntry { get; set; }

    /// <summary>An SISR the user started from its install folder.</summary>
    public void RunSisr(string executable, int id = 4711, string? commandLine = null)
    {
        lock (_lock)
        {
            Running.Add(("SISR", new SisrProcess(id, executable, commandLine ?? $"\"{executable}\"")));
        }
    }

    public IReadOnlyList<SisrProcess> Processes(string name)
    {
        lock (_lock)
        {
            return Running.Where(r => r.Name == name).Select(r => r.Process).ToList();
        }
    }

    public bool Stop(int processId)
    {
        lock (_lock)
        {
            if (Unstoppable.Contains(processId))
            {
                return false;
            }

            Stopped.Add(processId);
            Running.RemoveAll(r => r.Process.Id == processId);
            return true;
        }
    }

    /// <summary>The registry refuses to delete SISR's uninstall entry, as for a key another program holds.</summary>
    public bool UninstallEntryLocked { get; set; }

    public void DeleteUninstallEntry() =>
        HasUninstallEntry = UninstallEntryLocked ? throw new UnauthorizedAccessException("Zugriff verweigert") : false;

    public void UninstallViiper(string viiperExecutable) => ViiperUninstalls.Add(viiperExecutable);

    public int SteamLayoutResets { get; private set; }

    public int ViiperDetaches { get; private set; }

    public void ResetSteamInputLayout() => SteamLayoutResets++;

    public void DetachViiperDevices() => ViiperDetaches++;
}
