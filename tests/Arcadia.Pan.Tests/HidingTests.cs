using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// Part B of #243: Pan hides the real Steam Controller through HidHide and shows it again in every teardown, touching
/// only its own entries, journaled before each change, and only once nothing forwards to the controller any more.
/// </summary>
public sealed class HidingTests : IDisposable
{
    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";
    private const string UsersPad = @"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000";
    private const string UsersApp = @"\Device\HarddiskVolume3\Tools\DS4Windows.exe";
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true, ArcadiaImage);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new();
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _devices = new();
    private readonly List<string> _log = [];
    private readonly List<PanMessage> _notified = [];
    private readonly List<UnplannedEnd> _unplanned = [];
    private Guardian _guardian;

    public HidingTests()
    {
        _guardian = NewGuardian();
        ListenTo(_guardian);
        _cleanup.Children = [new HeldProcess(600, 1500, "viiper.exe")];
        _cleanup.Ports = [3];
    }

    public void Dispose()
    {
        _devices.Dispose();
        _journal.Dispose();
    }

    private Guardian NewGuardian(TimeSpan? deviceStepTimeout = null, TimeSpan? puckReleaseTimeout = null) => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: new SteamControllerHiding(_hidHide, _devices), deviceStepTimeout: deviceStepTimeout ?? FakeDevices.StepTimeout)
    {
        // Most tests have no client that answers (AnswerReleases): they wait this out.
        PuckReleaseTimeout = puckReleaseTimeout ?? TimeSpan.FromMilliseconds(100),
    };

    /// <summary>The client as Arcadia is: on <see cref="PanReply.ReleasePuck"/> it lets go and says so (#308).</summary>
    private void AnswerReleases(Guardian guardian, bool answer = true) => guardian.Notify += message =>
    {
        if (message.Reply is PanReply.ReleasePuck or PanReply.PuckRestarted)
        {
            lock (_devices.Calls) _devices.Calls.Add(message.Reply.ToString());
        }

        if (answer && message.Reply == PanReply.ReleasePuck)
        {
            guardian.HandleAsync(PanMessage.Of(PanCommand.PuckReleased, value: message.Value)).GetAwaiter().GetResult();
        }
    };

    private void ListenTo(Guardian guardian)
    {
        guardian.Notify += message => { lock (_notified) _notified.Add(message); };
        guardian.UnplannedEnd += end => { lock (_unplanned) _unplanned.Add(end); };
    }

    /// <summary>A teardown the client asked for: nobody is told of it, and no crash report is made.</summary>
    private void AssertPlannedTeardown()
    {
        lock (_notified) Assert.DoesNotContain(_notified, notice => notice.Reply == PanReply.TornDown);
        lock (_unplanned) Assert.Empty(_unplanned);
    }

    /// <summary>For a test whose devices hang: the guardian gives up on them soon.</summary>
    private void WithDevicesThatMayHang()
    {
        _guardian = NewGuardian(FakeDevices.HangTimeout);
        ListenTo(_guardian);
    }

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private JournalState Journal => _journal.File.Load().State;

    private Task<PanMessage> Send(PanCommand command, uint generation = 0) => _guardian.HandleAsync(PanMessage.Of(command, generation, command == PanCommand.EmulationStarted ? (uint)FakeCleanup.ServerPort : 0u));

    private async Task<uint> EmulatingAsync(ClientIdentity? client = null)
    {
        Assert.Equal(PanReason.None, _guardian.Register(client ?? Arcadia));
        Assert.Equal(PanReply.HelloOk, (await Send(PanCommand.Hello)).Reply);
        var started = await Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    private async Task<uint> HiddenAsync()
    {
        var generation = await EmulatingAsync();
        var hidden = await Send(PanCommand.Hide, generation);
        Assert.Equal(PanReply.Hidden, hidden.Reply);
        return generation;
    }

    private async Task TeardownDoneAsync()
    {
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }
    }

    /// <summary>HidHide as it was before Pan hid, and nothing left in the journal to undo.</summary>
    private void AssertRestored(List<string> hidden, List<string> allowed, bool cloaking)
    {
        Assert.Equal(hidden, _hidHide.Hidden);
        Assert.Equal(allowed, _hidHide.Allowed);
        Assert.Equal(cloaking, _hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task Hide_hides_the_connected_puck_allow_lists_the_client_turns_cloaking_on_and_restarts_it()
    {
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, @"HID\VID_28DE&PID_1205&MI_02\9&0&0000", UsersPad];
        var generation = await EmulatingAsync();

        var hidden = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReply.Hidden, hidden.Reply);
        Assert.Equal(2u, hidden.Value);
        // Only the listed puck: another Valve device and another pad stay visible even if the device list names them.
        Assert.Equal([FakeDevices.Slot2, FakeDevices.Slot3], _hidHide.Hidden);
        Assert.Equal([ArcadiaImage], _hidHide.Allowed);
        Assert.True(_hidHide.Cloaking);
        Assert.Equal(1, _devices.Restarts);
        // The client is allow-listed before anything is hidden, so it never loses the controller it reads.
        Assert.Equal(["SetAllowed", "SetHidden", "CloakingOn"], _hidHide.Calls.Where(call => call.StartsWith("Set") || call.StartsWith("Cloaking")));
        var journal = Journal;
        Assert.Equal([FakeDevices.Slot2, FakeDevices.Slot3], journal.HiddenDevices);
        Assert.Equal([ArcadiaImage], journal.AllowListed);
        Assert.False(journal.CloakingWasOn);
        Assert.False(journal.PuckRestartPending);
    }

    [Fact]
    public async Task Every_change_is_journaled_before_it_is_made()
    {
        var generation = await EmulatingAsync();
        var seen = new List<string>();
        _hidHide.BeforeChange = change =>
        {
            var journal = Journal;
            if (change == "SetAllowed" && journal.AllowListed.Contains(ArcadiaImage)
                || change == "SetHidden" && journal.HiddenDevices.Count == 2 && journal.PuckRestartPending
                || change == "CloakingOn" && journal.CloakingWasOn == false)
            {
                seen.Add(change);
            }
        };

        await Send(PanCommand.Hide, generation);

        Assert.Equal(["SetAllowed", "SetHidden", "CloakingOn"], seen);
    }

    [Fact]
    public async Task Unhide_removes_only_Pans_entries_and_leaves_the_users_entries_and_cloaking_as_they_were()
    {
        // The user hides a pad of their own and had already listed the puck's slot 3 and Arcadia (e.g. DS4Windows).
        _hidHide.Hidden = [UsersPad, FakeDevices.Slot3];
        _hidHide.Allowed = [UsersApp, ArcadiaImage];
        _hidHide.Cloaking = true;
        var generation = await HiddenAsync();
        Assert.Equal([FakeDevices.Slot2], Journal.HiddenDevices);
        Assert.Empty(Journal.AllowListed);
        Assert.True(Journal.CloakingWasOn);

        var shown = await Send(PanCommand.Unhide, generation);

        Assert.Equal(PanReply.Shown, shown.Reply);
        AssertRestored([UsersPad, FakeDevices.Slot3], [UsersApp, ArcadiaImage], cloaking: true);
        Assert.Equal(2, _devices.Restarts);
        Assert.True(_guardian.IsEmulating);
        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, generation)).Reply);
    }

    [Fact]
    public async Task Cloaking_that_was_off_is_turned_off_again()
    {
        var generation = await HiddenAsync();

        await Send(PanCommand.Unhide, generation);

        AssertRestored([], [], cloaking: false);
    }

    [Fact]
    public async Task A_second_hide_changes_nothing_and_keeps_what_cloaking_was_before_the_first()
    {
        var generation = await HiddenAsync();
        _hidHide.Calls.Clear();

        var again = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReply.Hidden, again.Reply);
        Assert.DoesNotContain(_hidHide.Calls, call => call.StartsWith("Set") || call.StartsWith("Cloaking"));
        Assert.Equal(1, _devices.Restarts);
        Assert.False(Journal.CloakingWasOn);
    }

    public static TheoryData<string, PanReason> RefusedStates => new()
    {
        { "missing", PanReason.HidHideMissing },
        { "driver not running", PanReason.HidHideMissing },
        { "1.1", PanReason.HidHideUnsupported },
        { "2.0", PanReason.HidHideUnsupported },
        { "unknown interface", PanReason.HidHideUnsupported },
        { "inverse", PanReason.HidHideInverseMode },
        { "cloaking off with the user's entries", PanReason.HidHideCloakingOffWithForeignEntries },
        { "busy", PanReason.HidHideBusy },
    };

    [Theory]
    [MemberData(nameof(RefusedStates))]
    public async Task HidHide_in_a_state_Pan_cannot_use_means_nothing_is_hidden_and_the_reason_goes_to_the_client(string state, PanReason reason)
    {
        Arrange(state);
        var generation = await EmulatingAsync();

        var check = await Send(PanCommand.CheckHiding);
        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal((PanReply.HidingState, reason), (check.Reply, check.Reason));
        Assert.Equal((PanReply.Rejected, reason), (hide.Reply, hide.Reason));
        Assert.DoesNotContain(_hidHide.Calls, call => call.StartsWith("Set") || call.StartsWith("Cloaking"));
        Assert.Equal(0, _devices.Restarts);
        Assert.False(Journal.HidingHasWork);
        Assert.True(_guardian.IsEmulating);
    }

    private void Arrange(string state)
    {
        switch (state)
        {
            case "missing": _hidHide.Version = null; break;
            case "driver not running": _hidHide.DriverRunning = false; break;
            case "1.1": _hidHide.Version = new Version(1, 1, 50); break;
            case "2.0": _hidHide.Version = new Version(2, 0); break;
            case "unknown interface": _hidHide.UnknownInterface = true; break;
            case "inverse": _hidHide.Inverse = true; break;
            case "cloaking off with the user's entries": _hidHide.Hidden = [UsersPad]; break;
            case "busy": _hidHide.FailAll = PanReason.HidHideBusy; break;
        }
    }

    public static TheoryData<string> UsableStates => new() { "cloaking on with the user's entries", "cloaking off with only the puck listed", "nothing listed" };

    [Theory]
    [MemberData(nameof(UsableStates))]
    public async Task HidHide_states_Pan_can_hide_in_and_undo_exactly(string state)
    {
        (_hidHide.Hidden, _hidHide.Cloaking) = state switch
        {
            "cloaking on with the user's entries" => ([UsersPad], true),
            "cloaking off with only the puck listed" => ([FakeDevices.Slot2], false),
            _ => (new List<string>(), false),
        };
        var before = (Hidden: _hidHide.Hidden.ToList(), Allowed: _hidHide.Allowed.ToList(), _hidHide.Cloaking);
        var generation = await EmulatingAsync();

        Assert.Equal(PanReason.None, (await Send(PanCommand.CheckHiding)).Reason);
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        Assert.True(_hidHide.Cloaking);
        Assert.Equal(PanReply.Shown, (await Send(PanCommand.Unhide, generation)).Reply);

        AssertRestored(before.Hidden, before.Allowed, before.Cloaking);
    }

    public static TheoryData<string, PanReason> LostHidings => new()
    {
        { "a hidden instance unticked", PanReason.HidingLost },
        { "cloaking turned off", PanReason.HidingLost },
        { "Arcadia taken off the allow list", PanReason.HidingLost },
        { "configuration client open", PanReason.HidHideBusy },
        { "HidHide uninstalled", PanReason.HidHideMissing },
    };

    [Theory]
    [MemberData(nameof(LostHidings))]
    public async Task A_check_with_the_hiding_generation_says_when_its_hiding_no_longer_holds(string change, PanReason reason)
    {
        var generation = await HiddenAsync();
        Assert.Equal(PanReason.None, (await Send(PanCommand.CheckHiding, generation)).Reason);

        switch (change)
        {
            case "a hidden instance unticked": _hidHide.Hidden.Remove(FakeDevices.Slot2); break;
            case "cloaking turned off": _hidHide.Cloaking = false; break;
            case "Arcadia taken off the allow list": _hidHide.Allowed.Remove(ArcadiaImage); break;
            case "configuration client open": _hidHide.FailAll = PanReason.HidHideBusy; break;
            case "HidHide uninstalled": _hidHide.Version = null; break;
        }

        var check = await Send(PanCommand.CheckHiding, generation);

        Assert.Equal((PanReply.HidingState, reason), (check.Reply, check.Reason));
        // The settings' check (no generation) still only asks whether Pan could hide.
        Assert.Equal(change is "configuration client open" or "HidHide uninstalled" ? reason : PanReason.None, (await Send(PanCommand.CheckHiding)).Reason);
    }

    [Fact]
    public async Task A_hiding_the_user_had_set_up_before_counts_as_held_while_it_stays()
    {
        (_hidHide.Hidden, _hidHide.Allowed, _hidHide.Cloaking) = ([FakeDevices.Slot2, FakeDevices.Slot3], [ArcadiaImage], true);
        var generation = await HiddenAsync();

        Assert.Equal(PanReason.None, (await Send(PanCommand.CheckHiding, generation)).Reason);
        _hidHide.Hidden.Remove(FakeDevices.Slot3);
        Assert.Equal(PanReason.HidingLost, (await Send(PanCommand.CheckHiding, generation)).Reason);
    }

    [Fact]
    public async Task A_second_hide_in_the_same_emulation_adds_to_what_the_check_expects_hidden()
    {
        var generation = await HiddenAsync();
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Dongle];
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);

        // The check holds both hides: the instance the second one added, and one only the first one hid.
        _hidHide.Hidden.Remove(FakeDevices.Dongle);
        Assert.Equal(PanReason.HidingLost, (await Send(PanCommand.CheckHiding, generation)).Reason);
        _hidHide.Hidden.Add(FakeDevices.Dongle);
        Assert.Equal(PanReason.None, (await Send(PanCommand.CheckHiding, generation)).Reason);
        _hidHide.Hidden.Remove(FakeDevices.Slot3);
        Assert.Equal(PanReason.HidingLost, (await Send(PanCommand.CheckHiding, generation)).Reason);
    }

    [Fact]
    public async Task After_an_unhide_a_check_of_that_generation_no_longer_expects_hiding()
    {
        var generation = await HiddenAsync();
        Assert.Equal(PanReply.Shown, (await Send(PanCommand.Unhide, generation)).Reply);

        Assert.Equal(PanReason.None, (await Send(PanCommand.CheckHiding, generation)).Reason);
    }

    [Fact]
    public async Task Hiding_belongs_to_the_current_emulation()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReason.NotRegistered, (await Send(PanCommand.Hide)).Reason);
        await Send(PanCommand.Hello);
        Assert.Equal(PanReason.NotEmulating, (await Send(PanCommand.Hide)).Reason);
        Assert.Equal(PanReply.HidingState, (await Send(PanCommand.CheckHiding)).Reply);

        var generation = (await Send(PanCommand.EmulationStarted)).Generation;
        Assert.Equal(PanReason.WrongGeneration, (await Send(PanCommand.Hide, generation + 1)).Reason);
        Assert.Empty(_hidHide.Hidden);
    }

    [Fact]
    public async Task A_client_whose_image_path_is_unknown_is_not_hidden_for()
    {
        var generation = await EmulatingAsync(Arcadia with { ImagePath = null });

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReason.HidingFailed, hide.Reason);
        Assert.Empty(_hidHide.Hidden);
    }

    [Fact]
    public async Task Stopping_the_emulation_while_hidden_tears_it_down_and_a_new_one_waits_for_that()
    {
        var generation = await HiddenAsync();
        _devices.RestartHangs = true;

        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, generation)).Reply);
        Assert.False(_guardian.IsEmulating);
        Assert.Equal(PanReason.SteamControllerBeingShown, (await Send(PanCommand.EmulationStarted)).Reason);
        _devices.EndRestartHang();
        await TeardownDoneAsync();

        Assert.Single(_cleanup.Ended);
        AssertRestored([], [], cloaking: false);
        Assert.False(Journal.HasWork);
        Assert.Contains("stopped by the client with the Steam Controller still hidden", Log());
        AssertPlannedTeardown();
        Assert.Equal(PanReply.Started, (await Send(PanCommand.EmulationStarted)).Reply);
    }

    [Fact]
    public async Task A_planned_exit_while_hidden_tears_it_down()
    {
        var generation = await HiddenAsync();

        Assert.Equal(PanReply.Acknowledged, (await Send(PanCommand.PlannedExit, generation)).Reply);
        await TeardownDoneAsync();
        _guardian.ClientGone();

        Assert.Single(_cleanup.Ended);
        AssertRestored([], [], cloaking: false);
        Assert.False(Journal.HasWork);
        AssertPlannedTeardown();
        Assert.Contains("exits with the Steam Controller still hidden", Log());
    }

    [Fact]
    public async Task A_killed_client_gets_the_puck_shown_and_restarted_only_after_the_forwarding_ended()
    {
        _hidHide.Hidden = [UsersPad];
        _hidHide.Cloaking = true;
        await HiddenAsync();
        _hidHide.BeforeChange = change =>
        {
            // Step 3 waits for steps 1 and 2: the port is detached and viiper.exe ended before the puck is shown.
            Assert.Equal([3], _cleanup.Detached);
            Assert.Single(_cleanup.Ended);
        };

        _guardian.ClientGone();
        await TeardownDoneAsync();

        AssertRestored([UsersPad], [], cloaking: true);
        Assert.Equal(2, _devices.Restarts);
        Assert.False(Journal.HasWork);
        Assert.Contains("Teardown: show the hidden controllers: ok", Log());
        Assert.Contains("Teardown: restart the shown controllers: ok", Log());
    }

    [Fact]
    public async Task A_hang_shows_the_puck_before_the_client_hears_of_the_teardown_and_it_hides_again_under_the_next_generation()
    {
        var generation = await HiddenAsync();

        _clock.Advance(TimeSpan.FromSeconds(3));
        _guardian.Tick();
        await TeardownDoneAsync();

        AssertRestored([], [], cloaking: false);
        // A client whose feeder hangs may still read its pipe: it is asked to let go of the puck first (#308).
        Assert.Equal([PanReply.ReleasePuck, PanReply.PuckRestarted, PanReply.TornDown], _notified.Select(n => n.Reply));
        Assert.Equal(PanReason.Hang, _notified[2].Reason);
        Assert.Equal(PanReason.WrongGeneration, (await Send(PanCommand.Hide, generation - 1)).Reason);
        Assert.Equal(PanReason.TeardownNotAcknowledged, (await Send(PanCommand.Hide, generation)).Reason);

        Assert.Equal(PanReply.Acknowledged, (await Send(PanCommand.TeardownAcknowledged, generation)).Reply);
        var next = (await Send(PanCommand.EmulationStarted)).Generation;
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, next)).Reply);
        Assert.Equal(2, _hidHide.Hidden.Count);
    }

    [Fact]
    public async Task A_forwarding_process_that_will_not_end_keeps_the_puck_hidden_until_a_later_teardown_ends_it()
    {
        await HiddenAsync();
        _cleanup.EndFails = true;

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Equal(2, _hidHide.Hidden.Count);
        Assert.True(Journal.HidingHasWork);
        Assert.Contains("skipped: the forwarding may still run", Log());

        // Pan idles out with the leftovers journaled: its stop tries again.
        _cleanup.EndFails = false;
        await _guardian.StopAsync();

        AssertRestored([], [], cloaking: false);
        Assert.False(Journal.HasWork);
    }

    [Fact]
    public async Task A_HidHide_change_that_fails_in_a_teardown_stays_journaled_and_a_restarted_Pan_undoes_it()
    {
        await HiddenAsync();
        _hidHide.FailOn = "SetHidden";

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Contains("show the hidden controllers: Failed", Log());
        Assert.Equal(2, Journal.HiddenDevices.Count);

        _hidHide.FailOn = null;
        var restarted = NewGuardian();
        Assert.True(await restarted.RecoverAsync());

        AssertRestored([], [], cloaking: false);
    }

    [Fact]
    public async Task After_a_power_loss_the_restarted_Pan_shows_the_puck_from_its_journal()
    {
        await HiddenAsync();
        var restarts = _devices.Restarts;

        // The machine went down with the puck hidden: no teardown ran; Pan starts with the journal it left.
        var booted = NewGuardian();
        Assert.True(await booted.RecoverAsync());

        AssertRestored([], [], cloaking: false);
        Assert.Equal(restarts + 1, _devices.Restarts);
    }

    [Fact]
    public async Task A_restart_that_fails_or_hangs_is_journaled_as_pending_and_retried()
    {
        WithDevicesThatMayHang();
        var generation = await EmulatingAsync();
        _devices.RestartHangs = true;

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReason.HidingFailed, hide.Reason);
        Assert.True(Journal.PuckRestartPending);
        Assert.Contains("not restarted: no answer within", Log());

        _devices.RestartHangs = false;
        _devices.RestartFails = true;
        Assert.Equal(PanReason.HidingFailed, (await Send(PanCommand.Unhide, generation)).Reason);
        Assert.True(Journal.PuckRestartPending);
        Assert.Empty(Journal.HiddenDevices);

        _devices.RestartFails = false;
        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.False(Journal.HasWork);
        Assert.Equal(1, _devices.Restarts);
    }

    [Fact]
    public async Task A_device_list_that_hangs_fails_the_hide_without_changing_HidHide()
    {
        WithDevicesThatMayHang();
        var generation = await EmulatingAsync();
        _devices.ListHangs = true;

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReason.HidingFailed, hide.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task A_hide_that_cannot_be_journaled_changes_nothing()
    {
        var generation = await EmulatingAsync();
        File.Delete(_journal.File.Path);
        Directory.CreateDirectory(_journal.File.Path);

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReason.HidingFailed, hide.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_hidHide.Allowed);
        Assert.False(_hidHide.Cloaking);
    }

    [Fact]
    public async Task A_HidHide_change_that_fails_during_the_hide_is_undone_by_the_teardown()
    {
        var generation = await EmulatingAsync();
        _hidHide.FailOn = "CloakingOn";

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReason.HidingFailed, hide.Reason);
        Assert.Equal(2, _hidHide.Hidden.Count);

        _hidHide.FailOn = null;
        _guardian.ClientGone();
        await TeardownDoneAsync();

        AssertRestored([], [], cloaking: false);
    }

    [Fact]
    public async Task A_puck_that_arrives_while_hiding_is_hidden_too_and_nothing_happens_while_not_hiding()
    {
        _devices.Present = [];
        await _guardian.DevicesArrivedAsync();
        Assert.Empty(_hidHide.Calls);

        var generation = await EmulatingAsync();
        var hidden = await Send(PanCommand.Hide, generation);
        Assert.Equal(0u, hidden.Value);
        Assert.True(_hidHide.Cloaking);
        var restarts = _devices.Restarts;

        _devices.Present = [FakeDevices.Slot2, FakeDevices.Dongle];
        await _guardian.DevicesArrivedAsync();

        Assert.Equal([FakeDevices.Slot2, FakeDevices.Dongle], _hidHide.Hidden);
        Assert.Equal([FakeDevices.Slot2, FakeDevices.Dongle], Journal.HiddenDevices);
        Assert.Equal(restarts + 1, _devices.Restarts);

        // Its own restart announces the puck again: nothing new, no second restart.
        await _guardian.DevicesArrivedAsync();
        Assert.Equal(restarts + 1, _devices.Restarts);
    }

    [Fact]
    public async Task The_hang_watch_waits_while_the_clients_hide_runs()
    {
        WithDevicesThatMayHang();
        var generation = await EmulatingAsync();
        _devices.RestartHangs = true;
        var hide = Send(PanCommand.Hide, generation);

        _clock.Advance(TimeSpan.FromSeconds(5));
        _guardian.Tick();
        await hide;
        _guardian.Tick();

        Assert.True(_guardian.IsEmulating);
        Assert.Null(_guardian.Teardown);
    }

    [Fact]
    public async Task A_journal_that_names_a_device_Pan_never_hides_is_unreadable()
    {
        await HiddenAsync();
        var tampered = Journal;
        tampered.HiddenDevices = [UsersPad];
        _journal.File.Save(tampered);

        var load = _journal.File.Load();

        Assert.Contains("not one Pan hides", load.Problem);
        Assert.True(load.FromPrevious);
    }

    [Fact]
    public async Task Without_access_to_HidHide_hiding_is_unsupported()
    {
        var guardian = new Guardian(_clock, _journal.File, _cleanup, _log.Add);
        guardian.Register(Arcadia);
        await guardian.HandleAsync(PanMessage.Of(PanCommand.Hello));
        var generation = (await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)FakeCleanup.ServerPort))).Generation;

        Assert.Equal(PanReply.Unsupported, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation))).Reply);
        Assert.Equal(PanReply.Unsupported, (await guardian.HandleAsync(PanMessage.Of(PanCommand.CheckHiding))).Reply);
    }

    [Fact]
    public async Task HidHide_uninstalled_meanwhile_leaves_nothing_to_undo()
    {
        await HiddenAsync();
        // Uninstalled with its lists: no service, no control device.
        (_hidHide.DriverRunning, _hidHide.ServiceRegistered, _hidHide.Version) = (false, false, null);

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.False(Journal.HasWork);
        Assert.Contains("HidHide is gone", Log());
    }

    [Fact]
    public async Task A_driver_that_does_not_answer_while_its_service_is_registered_keeps_the_journal()
    {
        await HiddenAsync();
        _hidHide.DriverRunning = false;

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Equal(2, Journal.HiddenDevices.Count);
    }

    [Fact]
    public async Task A_show_that_fails_for_a_passing_reason_is_retried_without_waiting_for_the_next_trigger()
    {
        var guardian = RetryingGuardian();
        var generation = await EmulatingAsync(guardian);
        Assert.Equal(PanReply.Hidden, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation))).Reply);
        _hidHide.FailAll = PanReason.HidHideBusy;

        guardian.ClientGone();
        await guardian.Teardown!;
        Assert.Equal(2, _hidHide.Hidden.Count);

        // The configuration client is closed again.
        _hidHide.FailAll = null;
        await guardian.ShowRetry!;

        AssertRestored([], [], cloaking: false);
        Assert.False(Journal.HasWork);
        Assert.Contains("shown on retry", Log());
    }

    [Fact]
    public async Task A_retry_leaves_the_hiding_of_a_new_emulation_alone()
    {
        var guardian = RetryingGuardian(TimeSpan.FromMilliseconds(300));
        var generation = await EmulatingAsync(guardian);
        await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation));
        _hidHide.FailOn = "SetHidden";
        guardian.ClientGone();
        await guardian.Teardown!;
        _hidHide.FailOn = null;

        // Arcadia is back before the retry and hides again.
        var next = await EmulatingAsync(guardian, Arcadia with { Pid = 501 });
        Assert.Equal(PanReply.Hidden, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, next))).Reply);
        await guardian.ShowRetry!;

        Assert.Equal(2, _hidHide.Hidden.Count);
        Assert.True(_hidHide.Cloaking);
        Assert.True(guardian.IsEmulating);
    }

    [Fact]
    public async Task A_new_emulation_waits_while_a_retry_shows_the_controller()
    {
        var guardian = RetryingGuardian(TimeSpan.FromMilliseconds(300));
        var generation = await EmulatingAsync(guardian);
        await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation));
        _hidHide.FailOn = "SetHidden";
        guardian.ClientGone();
        await guardian.Teardown!;
        _hidHide.FailOn = null;

        // The retry passed its check that nothing emulates and now reads HidHide's list.
        using var retryReads = new ManualResetEventSlim();
        using var retryGoesOn = new ManualResetEventSlim();
        _hidHide.BeforeRead = name =>
        {
            if (name == "GetHidden" && !retryReads.IsSet)
            {
                retryReads.Set();
                retryGoesOn.Wait(TimeSpan.FromSeconds(30));
            }
        };
        Assert.True(retryReads.Wait(TimeSpan.FromSeconds(10)));

        Assert.Equal(PanReason.None, guardian.Register(Arcadia with { Pid = 501 }));
        Assert.Equal(PanReply.HelloOk, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hello))).Reply);
        var early = await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)FakeCleanup.ServerPort));
        Assert.Equal(PanReason.SteamControllerBeingShown, early.Reason);
        Assert.False(guardian.IsEmulating);

        retryGoesOn.Set();
        await guardian.ShowRetry!;
        Assert.Empty(_hidHide.Hidden);
        Assert.False(_hidHide.Cloaking);
        Assert.Equal(PanReply.Started, (await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)FakeCleanup.ServerPort))).Reply);
    }

    [Fact]
    public async Task Pan_reports_a_pending_show_retry_until_it_succeeds()
    {
        var guardian = RetryingGuardian(TimeSpan.FromMilliseconds(300));
        var generation = await EmulatingAsync(guardian);
        await guardian.HandleAsync(PanMessage.Of(PanCommand.Hide, generation));
        _hidHide.FailAll = PanReason.HidHideBusy;
        guardian.ClientGone();
        await guardian.Teardown!;

        Assert.True(guardian.ShowRetryPending);
        _hidHide.FailAll = null;
        await guardian.ShowRetry!;
        Assert.False(guardian.ShowRetryPending);
        Assert.Empty(_hidHide.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task While_a_bootloader_is_connected_Pan_neither_hides_nor_restarts_the_puck()
    {
        var generation = await EmulatingAsync();
        _devices.Bootloader = true;

        var hide = await Send(PanCommand.Hide, generation);

        Assert.Equal(PanReply.Rejected, hide.Reply);
        Assert.Equal(PanReason.BootloaderPresent, hide.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.Equal(0, _devices.Restarts);
        Assert.Contains("A Valve bootloader is connected", Log());
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task A_few_seconds_after_the_bootloader_is_gone_the_hide_goes_through()
    {
        var generation = await EmulatingAsync();
        _devices.Bootloader = true;
        Assert.Equal(PanReason.BootloaderPresent, (await Send(PanCommand.Hide, generation)).Reason);

        // The device comes back as itself and may go on with a second stage: the grace keeps the hide off a little longer.
        _devices.Bootloader = false;
        Assert.Equal(PanReason.BootloaderPresent, (await Send(PanCommand.Hide, generation)).Reason);

        _clock.Advance(_guardian.BootloaderGrace);
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        Assert.Equal(1, _devices.Restarts);
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task A_teardown_while_a_bootloader_is_connected_shows_the_puck_but_leaves_its_restart_pending()
    {
        var generation = await HiddenAsync();
        var restarts = _devices.Restarts;
        _clock.Advance(_guardian.OwnRestartWindow);
        _devices.Bootloader = true;

        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, generation)).Reply);
        await _guardian.Teardown!;

        Assert.Empty(_hidHide.Hidden);
        Assert.Equal(restarts, _devices.Restarts);
        Assert.True(Journal.PuckRestartPending);
        Assert.Contains("not restarted: a firmware update is being flashed", Log());
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task The_bootloader_the_puck_passes_through_after_Pans_own_restart_is_no_flash()
    {
        var generation = await HiddenAsync();
        _devices.Bootloader = true;

        // The hide's restart sent the puck through 1007 for a moment: the show right after still restarts it.
        Assert.Equal(PanReply.Shown, (await Send(PanCommand.Unhide, generation)).Reply);
        Assert.Equal(2, _devices.Restarts);

        // Long after Pan's last restart, the same bootloader is a flash.
        _clock.Advance(_guardian.OwnRestartWindow);
        Assert.Equal(PanReason.BootloaderPresent, (await Send(PanCommand.Hide, generation)).Reason);
    }

    [Fact]
    public async Task A_bootloader_list_that_cannot_be_read_touches_nothing_and_fails_the_hide()
    {
        var generation = await EmulatingAsync();
        _devices.BootloaderFails = true;

        Assert.Equal(PanReason.HidingFailed, (await Send(PanCommand.Hide, generation)).Reason);
        Assert.Equal(0, _devices.Restarts);
        Assert.Empty(_hidHide.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task The_controllers_bootloader_by_cable_is_a_flash_also_right_after_Pans_own_restart()
    {
        var generation = await HiddenAsync();
        _devices.CableBootloader = true;

        Assert.Equal(PanReason.HidingFailed, (await Send(PanCommand.Unhide, generation)).Reason);
        Assert.Equal(1, _devices.Restarts);
        Assert.True(Journal.PuckRestartPending);
    }

    /// <summary>#308: Windows refuses the restart of a puck Arcadia's SDL holds; Pan's own restarts ask it to let go first.</summary>
    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task Pan_asks_the_client_to_let_go_before_it_restarts_the_puck_on_its_own_and_says_when_it_is_over()
    {
        AnswerReleases(_guardian);
        var generation = await HiddenAsync();

        // A new puck instance while hiding (another USB port): hidden, and the puck restarted.
        _devices.Present = [FakeDevices.Slot2, FakeDevices.Slot3, FakeDevices.Dongle];
        await _guardian.DevicesArrivedAsync();
        // The teardown after a stop while hidden shows and restarts it.
        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, generation)).Reply);
        await TeardownDoneAsync();

        // The client's own hide comes first, unannounced: it let go before it asked.
        Assert.Equal(["Restart", "ReleasePuck", "Restart", "PuckRestarted", "ReleasePuck", "Restart", "PuckRestarted"], _devices.Calls);
        var requests = _notified.Where(n => n.Reply == PanReply.ReleasePuck).Select(n => n.Value).ToList();
        Assert.Equal([1u, 2u], requests);
        Assert.Equal(requests, _notified.Where(n => n.Reply == PanReply.PuckRestarted).Select(n => n.Value));
        Assert.DoesNotContain("didn't let go", Log());
    }

    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task The_clients_own_hide_and_show_are_not_announced_as_it_let_go_itself()
    {
        AnswerReleases(_guardian);
        var generation = await HiddenAsync();

        Assert.Equal(PanReply.Shown, (await Send(PanCommand.Unhide, generation)).Reply);

        Assert.Equal(["Restart", "Restart"], _devices.Calls);
    }

    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task A_client_that_doesnt_let_go_holds_Pans_restart_up_only_briefly_and_a_late_answer_changes_nothing()
    {
        AnswerReleases(_guardian, answer: false);
        var generation = await HiddenAsync();
        // An answer to an earlier request (or none at all) doesn't count.
        Assert.Equal(PanReply.Acknowledged, (await Send(PanCommand.PuckReleased, generation)).Reply);

        Assert.Equal(PanReply.Stopped, (await Send(PanCommand.EmulationStopped, generation)).Reply);
        await TeardownDoneAsync();

        Assert.Equal(["Restart", "ReleasePuck", "Restart", "PuckRestarted"], _devices.Calls);
        Assert.Contains("Client didn't let go of the Steam Controller within 0.1 s; restarting it anyway", Log());
        AssertRestored([], [], cloaking: false);
    }

    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task Without_a_client_Pan_restarts_the_puck_without_asking()
    {
        AnswerReleases(_guardian);
        await HiddenAsync();

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Equal(["Restart", "Restart"], _devices.Calls);
    }

    private Guardian RetryingGuardian(TimeSpan? pause = null) => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: new SteamControllerHiding(_hidHide, _devices), deviceStepTimeout: FakeDevices.StepTimeout)
    {
        ShowRetryDelays = [pause ?? TimeSpan.FromMilliseconds(20)],
    };

    private async Task<uint> EmulatingAsync(Guardian guardian, ClientIdentity? client = null)
    {
        Assert.Equal(PanReason.None, guardian.Register(client ?? Arcadia));
        Assert.Equal(PanReply.HelloOk, (await guardian.HandleAsync(PanMessage.Of(PanCommand.Hello))).Reply);
        var started = await guardian.HandleAsync(PanMessage.Of(PanCommand.EmulationStarted, value: (uint)FakeCleanup.ServerPort));
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }
}
