using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The QE review of PR #252 (#243 part B1): cases the review found Pan gets wrong. Each is red against the PR's first
/// commit; the fixes make them green.
/// </summary>
public sealed class HidingReviewTests : IDisposable
{
    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";
    private const string UsersPad = @"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000";
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true, ArcadiaImage);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new();
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _devices = new();
    private readonly List<string> _log = [];
    private Guardian _guardian;

    public HidingReviewTests()
    {
        _guardian = NewGuardian(FakeDevices.StepTimeout);
        _cleanup.Children = [new HeldProcess(600, 1500, "viiper.exe")];
        _cleanup.Ports = [3];
    }

    public void Dispose()
    {
        _devices.Dispose();
        _journal.Dispose();
    }

    private Guardian NewGuardian(TimeSpan deviceStepTimeout) => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: new SteamControllerHiding(_hidHide, _devices), deviceStepTimeout: deviceStepTimeout);

    private JournalState Journal => _journal.File.Load().State;

    private Task<PanMessage> Send(PanCommand command, uint generation = 0) => _guardian.HandleAsync(PanMessage.Of(command, generation));

    private async Task<uint> EmulatingAsync()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, (await Send(PanCommand.Hello)).Reply);
        var started = await Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    private async Task TeardownDoneAsync()
    {
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }
    }

    [Fact]
    public async Task A_restart_that_needs_a_replug_does_not_keep_the_client_from_stopping_its_emulation()
    {
        // The puck is shown again, only its restart waits for a replug (DI_NEEDRESTART): nothing is hidden any more.
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        _devices.RestartFails = true;
        await Send(PanCommand.Unhide, generation);
        Assert.Empty(_hidHide.Hidden);
        Assert.False(_hidHide.Cloaking);

        var stopped = await Send(PanCommand.EmulationStopped, generation);

        Assert.Equal(PanReply.Stopped, stopped.Reply);

        // And the next emulation, which never hid anything, stops too.
        var next = (await Send(PanCommand.EmulationStarted)).Generation;
        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, next)).Reply);
        Assert.True(Journal.PuckRestartPending, "the restart is still retried at the next trigger");
    }

    [Fact]
    public async Task A_hiding_check_queued_behind_an_arrival_does_not_count_as_a_hang()
    {
        _guardian = NewGuardian(FakeDevices.HangTimeout);
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        // The puck arrives on another port; its restart takes long (Windows' PnP lock), holding HidHide's gate.
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Dongle];
        _devices.RestartHangs = true;
        var arrival = _guardian.DevicesArrivedAsync();

        // The client asks whether Pan can hide (e.g. its settings row); its heartbeats queue behind the answer on the pipe.
        var check = Send(PanCommand.CheckHiding);
        _clock.Advance(TimeSpan.FromSeconds(5));
        _guardian.Tick();
        await arrival;
        await check;
        _guardian.Tick();

        Assert.Null(_guardian.Teardown);
        Assert.True(_guardian.IsEmulating);
    }

    [Fact]
    public async Task A_device_the_user_hides_while_Pan_lists_the_puck_is_not_lost()
    {
        _hidHide.Cloaking = true;
        var generation = await EmulatingAsync();
        // Listing the devices may wait up to 10 s on Windows' PnP lock; meanwhile the user hides a pad of theirs.
        _devices.WhileListing = () =>
        {
            _hidHide.Hidden = [.. _hidHide.Hidden, UsersPad];
            _devices.WhileListing = null;
        };

        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);

        Assert.Contains(UsersPad, _hidHide.Hidden);
    }

    [Fact]
    public async Task A_HidHide_whose_version_cannot_be_read_but_whose_driver_answers_is_still_restored()
    {
        // E.g. an ImagePath form DriverFile doesn't know, or a HidHide upgrade swapping its driver file: the lists are
        // still there and still hide the puck from Steam.
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        _hidHide.Version = null;

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_hidHide.Allowed);
        Assert.False(_hidHide.Cloaking);
    }
}
