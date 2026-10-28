using Arcadia.ControllerSupport;
using Arcadia.Tests.Sisr;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public sealed class SisrInstallationTests : IDisposable
{
    private readonly FakeSisrPc _pc = new();

    private string LocalAppData => _pc.LocalAppData;

    private string Roaming => _pc.Roaming;

    private string SteamRoot => _pc.SteamRoot;

    private string SisrExe => _pc.SisrExe;

    private string LogDirectory => _pc.LogDirectory;

    private string ArcadiaViiper => _pc.ArcadiaViiper;

    public void Dispose() => _pc.Dispose();

    [Fact]
    [Trait("TestCase", "T214-1")]
    public void Without_SISR_and_its_marker_there_is_no_group_even_with_the_CEF_flag()
    {
        Touch(Path.Combine(SteamRoot, ".cef-enable-remote-debugging"));

        var found = _pc.Installation().Find();

        Assert.False(found.ShowsGroup);
        Assert.True(found.CefFlag);
    }

    [Fact]
    [Trait("TestCase", "T214-1")]
    public void SISR_installed_or_only_its_marker_left_shows_the_group()
    {
        Touch(SisrExe);
        Assert.True(_pc.Installation().Find() is { ShowsGroup: true, Program: true, HasMarker: false });

        Directory.Delete(Path.GetDirectoryName(SisrExe)!, recursive: true);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        Assert.True(_pc.Installation().Find() is { ShowsGroup: true, Program: false, MarkerAccounts: 1 });
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
        Touch(Path.Combine(_pc.Root, "Desktop", "SISR.lnk"));
        Touch(Path.Combine(_pc.Root, "Programs", "SISR.lnk"));
        Touch(Path.Combine(_pc.Root, "Programs", "SISR (No Steam).lnk"));
        Touch(Path.Combine(_pc.Root, "Programs", "Other.lnk"));
        Touch(Path.Combine(LocalAppData, "VIIPER", "viiper.exe"));
        Touch(Path.Combine(Roaming, "VIIPER", "key"));
        Touch(Path.Combine(SteamRoot, ".cef-enable-remote-debugging"));
        Touch(Path.Combine(Roaming, "Arcadia", "settings.json"));
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(111), "Spiel", @"D:\Spiele\spiel.exe");
        _pc.System.HasUninstallEntry = true;

        var result = _pc.Installation(arcadiaViiper: null).Remove(steamRunning: false);

        Assert.True(result.Complete, result.Cause);
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "SISR")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "SISR")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "SISR.exe")));
        Assert.Equal(["Other.lnk"], Directory.GetFiles(Path.Combine(_pc.Root, "Programs")).Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(_pc.Root, "Desktop", "SISR.lnk")));
        Assert.False(_pc.System.HasUninstallEntry);
        Assert.False(File.Exists(Path.Combine(SteamRoot, ".cef-enable-remote-debugging")));
        Assert.Equal(["Spiel"], SteamShortcuts.Read(Shortcuts(111)).Select(s => s.Name));
        Assert.Equal([Path.Combine(LocalAppData, "VIIPER", "viiper.exe")], _pc.System.ViiperUninstalls);
        Assert.False(Directory.Exists(Path.Combine(LocalAppData, "VIIPER")));
        Assert.False(Directory.Exists(Path.Combine(Roaming, "VIIPER")));
        Assert.True(File.Exists(Path.Combine(Roaming, "Arcadia", "settings.json")));
        Assert.False(_pc.Installation().Find().ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void VIIPERs_key_folder_stays_while_Arcadias_own_VIIPER_uses_it()
    {
        Touch(SisrExe);
        Touch(ArcadiaViiper);
        Touch(Path.Combine(Roaming, "VIIPER", "key"));

        Assert.True(_pc.Installation().Remove(steamRunning: false).Complete);

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
        _pc.System.Running.Add(new("viiper", new(7, standalone, null)));

        Assert.True(_pc.Installation(arcadiaViiper: null).Remove(steamRunning: false).Complete);

        Assert.True(File.Exists(standalone));
        Assert.Empty(_pc.System.ViiperUninstalls);
        Assert.True(File.Exists(Path.Combine(Roaming, "VIIPER", "key")));
    }

    [Fact]
    [Trait("TestCase", "T214-5")]
    public void Only_markers_to_exactly_SISRs_folder_go_in_every_account()
    {
        var elsewhere = Path.Combine(_pc.Root, "Tools", "SISR", "SISR.exe");
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(222), "SISR Marker", SisrExe, "--marker");
        SteamShortcuts.Add(Shortcuts(222), "SISR Marker", elsewhere, "--marker");
        SteamShortcuts.Add(Shortcuts(333), "Spiel", @"D:\Spiele\spiel.exe");

        Assert.Equal(2, _pc.Installation().Find().MarkerAccounts);
        Assert.True(_pc.Installation().Remove(steamRunning: false).Complete);

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

        var result = _pc.Installation().Remove(steamRunning: true);

        Assert.False(result.Complete);
        Assert.Equal(SisrInstallation.SteamRunningCause, result.Cause);
        Assert.False(File.Exists(SisrExe));
        Assert.True(_pc.Installation().Find() is { ShowsGroup: true, Program: false, MarkerAccounts: 1 });
    }

    [Fact]
    [Trait("TestCase", "T214-8")]
    public void An_SISR_that_wont_stop_keeps_everything_and_says_why()
    {
        Touch(SisrExe);
        SteamShortcuts.Add(Shortcuts(111), "SISR Marker", SisrExe, "--marker");
        _pc.System.Running.Add(new("SISR", new(5, SisrExe, "SISR.exe")));
        _pc.System.Unstoppable.Add(5);

        var installation = _pc.Installation();
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
        Assert.False(_pc.Installation().Remove(steamRunning: true).Complete);

        Assert.True(_pc.Installation().Remove(steamRunning: false).Complete);
        Assert.False(_pc.Installation().Find().ShowsGroup);
        Assert.True(_pc.Installation().Remove(steamRunning: false).Complete);
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public void Removing_stops_SISR_and_its_VIIPER_from_its_folder_only()
    {
        Touch(SisrExe);
        var sisrViiper = Path.Combine(LocalAppData, "SISR", "viiper.exe");
        _pc.System.Running.Add(new("SISR", new(5, SisrExe, null)));
        _pc.System.Running.Add(new("SISR", new(6, Path.Combine(_pc.Root, "Tools", "SISR.exe"), null)));
        _pc.System.Running.Add(new("viiper", new(7, sisrViiper, null)));
        _pc.System.Running.Add(new("viiper", new(8, ArcadiaViiper, null)));

        Assert.True(_pc.Installation().StopForRemoval());

        Assert.Equal([5, 7], _pc.System.Stopped);
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
        _pc.System.Running.Add(new("SISR", new(5, SisrExe, arcadias)));
        _pc.System.Running.Add(new("SISR", new(6, SisrExe, "SISR.exe")));
        _pc.System.Running.Add(new("SISR", new(7, SisrExe, null)));

        Assert.Equal(1, _pc.Installation().StopStartedByOldArcadia());

        Assert.Equal([5], _pc.System.Stopped);
        Assert.True(_pc.Installation().IsAnyRunning);
    }

    private string Shortcuts(uint account) => _pc.Shortcuts(account);

    private static void Touch(string path) => FakeSisrPc.Touch(path);
}
