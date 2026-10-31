using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>When Pan removes the stale device entries (#347): only with nothing of Arcadia's plugged in or left to undo.</summary>
public sealed class GuardianStaleDevicesTests : IDisposable
{
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new();
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _devices = new();
    private readonly FakeStaleDevices _store = FakeStaleDevices.Typical();
    private readonly List<string> _log = [];
    private readonly Guardian _guardian;

    public GuardianStaleDevicesTests()
    {
        _guardian = NewGuardian();
        _cleanup.Children = [new HeldProcess(600, 1500, "viiper.exe")];
        _cleanup.Ports = [3];
    }

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
    };

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private int RemovedCount() { lock (_store.Removed) return _store.Removed.Count; }

    private PanMessage Send(PanCommand command, uint generation = 0) =>
        _guardian.HandleNow(PanMessage.Of(command, generation, command == PanCommand.EmulationStarted ? (uint)FakeCleanup.ServerPort : 0u));

    private void Connect()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, _guardian.HandleNow(PanMessage.Of(PanCommand.Hello)).Reply);
    }

    private uint StartEmulation()
    {
        var started = Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    private async Task CleanupDoneAsync()
    {
        if (_guardian.StaleCleanup is { } cleanup)
        {
            await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    [Trait("TestCase", "T347-2")]
    public async Task Pan_cleans_up_once_after_its_start_and_the_client_connects_meanwhile()
    {
        Assert.True(await _guardian.RecoverAsync());
        using var blocked = new ManualResetEventSlim();
        _store.DuringRemove = _ => blocked.Wait(TimeSpan.FromSeconds(10));

        _guardian.CleanUpStaleDevices("Pan started");
        // Arcadia's connect isn't delayed by it.
        Connect();
        blocked.Set();
        await CleanupDoneAsync();

        Assert.Equal(4, RemovedCount());
        Assert.Contains("Stale device entries (Pan started): 4 found, 4 removed, 0 failed, 0 kept", Log());
    }

    [Fact]
    [Trait("TestCase", "T347-2")]
    public async Task A_planned_stop_cleans_up_after_it()
    {
        Connect();
        var generation = StartEmulation();
        _guardian.CleanUpStaleDevices("test");
        Assert.Null(_guardian.StaleCleanup);

        Assert.Equal(PanReply.Stopped, Send(PanCommand.EmulationStopped, generation).Reply);
        await CleanupDoneAsync();

        Assert.Equal(4, RemovedCount());
        Assert.Contains("Stale device entries (emulation stopped)", Log());
    }

    [Fact]
    [Trait("TestCase", "T347-2")]
    public async Task A_teardown_after_a_hang_cleans_up_once_nothing_is_left_to_undo()
    {
        Connect();
        StartEmulation();
        _clock.Advance(TimeSpan.FromSeconds(4));
        _guardian.Tick();
        await _guardian.Teardown!;
        await CleanupDoneAsync();

        Assert.Equal(4, RemovedCount());
        Assert.Contains("Stale device entries (teardown, Hang)", Log());
    }

    [Fact]
    [Trait("TestCase", "T347-7")]
    public async Task A_teardown_that_leaves_something_to_undo_does_not_clean_up()
    {
        Connect();
        StartEmulation();
        _cleanup.EndFails = true;
        _guardian.ClientGone();
        await _guardian.Teardown!;

        Assert.Null(_guardian.StaleCleanup);
        Assert.Equal(0, RemovedCount());
    }

    [Fact]
    [Trait("TestCase", "T347-5")]
    public async Task A_new_emulation_waits_until_the_removal_running_returned_and_the_cleanup_stops_there()
    {
        using var inRemoval = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _store.DuringRemove = _ =>
        {
            inRemoval.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        };
        Connect();
        _guardian.CleanUpStaleDevices("Pan started");
        Assert.True(inRemoval.Wait(TimeSpan.FromSeconds(10)));

        var waiting = Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Rejected, waiting.Reply);
        Assert.Equal(PanReason.SteamControllerBeingShown, waiting.Reason);

        release.Set();
        await CleanupDoneAsync();
        Assert.Equal(1, RemovedCount());
        Assert.Contains("stopped; the rest goes next time", Log());
        StartEmulation();
    }

    [Fact]
    [Trait("TestCase", "T347-7")]
    public async Task A_stop_ends_a_running_cleanup_between_two_entries_and_starts_none()
    {
        using var inRemoval = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _store.DuringRemove = _ =>
        {
            inRemoval.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        };
        _guardian.CleanUpStaleDevices("Pan started");
        Assert.True(inRemoval.Wait(TimeSpan.FromSeconds(10)));

        var stop = _guardian.StopAsync();
        Assert.False(stop.IsCompleted);
        release.Set();
        await stop.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, RemovedCount());
        _guardian.CleanUpStaleDevices("after stop");
        await CleanupDoneAsync();
        Assert.Equal(1, RemovedCount());
    }

    [Fact]
    [Trait("TestCase", "T347-7")]
    public async Task A_stop_while_emulating_tears_down_without_cleaning_up()
    {
        Connect();
        StartEmulation();

        await _guardian.StopAsync();

        Assert.Null(_guardian.StaleCleanup);
        Assert.Equal(0, RemovedCount());
    }

    [Fact]
    [Trait("TestCase", "T347-7")]
    public async Task No_cleanup_while_a_bootloader_is_connected()
    {
        var guardian = NewGuardian(hiding: true);
        _devices.Bootloader = true;

        guardian.CleanUpStaleDevices("Pan started");
        await guardian.StaleCleanup!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, RemovedCount());
        Assert.Contains("a Valve bootloader is connected", Log());

        _devices.Bootloader = false;
        guardian.CleanUpStaleDevices("later");
        await guardian.StaleCleanup!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(4, RemovedCount());
    }

    [Fact]
    [Trait("TestCase", "T347-4")]
    public async Task A_cleanup_that_fails_never_faults()
    {
        _store.ListFails = true;

        _guardian.CleanUpStaleDevices("Pan started");
        await CleanupDoneAsync();

        Assert.True(_guardian.StaleCleanup!.IsCompletedSuccessfully);
        Assert.Contains("Stale device entries not listed", Log());
    }
}
