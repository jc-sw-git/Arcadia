using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// #345: Pan hides the Steam Controller by cable (<c>28DE:1302</c>) beside the puck, with the same checks, journal and
/// teardown, and restarts only the cable when it arrives while the puck's copy holds the puck.
/// </summary>
public sealed class CableHidingTests : IDisposable
{
    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true, ArcadiaImage);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new();
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _devices = new();
    private readonly List<string> _log = [];
    private readonly Guardian _guardian;

    public CableHidingTests()
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

    private Guardian NewGuardian() => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: new SteamControllerHiding(_hidHide, _devices), deviceStepTimeout: FakeDevices.StepTimeout)
    {
        PuckReleaseTimeout = TimeSpan.FromMilliseconds(100),
    };

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private JournalState Journal => _journal.File.Load().State;

    private Task<PanMessage> Send(PanCommand command, uint generation = 0) =>
        _guardian.HandleAsync(PanMessage.Of(command, generation, command == PanCommand.EmulationStarted ? (uint)FakeCleanup.ServerPort : 0u));

    private async Task<uint> HiddenAsync()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, (await Send(PanCommand.Hello)).Reply);
        var started = await Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, started.Generation)).Reply);
        return started.Generation;
    }

    [Fact]
    public async Task Hide_hides_the_cable_beside_the_puck_and_restarts_both()
    {
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Cable1, FakeDevices.Cable3];

        await HiddenAsync();

        Assert.Equal([FakeDevices.Slot2, FakeDevices.Cable1, FakeDevices.Cable3], _hidHide.Hidden);
        Assert.Equal([FakeDevices.Slot2, FakeDevices.Cable1, FakeDevices.Cable3], Journal.HiddenDevices);
        Assert.True(_hidHide.Cloaking);
        Assert.Null(Assert.Single(_devices.RestartScopes));
        Assert.False(Journal.PuckRestartPending);
    }

    [Fact]
    public async Task A_cable_plugged_in_while_the_pucks_copy_runs_is_hidden_and_only_the_cable_restarted()
    {
        await HiddenAsync();
        // Arcadia's copy holds the puck now: Windows would refuse to restart it (#308).
        _devices.PuckRestartVetoed = true;
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Cable1, FakeDevices.Cable3];

        await _guardian.DevicesArrivedAsync();

        Assert.Equal([FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Cable1, FakeDevices.Cable3], _hidHide.Hidden);
        Assert.Equal([HidingList.Cable], _devices.RestartScopes[^1]);
        Assert.False(Journal.PuckRestartPending);

        // The cable's restart announces it again: nothing new, no further restart.
        var restarts = _devices.RestartScopes.Count;
        await _guardian.DevicesArrivedAsync();
        Assert.Equal(restarts, _devices.RestartScopes.Count);
        Assert.DoesNotContain("not hidden", Log());
    }

    [Fact]
    public async Task A_puck_restart_still_pending_stays_pending_when_the_cable_arrives()
    {
        _devices.Present = [FakeDevices.Slot2];
        await HiddenAsync();
        _devices.RestartFails = true;
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3];
        await _guardian.DevicesArrivedAsync();
        Assert.True(Journal.PuckRestartPending);

        _devices.RestartFails = false;
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Cable1];
        await _guardian.DevicesArrivedAsync();

        // The puck's restart was never done: the cable's arrival doesn't narrow it to the cable.
        Assert.Equal([HidingList.Puck, HidingList.Cable], _devices.RestartScopes[^1]!.OrderBy(kind => kind.Name == "cable"));
        Assert.False(Journal.PuckRestartPending);
    }

    [Fact]
    public async Task The_teardown_shows_the_cable_too_and_restarts_every_device()
    {
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Cable1, FakeDevices.Cable3];
        await HiddenAsync();

        _guardian.ClientGone();
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }

        Assert.Empty(_hidHide.Hidden);
        Assert.False(_hidHide.Cloaking);
        Assert.Null(_devices.RestartScopes[^1]);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task After_a_power_loss_a_restarted_Pan_shows_the_cable_from_its_journal()
    {
        _devices.Present = [FakeDevices.Cable1, FakeDevices.Cable3];
        await HiddenAsync();

        var booted = NewGuardian();
        Assert.True(await booted.RecoverAsync());

        Assert.Empty(_hidHide.Hidden);
        Assert.False(_hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task No_hide_while_the_cable_controllers_bootloader_is_flashed()
    {
        _devices.Present = [FakeDevices.Cable1];
        _devices.CableBootloader = true;
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        await Send(PanCommand.Hello);
        var started = await Send(PanCommand.EmulationStarted);

        var hide = await Send(PanCommand.Hide, started.Generation);

        Assert.Equal(PanReason.BootloaderPresent, hide.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_devices.RestartScopes);
    }
}
