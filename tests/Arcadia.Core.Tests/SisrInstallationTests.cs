using System.Globalization;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public sealed class SisrInstallationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-sisr-" + Guid.NewGuid());
    private readonly FakeSisrSystem _system = new();

    private string LocalAppData => Path.Combine(_root, "Local");

    private string Roaming => Path.Combine(_root, "Roaming");

    private string SteamRoot => Path.Combine(_root, "Steam");

    private string SisrExe => Path.Combine(LocalAppData, "SISR", "SISR.exe");

    private string LogDirectory => Path.Combine(Roaming, "Arcadia", "logs");

    private string ArcadiaViiper => Path.Combine(LocalAppData, "Arcadia", "current", "viiper.exe");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    [Trait("TestCase", "T214-1")]
    public void Without_SISR_and_its_marker_there_is_no_group_even_with_the_CEF_flag()
    {
        Touch(Path.Combine(SteamRoot, ".cef-enable-remote-debugging"));

        var found = Installation().Find();

        Assert.False(found.ShowsGroup);
        Assert.True(found.CefFlag);
    }

    [Fact]
    [Trait("TestCase", "T214-1")]
    public void SISR_installed_or_only_its_marker_left_shows_the_group()
    {
        Touch(SisrExe);
        Assert.True(Installation().Find() is { ShowsGroup: true, Program: true, HasMarker: false });

        Directory.Delete(Path.GetDirectoryName(SisrExe)!, recursive: true);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        Assert.True(Installation().Find() is { ShowsGroup: true, Program: false, MarkerAccounts: 1 });
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void Removing_takes_SISR_its_data_links_uninstall_entry_marker_CEF_flag_and_VIIPER_and_keeps_the_rest()
    {
        Touch(SisrExe);
        Touch(Path.Combine(LocalAppData, "SISR", ".initial_setup_done"));
        Touch(Path.Combine(LocalAppData, "SISR", "viiper.exe"));
        Touch(Path.Combine(Roaming, "SISR", "settings.yaml"));
        Touch(Path.Combine(Roaming, "SISR.exe", "EBWebView", "data"));
        Touch(Path.Combine(_root, "Desktop", "SISR.lnk"));
        Touch(Path.Combine(_root, "Programs", "SISR.lnk"));
        Touch(Path.Combine(_root, "Programs", "SISR (No Steam).lnk"));
        Touch(Path.Combine(_root, "Programs", "Other.lnk"));
        Touch(Path.Combine(LocalAppData, "VIIPER", "viiper.exe"));
        Touch(Path.Combine(Roaming, "VIIPER", "key"));
        Touch(Path.Combine(SteamRoot, ".cef-enable-remote-debugging"));
        Touch(Path.Combine(Roaming, "Arcadia", "settings.json"));
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(111), "Spiel", @"D:\Spiele\spiel.exe");
        _system.HasUninstallEntry = true;

        var result = Installation(arcadiaViiper: null).Remove(steamRunning: false);

        Assert.True(result.Complete, result.Cause);
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "SISR")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "SISR")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "SISR.exe")));
        Assert.Equal(["Other.lnk"], Directory.GetFiles(Path.Combine(_root, "Programs")).Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(_root, "Desktop", "SISR.lnk")));
        Assert.False(_system.HasUninstallEntry);
        Assert.False(File.Exists(Path.Combine(SteamRoot, ".cef-enable-remote-debugging")));
        Assert.Equal(["Spiel"], SteamShortcuts.Read(Shortcuts(111)).Select(s => s.Name));
        Assert.Equal([Path.Combine(LocalAppData, "VIIPER", "viiper.exe")], _system.ViiperUninstalls);
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "VIIPER")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "VIIPER")));
        Assert.True(File.Exists(Path.Combine(Roaming, "Arcadia", "settings.json")));
        Assert.False(Installation().Find().ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void VIIPERs_key_folder_stays_while_Arcadias_own_VIIPER_uses_it()
    {
        Touch(SisrExe);
        Touch(ArcadiaViiper);
        Touch(Path.Combine(Roaming, "VIIPER", "key"));

        Assert.True(Installation().Remove(steamRunning: false).Complete);

        Assert.True(File.Exists(Path.Combine(Roaming, "VIIPER", "key")));
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void A_standalone_VIIPER_that_runs_stays_with_its_key_folder()
    {
        Touch(SisrExe);
        var standalone = Path.Combine(LocalAppData, "VIIPER", "viiper.exe");
        Touch(standalone);
        Touch(Path.Combine(Roaming, "VIIPER", "key"));
        _system.Running.Add(new("viiper", new(7, standalone, null)));

        Assert.True(Installation(arcadiaViiper: null).Remove(steamRunning: false).Complete);

        Assert.True(File.Exists(standalone));
        Assert.Empty(_system.ViiperUninstalls);
        Assert.True(File.Exists(Path.Combine(Roaming, "VIIPER", "key")));
    }

    [Fact]
    [Trait("TestCase", "T214-5")]
    public void Only_markers_to_exactly_SISRs_folder_go_in_every_account()
    {
        var elsewhere = Path.Combine(_root, "Tools", "SISR", "SISR.exe");
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(222), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(222), "SISR Marker", elsewhere, "--marker");
        SteamShortcuts.Add(Shortcuts(333), "Spiel", @"D:\Spiele\spiel.exe");

        Assert.Equal(2, Installation().Find().MarkerAccounts);
        Assert.True(Installation().Remove(steamRunning: false).Complete);

        Assert.Empty(SteamShortcuts.Read(Shortcuts(111)));
        Assert.Equal([elsewhere], SteamShortcuts.Read(Shortcuts(222)).Select(s => s.Executable));
        Assert.Equal(["Spiel"], SteamShortcuts.Read(Shortcuts(333)).Select(s => s.Name));
        Assert.True(File.Exists(Shortcuts(111) + ".bak"));
    }

    [Fact]
    [Trait("TestCase", "T214-4")]
    public void With_Steam_running_the_marker_stays_and_the_group_with_it()
    {
        Touch(SisrExe);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");

        var result = Installation().Remove(steamRunning: true);

        Assert.False(result.Complete);
        Assert.Equal(SisrInstallation.SteamRunningCause, result.Cause);
        Assert.False(File.Exists(SisrExe));
        Assert.True(Installation().Find() is { ShowsGroup: true, Program: false, MarkerAccounts: 1 });
    }

    [Fact]
    [Trait("TestCase", "T214-8")]
    public void An_SISR_that_wont_stop_keeps_everything_and_says_why()
    {
        Touch(SisrExe);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        _system.Running.Add(new("SISR", new(5, SisrExe, "SISR.exe")));
        _system.Unstoppable.Add(5);

        var installation = Installation();
        Assert.False(installation.StopForRemoval());
        var result = installation.Remove(steamRunning: false);

        Assert.Equal(SisrInstallation.StopFailedCause, result.Cause);
        Assert.True(File.Exists(SisrExe));
        Assert.Equal(1, installation.Find().MarkerAccounts);
    }

    [Fact]
    [Trait("TestCase", "T214-8")]
    public void A_second_run_finishes_what_the_first_left_and_a_run_with_nothing_left_succeeds()
    {
        Touch(SisrExe);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        Assert.False(Installation().Remove(steamRunning: true).Complete);

        Assert.True(Installation().Remove(steamRunning: false).Complete);
        Assert.False(Installation().Find().ShowsGroup);
        Assert.True(Installation().Remove(steamRunning: false).Complete);
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void Removing_stops_SISR_and_its_VIIPER_from_its_folder_only()
    {
        Touch(SisrExe);
        var sisrViiper = Path.Combine(LocalAppData, "SISR", "viiper.exe");
        _system.Running.Add(new("SISR", new(5, SisrExe, null)));
        _system.Running.Add(new("SISR", new(6, Path.Combine(_root, "Tools", "SISR.exe"), null)));
        _system.Running.Add(new("viiper", new(7, sisrViiper, null)));
        _system.Running.Add(new("viiper", new(8, ArcadiaViiper, null)));

        Assert.True(Installation().StopForRemoval());

        Assert.Equal([5, 7], _system.Stopped);
    }

    [Theory]
    [Trait("TestCase", "T214-10")]
    [InlineData(true, "\"C:\\SISR\\SISR.exe\" --api.listen-address localhost:47640 --update-notify none --log.file {log}")]
    [InlineData(true, "SISR.exe --api.listen-address localhost:47640 --update-notify none --log.file \"{log}\"")]
    [InlineData(false, "SISR.exe")]
    [InlineData(false, "SISR.exe --api.listen-address localhost:47640")]
    [InlineData(false, "SISR.exe --api.listen-address localhost:47640 --log.file C:\\Temp\\sisr.log")]
    [InlineData(false, "SISR.exe --api.listen-address localhost:9000 --log.file {log}")]
    [InlineData(false, null)]
    public void Only_an_SISR_started_with_Arcadias_port_and_log_counts_as_the_old_Arcadias(bool expected, string? commandLine)
    {
        var line = commandLine?.Replace("{log}", Path.Combine(LogDirectory, "sisr.log"), StringComparison.Ordinal);

        Assert.Equal(expected, SisrInstallation.IsStartedByArcadia(line, [LogDirectory]));
    }

    [Fact]
    [Trait("TestCase", "T214-10")]
    public void At_start_only_the_SISR_the_old_Arcadia_started_is_stopped()
    {
        var arcadias = $"SISR.exe --api.listen-address localhost:47640 --update-notify none --log.file {Path.Combine(LogDirectory, "sisr.log")}";
        _system.Running.Add(new("SISR", new(5, SisrExe, arcadias)));
        _system.Running.Add(new("SISR", new(6, SisrExe, "SISR.exe")));
        _system.Running.Add(new("SISR", new(7, SisrExe, null)));

        Assert.Equal(1, Installation().StopStartedByOldArcadia());

        Assert.Equal([5], _system.Stopped);
        Assert.True(Installation().IsAnyRunning);
    }

    private SisrInstallation Installation() => Installation(ArcadiaViiper);

    private SisrInstallation Installation(string? arcadiaViiper) => new(
        new SisrPaths(LocalAppData, Roaming, Path.Combine(_root, "Programs"), Path.Combine(_root, "Desktop"), [LogDirectory], arcadiaViiper),
        new SteamInstallation(SteamRoot, 111),
        _system);

    private string Shortcuts(uint account)
    {
        var config = Path.Combine(SteamRoot, "userdata", account.ToString(CultureInfo.InvariantCulture), "config");
        Directory.CreateDirectory(config);
        return Path.Combine(config, "shortcuts.vdf");
    }

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private sealed class FakeSisrSystem : ISisrSystem
    {
        public List<(string Name, SisrProcess Process)> Running { get; } = [];

        public HashSet<int> Unstoppable { get; } = [];

        public List<int> Stopped { get; } = [];

        public List<string> ViiperUninstalls { get; } = [];

        public bool HasUninstallEntry { get; set; }

        public IReadOnlyList<SisrProcess> Processes(string name) =>
            Running.Where(r => r.Name == name).Select(r => r.Process).ToList();

        public bool Stop(int processId)
        {
            if (Unstoppable.Contains(processId))
            {
                return false;
            }

            Stopped.Add(processId);
            Running.RemoveAll(r => r.Process.Id == processId);
            return true;
        }

        public void DeleteUninstallEntry() => HasUninstallEntry = false;

        public void UninstallViiper(string viiperExecutable) => ViiperUninstalls.Add(viiperExecutable);
    }
}
