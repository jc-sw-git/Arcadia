using System.Diagnostics;
using System.Runtime.Versioning;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The real HidHide driver: only where <c>ARCADIA_HIDHIDE_TESTS=1</c> says HidHide may be changed (the Windows CI
/// runner after <c>tools/ci/Install-HidHide.ps1</c>), never on a developer's PC by accident. Every test puts HidHide
/// back as it found it.
/// </summary>
public sealed class HidHideFactAttribute : FactAttribute
{
    public HidHideFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "HidHide is a Windows driver";
        }
        else if (!HidHideTests.Enabled)
        {
            Skip = "changes the real HidHide; set ARCADIA_HIDHIDE_TESTS=1 (the CI does after installing it)";
        }
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only";
        }
    }
}

/// <summary>
/// Tests that change this machine (the Pan service, the real HidHide) run alone: they share that state, and while they
/// wait on it they block threads the timing-sensitive tests running beside them would need.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MachineStateCollection
{
    public const string Name = "Machine state";
}

[SupportedOSPlatform("windows")]
[Collection(MachineStateCollection.Name)]
public sealed class HidHideTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("ARCADIA_HIDHIDE_TESTS") == "1";

    /// <summary>HidHide's whole state, comparable.</summary>
    public static string Snapshot()
    {
        var driver = new HidHideDriver();
        return $"hidden: {string.Join(" | ", driver.GetHiddenDevices())}; allowed: {string.Join(" | ", driver.GetAllowedApplications())}; cloaking: {driver.GetCloaking()}; inverse: {driver.GetInverse()}";
    }

    [HidHideFact]
    public void The_driver_interface_reads_and_writes_HidHides_lists_and_switches()
    {
        var driver = new HidHideDriver();
        Assert.True(driver.InstalledVersion() >= SteamControllerHiding.MinimumVersion, $"HidHide {driver.InstalledVersion()} found");
        Assert.False(driver.GetInverse());
        var before = Snapshot();
        var hidden = driver.GetHiddenDevices();
        var allowed = driver.GetAllowedApplications();
        var cloaking = driver.GetCloaking();
        const string device = @"HID\VID_28DE&PID_1304&MI_02&COL03\ARCADIA&TEST&0&0002";
        var self = @"\Device\HarddiskVolume1\Arcadia\test host.exe";

        try
        {
            driver.SetHiddenDevices([.. hidden, device]);
            driver.SetAllowedApplications([.. allowed, self]);
            driver.SetCloaking(!cloaking);

            Assert.Contains(device, driver.GetHiddenDevices());
            Assert.Contains(self, driver.GetAllowedApplications());
            Assert.Equal(!cloaking, driver.GetCloaking());
        }
        finally
        {
            driver.SetHiddenDevices(hidden);
            driver.SetAllowedApplications(allowed);
            driver.SetCloaking(cloaking);
        }

        Assert.Equal(before, Snapshot());
    }

    [HidHideFact]
    public async Task The_guardian_hides_through_the_real_driver_and_its_teardown_restores_HidHide()
    {
        using var journal = new TempJournal();
        var before = Snapshot();
        var cleanup = new FakeCleanup();
        var guardian = new Guardian(new FakeClock(), journal.File, cleanup, _ => { }, hiding: new SteamControllerHiding(new HidHideDriver(), new FakeDevices()));
        guardian.Register(new ClientIdentity(Environment.ProcessId, 1, InConsoleSession: true, NativeImagePath()));
        await guardian.HandleAsync(PanMessage.Of(PanCommand.Hello));
        var generation = (await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted))).Generation;

        var hidden = await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation));

        Assert.Equal(PanReply.Hidden, hidden.Reply);
        Assert.Contains(FakeDevices.Slot2, new HidHideDriver().GetHiddenDevices());
        Assert.Contains(NativeImagePath(), new HidHideDriver().GetAllowedApplications());

        guardian.ClientGone();
        await guardian.Teardown!;

        Assert.Equal(before, Snapshot());
    }

    [WindowsFact]
    public void The_device_list_answers_and_names_only_listed_devices()
    {
        // The runner has no puck; a developer's PC may. Either way the plug and play manager answers.
        Assert.All(new PuckDevices().PresentHidInstances(), id => Assert.True(HidingList.IsListedHid(id), id));
    }

    [WindowsFact]
    public void Windows_takes_the_arrival_registration()
    {
        using var watch = DeviceArrivalWatch.Start(() => Task.CompletedTask, line => Assert.Fail(line));

        Assert.NotNull(watch);
    }

    private static string NativeImagePath()
    {
        // The form HidHide's allow list holds (\Device\HarddiskVolumeN\…), as Pan reads it from the client's process.
        var path = Process.GetCurrentProcess().MainModule!.FileName;
        var drive = path[..2];
        var device = QueryDosDevice(drive);
        return device + path[2..];
    }

    private static string QueryDosDevice(string drive)
    {
        var buffer = new char[1024];
        var length = QueryDosDeviceW(drive, buffer, buffer.Length);
        Assert.True(length > 0, $"QueryDosDevice({drive})");
        return new string(buffer, 0, Array.IndexOf(buffer, '\0'));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int QueryDosDeviceW(string deviceName, char[] targetPath, int max);
}
