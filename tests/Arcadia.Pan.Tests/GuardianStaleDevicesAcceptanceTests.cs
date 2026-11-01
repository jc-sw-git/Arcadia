using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>Acceptance tests of #347 written from the test plan's text (T347-3, T347-4, T347-7).</summary>
public sealed class GuardianStaleDevicesAcceptanceTests : IDisposable
{
    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true, ArcadiaImage);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new() { Children = [new HeldProcess(600, 1500, "viiper.exe")], Ports = [3] };
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _devices = new();
    private readonly FakeStaleDevices _store = FakeStaleDevices.Typical();
    private readonly List<string> _log = [];

    public void Dispose()
    {
        _devices.Dispose();
        _journal.Dispose();
    }

    private Guardian NewGuardian(bool hiding = false) => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: hiding ? new SteamControllerHiding(_hidHide, _devices) : null, deviceStepTimeout: FakeDevices.StepTimeout)
    {
        StaleDevices = _store,
        BootloaderGrace = TimeSpan.Zero,
        StaleCleanupSettle = TimeSpan.FromMilliseconds(50),
        ShowRetryDelays = [TimeSpan.FromMilliseconds(20)],
    };

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private static async Task CleanupDoneAsync(Guardian guardian)
    {
        if (guardian.StaleCleanup is { } cleanup)
        {
            await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task<uint> EmulatingAsync(Guardian guardian, ClientIdentity client)
    {
        Assert.Equal(PanReason.None, guardian.Register(client));
        Assert.Equal(PanReply.HelloOk, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hello))).Reply);
        var started = await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)FakeCleanup.ServerPort));
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    /// <summary>What one session leaves: a copy with its interface and collection, and an Xbox pad, below usbip-win2.</summary>
    private void SessionLeavesEntries(int session)
    {
        var copy = $@"USB\VID_28DE&PID_1304\2&387D&0&{10 + session}";
        var iface = $@"USB\VID_28DE&PID_1304&MI_02\3&1C5D&{session}&0002";
        _store.Add(copy, present: false, parent: FakeStaleDevices.UsbipHub);
        _store.Add(iface, present: false, parent: copy);
        _store.Add($@"HID\VID_28DE&PID_1304&MI_02&COL01\4&613&{session}&0000", present: false, parent: iface);
        _store.Add($@"HID\VID_045E&PID_028E&IG_00\4&115&{session}&0000", present: false, parent: FakeStaleDevices.UsbipHub);
    }

    private int StaleBelowUsbip() =>
        StaleDeviceList.Select(_store.KnownDevices(), _store.HardwareIdsOf).Count;

    [Fact]
    [Trait("TestCase", "T347-3")]
    public async Task After_each_of_three_sessions_in_a_row_the_entries_of_that_session_are_gone()
    {
        var guardian = NewGuardian();
        var clients = new[] { Arcadia, Arcadia with { Pid = 501 }, Arcadia with { Pid = 502 } };
        for (var session = 0; session < clients.Length; session++)
        {
            var generation = await EmulatingAsync(guardian, clients[session]);
            SessionLeavesEntries(session);

            var stopped = await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStopped, generation));
            Assert.Equal(PanReply.Stopped, stopped.Reply);
            await CleanupDoneAsync(guardian);
            guardian.ClientGone();

            // Never more than one session's entries are left, here none.
            Assert.Equal(0, StaleBelowUsbip());
        }

        Assert.Equal(3, Log().Split('\n').Count(line => line.StartsWith("Stale device entries (emulation stopped)", StringComparison.Ordinal)));
        Assert.Contains(FakeStaleDevices.LiveCopy, _store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.RealPuckEntry, _store.Devices.Keys);
    }

    [Fact]
    [Trait("TestCase", "T347-4")]
    public async Task Arcadia_ended_hard_during_an_emulation_gets_a_teardown_then_a_cleanup_that_logs_no_instance_id()
    {
        var guardian = NewGuardian();
        await EmulatingAsync(guardian, Arcadia);
        _store.Failing.Add(FakeStaleDevices.StalePad);

        guardian.ClientGone();
        await guardian.Teardown!.WaitAsync(TimeSpan.FromSeconds(10));
        await CleanupDoneAsync(guardian);

        var log = Log();
        Assert.True(log.IndexOf("Teardown of generation", StringComparison.Ordinal) < log.IndexOf("Stale device entries (teardown, ClientGone)", StringComparison.Ordinal));
        Assert.Contains("Stale entries not removed: 1 × 045E:028E: CONFIGRET 0x17", log);
        foreach (var id in new[] { FakeStaleDevices.StaleCopy, FakeStaleDevices.StaleInterface, FakeStaleDevices.StaleCollection, FakeStaleDevices.StalePad })
        {
            Assert.DoesNotContain(id, log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(id.Split('\\')[^1], log, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("TestCase", "T347-7")]
    public async Task No_cleanup_while_a_controller_is_still_hidden_and_one_once_it_is_shown()
    {
        var guardian = NewGuardian(hiding: true);
        var generation = await EmulatingAsync(guardian, Arcadia);
        Assert.Equal(PanReply.Hidden, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation))).Reply);
        _hidHide.FailAll = PanReason.HidHideBusy;

        guardian.ClientGone();
        await guardian.Teardown!.WaitAsync(TimeSpan.FromSeconds(10));

        // The puck is still hidden: nothing is removed, also on another trigger (as at Pan's start).
        Assert.Null(guardian.StaleCleanup);
        guardian.CleanUpStaleDevices("Pan started");
        Assert.Null(guardian.StaleCleanup);
        Assert.Empty(_store.Removed);
        Assert.DoesNotContain("Stale device entries (", Log());

        _hidHide.FailAll = null;
        await guardian.ShowRetry!.WaitAsync(TimeSpan.FromSeconds(10));
        await CleanupDoneAsync(guardian);

        Assert.Contains("Stale device entries (controllers shown on retry): 4 found, 4 removed", Log());
    }
}
