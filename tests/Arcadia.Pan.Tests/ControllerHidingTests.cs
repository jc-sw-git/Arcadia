using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// #335: Pan hides every controller the client takes over, one model at a time, with the puck's guarantees: journaled
/// first, the user's entries untouched, arrivals hidden, a single model shown alone, every teardown shows them all.
/// </summary>
public sealed class ControllerHidingTests : IDisposable
{
    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";
    private const string UsersEntry = @"HID\VID_046D&PID_C52B&MI_02\7&AA&0&0000";
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true, ArcadiaImage);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new() { Children = [new HeldProcess(600, 1500, "viiper.exe")], Ports = [3] };
    private readonly FakeHidHide _hidHide = new();
    private readonly FakeDevices _puck = new();
    private readonly FakeControllers _controllers = new();
    private readonly List<string> _log = [];
    private readonly HashSet<string> _running = [];
    private Guardian _guardian;

    public ControllerHidingTests() => _guardian = NewGuardian();

    public void Dispose()
    {
        _puck.Dispose();
        _journal.Dispose();
    }

    private Guardian NewGuardian(TimeSpan? deviceStepTimeout = null) => new(
        _clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); },
        hiding: new SteamControllerHiding(_hidHide, _puck, _controllers), deviceStepTimeout: deviceStepTimeout ?? FakeDevices.StepTimeout)
    {
        PuckReleaseTimeout = TimeSpan.FromMilliseconds(100),
        ProcessRuns = name => { lock (_running) return _running.Contains(name); },
    };

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private JournalState Journal => _journal.File.Load().State;

    private Task<PanMessage> Send(PanCommand command, uint generation = 0, uint value = 0) =>
        _guardian.HandleAsync(PanMessage.Of(command, generation, command == PanCommand.EmulationStarted ? (uint)FakeCleanup.ServerPort : value));

    private Task<PanMessage> Hide(ControllerModel model, uint generation) => Send(PanCommand.HideController, generation, model.Value);

    private Task<PanMessage> Show(ControllerModel model, uint generation) => Send(PanCommand.ShowController, generation, model.Value);

    private async Task<uint> EmulatingAsync()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, (await Send(PanCommand.Hello)).Reply);
        var started = await Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    /// <summary>The puck, the DualSense and the 8BitDo hidden in one emulation.</summary>
    private async Task<uint> AllHiddenAsync()
    {
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, generation)).Reply);
        Assert.Equal(PanReply.ControllerHidden, (await Hide(FakeControllers.DualSense, generation)).Reply);
        Assert.Equal(PanReply.ControllerHidden, (await Hide(FakeControllers.EightBitDo, generation)).Reply);
        return generation;
    }

    private async Task TeardownDoneAsync()
    {
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }
    }

    [Fact]
    public async Task A_DualSense_is_hidden_with_the_client_allow_listed_and_its_interface_restarted()
    {
        var generation = await EmulatingAsync();

        var hidden = await Hide(FakeControllers.DualSense, generation);

        Assert.Equal(PanReply.ControllerHidden, hidden.Reply);
        Assert.Equal(PanReason.None, hidden.Reason);
        Assert.Equal([FakeControllers.DualSenseHid], _hidHide.Hidden);
        Assert.Equal([ArcadiaImage], _hidHide.Allowed);
        Assert.True(_hidHide.Cloaking);
        Assert.Equal([FakeControllers.DualSenseHid], _controllers.Restarted);
        Assert.Equal(["SetAllowed", "SetHidden", "CloakingOn"], _hidHide.Calls.Where(call => call.StartsWith("Set") || call.StartsWith("Cloaking")));
        var journal = Journal;
        Assert.Equal([FakeControllers.DualSenseHid], journal.HiddenControllers);
        Assert.Empty(journal.HiddenDevices);
        Assert.Empty(journal.ControllerRestarts);
        Assert.Equal(0, _puck.Restarts);
    }

    [Fact]
    public async Task An_XInput_pad_is_hidden_with_its_XInput_interface_which_only_a_reconnect_restarts()
    {
        var generation = await EmulatingAsync();

        var hidden = await Hide(FakeControllers.EightBitDo, generation);

        // #335 A1: XInput, WGI and SDL read it through the XInput interface; A3: Windows restarts that only on reconnect.
        Assert.Equal(PanReply.ControllerHidden, hidden.Reply);
        Assert.Equal(PanReason.ControllerNeedsReplug, hidden.Reason);
        Assert.Equal([FakeControllers.EightBitDoHid, FakeControllers.EightBitDoXInput], _hidHide.Hidden);
        Assert.Equal([FakeControllers.EightBitDoHid], _controllers.Restarted);

        // Hidden already: a second hide changes nothing and has nothing new to report.
        var again = await Hide(FakeControllers.EightBitDo, generation);
        Assert.Equal(PanReason.None, again.Reason);
        Assert.Equal(2, _hidHide.Hidden.Count);
    }

    [Fact]
    public async Task A_freshly_plugged_XInput_pad_is_reconnected_by_its_port_so_nobody_needs_to_replug_it()
    {
        // #238: Steam opens a freshly plugged XInput pad within a second, before Pan hides it.
        _controllers.Reconnectable.Add(FakeControllers.EightBitDoXInput);
        var generation = await EmulatingAsync();

        var hidden = await Hide(FakeControllers.EightBitDo, generation);

        Assert.Equal(PanReply.ControllerHidden, hidden.Reply);
        Assert.Equal(PanReason.None, hidden.Reason);
        Assert.Equal([FakeControllers.EightBitDoXInput], _controllers.Reconnected);
        Assert.Empty(Journal.ControllerRestarts);

        // Hidden before: not reconnected again.
        await Hide(FakeControllers.EightBitDo, generation);
        Assert.Single(_controllers.Reconnected);
    }

    [Fact]
    public async Task A_reconnect_without_an_answer_leaves_the_pad_hidden_and_waiting_for_the_players_reconnect()
    {
        _controllers.Reconnectable.Add(FakeControllers.EightBitDoXInput);
        _controllers.ReconnectHangs = true;
        _guardian = NewGuardian(TimeSpan.FromSeconds(1));
        var generation = await EmulatingAsync();

        var hidden = await Hide(FakeControllers.EightBitDo, generation);

        Assert.Equal(PanReply.ControllerHidden, hidden.Reply);
        Assert.Equal(PanReason.ControllerNeedsReplug, hidden.Reason);
        Assert.Equal([FakeControllers.EightBitDoHid, FakeControllers.EightBitDoXInput], _hidHide.Hidden);
    }

    [Fact]
    public async Task A_restart_that_needs_a_replug_is_reported_and_not_tried_again()
    {
        _controllers.NeedReplug.Add(FakeControllers.DualSenseHid);
        var generation = await EmulatingAsync();

        var hidden = await Hide(FakeControllers.DualSense, generation);

        Assert.Equal(PanReason.ControllerNeedsReplug, hidden.Reason);
        Assert.Empty(Journal.ControllerRestarts);
    }

    [Fact]
    [Trait("TestCase", "T335-4")]
    public async Task One_model_shown_again_leaves_the_others_the_puck_and_the_allow_list_hidden()
    {
        var generation = await AllHiddenAsync();

        var shown = await Show(FakeControllers.DualSense, generation);

        Assert.Equal(PanReply.ControllerShown, shown.Reply);
        Assert.Equal(PanReason.None, shown.Reason);
        Assert.Equal([FakeDevices.Slot2, FakeDevices.Slot3, FakeControllers.EightBitDoHid, FakeControllers.EightBitDoXInput], _hidHide.Hidden);
        Assert.Equal([ArcadiaImage], _hidHide.Allowed);
        Assert.True(_hidHide.Cloaking);
        Assert.Equal([FakeControllers.EightBitDoHid, FakeControllers.EightBitDoXInput], Journal.HiddenControllers);
        // Restarted again, so programs that missed it see it come back.
        Assert.Equal(2, _controllers.Restarted.Count(id => id == FakeControllers.DualSenseHid));

        // Its arrival is left alone from now on.
        _controllers.Connected[FakeControllers.DualSense] = new(true, [new(FakeControllers.DualSenseBluetooth, ControllerInterface.Hid)]);
        await _guardian.ControllerArrivedAsync(FakeControllers.DualSense);
        Assert.DoesNotContain(FakeControllers.DualSenseBluetooth, _hidHide.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T335-4")]
    public async Task The_last_model_shown_takes_the_allow_list_entry_and_cloaking_back()
    {
        var generation = await EmulatingAsync();
        await Hide(FakeControllers.EightBitDo, generation);

        var shown = await Show(FakeControllers.EightBitDo, generation);

        // Running programs got the XInput interface back without a restart: they see it once it is reconnected.
        Assert.Equal(PanReason.ControllerNeedsReplug, shown.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_hidHide.Allowed);
        Assert.False(_hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    [Trait("TestCase", "T335-4")]
    public async Task Showing_the_Steam_Controller_leaves_the_controllers_and_the_allow_list_entry()
    {
        var generation = await AllHiddenAsync();

        Assert.Equal(PanReply.Shown, (await Send(PanCommand.Unhide, generation)).Reply);

        Assert.Equal(3, _hidHide.Hidden.Count);
        Assert.DoesNotContain(FakeDevices.Slot2, _hidHide.Hidden);
        Assert.Equal([ArcadiaImage], _hidHide.Allowed);
        Assert.True(_hidHide.Cloaking);
    }

    [Fact]
    public async Task Entries_the_user_had_stay_through_hide_show_and_teardown()
    {
        _hidHide.Hidden = [UsersEntry, FakeControllers.DualSenseHid];
        _hidHide.Allowed = [ArcadiaImage];
        _hidHide.Cloaking = true;
        var generation = await EmulatingAsync();

        await Hide(FakeControllers.DualSense, generation);
        await Hide(FakeControllers.EightBitDo, generation);
        Assert.Equal([FakeControllers.EightBitDoHid, FakeControllers.EightBitDoXInput], Journal.HiddenControllers);
        await Show(FakeControllers.DualSense, generation);
        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Equal([UsersEntry, FakeControllers.DualSenseHid], _hidHide.Hidden);
        Assert.Equal([ArcadiaImage], _hidHide.Allowed);
        Assert.True(_hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task A_controller_plugged_in_while_taken_over_is_hidden_at_its_arrival_without_a_restart()
    {
        var generation = await EmulatingAsync();
        await Hide(FakeControllers.DualSense, generation);
        _controllers.Restarted.Clear();
        _controllers.Connected[FakeControllers.DualSense] = new(true,
            [new(FakeControllers.DualSenseHid, ControllerInterface.Hid), new(FakeControllers.DualSenseBluetooth, ControllerInterface.Hid)]);

        await _guardian.ControllerArrivedAsync(FakeControllers.DualSense);

        Assert.Equal([FakeControllers.DualSenseHid, FakeControllers.DualSenseBluetooth], _hidHide.Hidden);
        Assert.Equal([FakeControllers.DualSenseHid, FakeControllers.DualSenseBluetooth], Journal.HiddenControllers);
        Assert.Empty(_controllers.Restarted);
        Assert.Empty(Journal.ControllerRestarts);
    }

    [Fact]
    public async Task An_arrival_of_a_model_the_client_did_not_take_over_is_left_alone()
    {
        var generation = await EmulatingAsync();
        await Hide(FakeControllers.DualSense, generation);

        await _guardian.ControllerArrivedAsync(FakeControllers.EightBitDo);

        Assert.Equal([FakeControllers.DualSenseHid], _hidHide.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T335-5")]
    public async Task Nothing_of_a_vendor_is_hidden_while_its_firmware_tool_runs_and_the_reason_reaches_the_client()
    {
        var generation = await EmulatingAsync();
        lock (_running) _running.Add("XboxDevices");
        var xbox = new ControllerModel(ControllerList.Microsoft, 0x0B12);
        _controllers.Connected[xbox] = new(true, [new(@"USB\VID_045E&PID_0B12&MI_00\7&1&0&0000", ControllerInterface.XInput)]);

        var refused = await Hide(xbox, generation);

        Assert.Equal(PanReply.Rejected, refused.Reply);
        Assert.Equal(PanReason.FirmwareToolRuns, refused.Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.Contains("Xbox Accessories runs", Log());
        // Another vendor's controller is hidden meanwhile.
        Assert.Equal(PanReply.ControllerHidden, (await Hide(FakeControllers.DualSense, generation)).Reply);

        lock (_running) _running.Clear();
        Assert.Equal(PanReply.ControllerHidden, (await Hide(xbox, generation)).Reply);
    }

    [Fact]
    [Trait("TestCase", "T335-5")]
    public async Task An_arrival_while_the_vendors_firmware_tool_runs_is_not_hidden()
    {
        var generation = await EmulatingAsync();
        await Hide(FakeControllers.DualSense, generation);
        lock (_running) _running.Add("FWupdater");
        _controllers.Connected[FakeControllers.DualSense] = new(true, [new(FakeControllers.DualSenseBluetooth, ControllerInterface.Hid)]);

        await _guardian.ControllerArrivedAsync(FakeControllers.DualSense);

        Assert.DoesNotContain(FakeControllers.DualSenseBluetooth, _hidHide.Hidden);
        Assert.Contains("DualSense firmware updater runs: not hidden", Log());
    }

    [Fact]
    public async Task A_model_that_is_not_connected_cannot_be_hidden_or_is_Valves_is_refused_with_its_reason()
    {
        var generation = await EmulatingAsync();
        var wheel = new ControllerModel(0x046D, 0xC266);
        _controllers.Connected[wheel] = new(true, []);

        Assert.Equal(PanReason.ControllerNotConnected, (await Hide(new ControllerModel(0x0F0D, 0x00C1), generation)).Reason);
        Assert.Equal(PanReason.ControllerNotHideable, (await Hide(wheel, generation)).Reason);
        Assert.Equal(PanReason.ControllerNotHideable, (await Hide(new ControllerModel(ControllerList.Valve, 0x1304), generation)).Reason);
        Assert.Empty(_hidHide.Hidden);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task Devices_the_scan_names_that_are_not_the_models_own_are_never_hidden()
    {
        var generation = await EmulatingAsync();
        _controllers.Connected[FakeControllers.DualSense] = new(true,
        [
            new(FakeControllers.DualSenseHid, ControllerInterface.Hid),
            new(FakeDevices.Slot2, ControllerInterface.Hid),
            new(FakeControllers.EightBitDoHid, ControllerInterface.Hid),
            new(@"USB\VID_054C&PID_0CE6\5&1&0&1", ControllerInterface.Hid),
        ]);

        await Hide(FakeControllers.DualSense, generation);

        Assert.Equal([FakeControllers.DualSenseHid], _hidHide.Hidden);
    }

    [Fact]
    public async Task A_restart_that_fails_is_rejected_stays_pending_and_the_teardown_shows_and_restarts_it()
    {
        var generation = await EmulatingAsync();
        _controllers.RestartFails = true;

        var hidden = await Hide(FakeControllers.DualSense, generation);

        Assert.Equal(PanReply.Rejected, hidden.Reply);
        Assert.Equal(PanReason.HidingFailed, hidden.Reason);
        Assert.Equal([FakeControllers.DualSenseHid], Journal.ControllerRestarts);

        _controllers.RestartFails = false;
        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Empty(_hidHide.Hidden);
        Assert.False(Journal.HidingHasWork);
        Assert.Contains(FakeControllers.DualSenseHid, _controllers.Restarted);
    }

    [Fact]
    public async Task A_killed_client_gets_every_controller_and_the_puck_shown_and_restarted()
    {
        await AllHiddenAsync();
        _controllers.Restarted.Clear();

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_hidHide.Allowed);
        Assert.False(_hidHide.Cloaking);
        Assert.Equal([FakeControllers.DualSenseHid, FakeControllers.EightBitDoHid], _controllers.Restarted);
        Assert.False(Journal.HasWork);
        Assert.Contains("Teardown: show the hidden controllers: ok", Log());
        Assert.Contains("Teardown: restart the shown controllers: ok", Log());
    }

    [Fact]
    public async Task A_running_controller_hide_holds_the_hang_watch_and_releases_it_when_done()
    {
        // Its heartbeats wait behind the hide on the pipe: silence then is no hang, but after the hide it is again.
        var generation = await EmulatingAsync();
        using var block = new ManualResetEventSlim();
        _controllers.ScanBlock = block;
        var hide = Hide(FakeControllers.DualSense, generation);
        await _controllers.ScanEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _clock.Advance(TimeSpan.FromSeconds(4));

        _guardian.Tick();
        Assert.Null(_guardian.Teardown);

        block.Set();
        Assert.Equal(PanReply.ControllerHidden, (await hide).Reply);
        // The time the hide took is no silence of the client's: the watch counts from its end.
        _clock.Advance(TimeSpan.FromSeconds(2.9));
        _guardian.Tick();
        Assert.Null(_guardian.Teardown);
        _clock.Advance(TimeSpan.FromMilliseconds(200));
        _guardian.Tick();
        Assert.NotNull(_guardian.Teardown);
        Assert.Contains("hang", Log());
        await TeardownDoneAsync();
    }

    [Fact]
    public async Task A_show_whose_restart_fails_is_refused_and_left_to_the_teardown()
    {
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.ControllerHidden, (await Hide(FakeControllers.DualSense, generation)).Reply);
        _controllers.RestartFails = true;

        var shown = await Show(FakeControllers.DualSense, generation);

        // Programs that had it open would not see it come back: the client hears that, and the teardown tries again.
        Assert.Equal((PanReply.Rejected, PanReason.HidingFailed), (shown.Reply, shown.Reason));
        Assert.Contains("its restart failed", Log());
        Assert.True(Journal.HidingHasWork);
        _controllers.RestartFails = false;
        _controllers.Restarted.Clear();
        _guardian.ClientGone();
        await TeardownDoneAsync();
        Assert.Contains(FakeControllers.DualSenseHid, _controllers.Restarted);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task A_hide_still_scanning_when_the_service_stops_hides_nothing_afterwards()
    {
        // QE #337: the stop's teardown found no hiding work (the hide still scanned, nothing journaled yet) and showed
        // nothing; the hide must not journal and hide the pad afterwards, or it stays hidden while Pan is off.
        var generation = await EmulatingAsync();
        using var block = new ManualResetEventSlim();
        _controllers.ScanBlock = block;
        var hide = Hide(FakeControllers.DualSense, generation);
        await _controllers.ScanEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _guardian.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

        block.Set();
        var answer = await hide;

        Assert.NotEqual(PanReply.ControllerHidden, answer.Reply);
        Assert.Empty(_hidHide.Hidden);
        Assert.False(_hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task A_new_emulation_takes_over_nothing_until_the_client_asks_again()
    {
        var generation = await AllHiddenAsync();
        _guardian.ClientGone();
        await TeardownDoneAsync();
        _guardian = NewGuardian();
        Assert.True(await _guardian.RecoverAsync());
        var next = await EmulatingAsync();
        Assert.NotEqual(generation, next);
        Assert.Equal(PanReply.Hidden, (await Send(PanCommand.Hide, next)).Reply);

        await _guardian.ControllerArrivedAsync(FakeControllers.DualSense);

        Assert.Equal([FakeDevices.Slot2, FakeDevices.Slot3], _hidHide.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T295-3")]
    public async Task A_puck_arrival_while_only_other_controllers_are_hidden_leaves_the_puck_to_Steam()
    {
        // E.g. the puck back after its own flash, or after "Controller freigeben" while a DualSense stays taken over (#295).
        var generation = await EmulatingAsync();
        Assert.Equal(PanReply.ControllerHidden, (await Send(PanCommand.HideController, generation, FakeControllers.DualSense.Value)).Reply);

        await _guardian.DevicesArrivedAsync();

        Assert.DoesNotContain(FakeDevices.Slot2, _hidHide.Hidden);
        Assert.Equal(0, _puck.Restarts);
    }

    [Fact]
    public async Task After_a_power_loss_the_restarted_Pan_shows_every_journaled_controller()
    {
        await AllHiddenAsync();

        var restarted = NewGuardian();
        Assert.True(await restarted.RecoverAsync());

        Assert.Empty(_hidHide.Hidden);
        Assert.Empty(_hidHide.Allowed);
        Assert.False(_hidHide.Cloaking);
        Assert.False(Journal.HidingHasWork);
    }

    [Fact]
    public async Task Hiding_a_controller_belongs_to_the_current_emulation()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        await Send(PanCommand.Hello);

        Assert.Equal(PanReason.NotEmulating, (await Hide(FakeControllers.DualSense, 0)).Reason);
        var generation = (await Send(PanCommand.EmulationStarted)).Generation;
        Assert.Equal(PanReason.WrongGeneration, (await Hide(FakeControllers.DualSense, generation + 1)).Reason);
        Assert.Empty(_hidHide.Hidden);
    }

    [Fact]
    public async Task A_Pan_without_access_to_the_controllers_answers_unsupported()
    {
        _guardian = new Guardian(_clock, _journal.File, _cleanup, _ => { }, hiding: new SteamControllerHiding(_hidHide, _puck), deviceStepTimeout: FakeDevices.StepTimeout);
        var generation = await EmulatingAsync();

        Assert.Equal(PanReply.Unsupported, (await Hide(FakeControllers.DualSense, generation)).Reply);
    }
}

public sealed class HubPortTests
{
    private const string RootHub = @"USB\ROOT_HUB30\4&C7355C0&0&0";

    // The chains as Windows lists them on the owner's PC (8BitDo's dongle) and for a DualSense by USB and by Bluetooth.
    private static readonly Dictionary<string, string> Parents = new(StringComparer.OrdinalIgnoreCase)
    {
        [@"USB\VID_2DC8&PID_310B&MI_00\6&1F8984A7&1&0000"] = @"USB\VID_2DC8&PID_310B\F5FF9E2496",
        [@"USB\VID_2DC8&PID_310B\F5FF9E2496"] = RootHub,
        [@"USB\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0003"] = @"USB\VID_054C&PID_0CE6\5&1&0&2",
        [@"USB\VID_054C&PID_0CE6\5&1&0&2"] = @"USB\VID_05E3&PID_0610\6&2",
        [@"USB\VID_05E3&PID_0610\6&2"] = RootHub,
        [@"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&0ce6\9&1"] = @"BTH\MS_BTHPAN\7&1",
        [@"BTH\MS_BTHPAN\7&1"] = @"USB\VID_8087&PID_0029\5&1",
        [@"USB\VID_8087&PID_0029\5&1"] = RootHub,
    };

    private static readonly HashSet<string> Hubs = new(StringComparer.OrdinalIgnoreCase) { RootHub, @"USB\VID_05E3&PID_0610\6&2" };

    private static string? OnHubPort(string device) => ControllerList.OnHubPort(device, id => Parents.GetValueOrDefault(id), Hubs.Contains);

    [Fact]
    public void An_XInput_interface_is_cycled_at_the_port_of_its_USB_device() =>
        Assert.Equal(@"USB\VID_2DC8&PID_310B\F5FF9E2496", OnHubPort(@"USB\VID_2DC8&PID_310B&MI_00\6&1F8984A7&1&0000"));

    [Fact]
    public void Behind_a_hub_the_port_is_the_nearest_hubs() =>
        Assert.Equal(@"USB\VID_054C&PID_0CE6\5&1&0&2", OnHubPort(@"USB\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0003"));

    [Fact]
    public void A_Bluetooth_controller_never_cycles_the_radios_port() =>
        Assert.Null(OnHubPort(@"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&0ce6\9&1"));

    [Fact]
    public void A_device_without_a_hub_above_it_or_without_a_model_is_not_cycled()
    {
        Assert.Null(OnHubPort(@"USB\VID_045E&PID_028E\unplugged"));
        Assert.Null(OnHubPort(RootHub));
    }
}

public sealed class ControllerModelTests
{
    [Theory]
    [InlineData(@"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000", 0x054C, 0x0CE6)]
    [InlineData(@"USB\VID_2DC8&PID_310B&MI_00\7&5E6F&0&0000", 0x2DC8, 0x310B)]
    [InlineData(@"HID\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&0ce6\9&1D2E&0&0000", 0x054C, 0x0CE6)]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02045e_PID&0b13_REV&0509\9&1&0&0000", 0x045E, 0x0B13)]
    [InlineData(@"\\?\HID#VID_057E&PID_2069&MI_00#8&1&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x057E, 0x2069)]
    public void The_model_is_read_from_USB_Bluetooth_and_Bluetooth_LE_ids_and_from_interface_links(string id, int vendor, int product) =>
        Assert.Equal(new ControllerModel((ushort)vendor, (ushort)product), ControllerModel.Of(id));

    [Theory]
    [InlineData(@"HID\ConvertedDevice&Col01\5&1&0&0000")]
    [InlineData(@"ROOT\SYSTEM\0001")]
    public void An_id_without_USB_ids_names_no_model(string id) => Assert.Null(ControllerModel.Of(id));

    [Fact]
    public void A_model_travels_in_a_messages_value()
    {
        var model = new ControllerModel(0x054C, 0x0CE6);

        Assert.Equal(model, ControllerModel.From(model.Value));
        Assert.Equal("HideController(gen 3, 054C:0CE6)", PanMessage.Of(PanCommand.HideController, 3, model.Value).ToString());
    }

    [Theory]
    [InlineData(@"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000", true)]
    [InlineData(@"USB\VID_2DC8&PID_310B&MI_00\7&5E6F&0&0000", true)]
    [InlineData(@"HID\VID_28DE&PID_1304&MI_02\8&1&0&0002", false)]
    [InlineData(@"HID\VID_0000&PID_0000\1", false)]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&0ce6\9", false)]
    [InlineData(@"HID\ConvertedDevice&Col01\5", false)]
    public void Pan_journals_only_controller_instances_it_may_take_over(string id, bool journaled) =>
        Assert.Equal(journaled, ControllerList.IsControllerInstance(id));

    [Fact]
    public void Each_firmware_tool_holds_off_only_its_own_vendor()
    {
        Assert.Equal("Xbox Accessories", ControllerList.RunningToolFor(new(ControllerList.Microsoft, 0x0B12), name => name == "XboxDevices")?.Name);
        Assert.Equal("DualSense firmware updater", ControllerList.RunningToolFor(new(ControllerList.Sony, 0x0CE6), name => name == "FWupdater")?.Name);
        Assert.Null(ControllerList.RunningToolFor(new(ControllerList.Sony, 0x0CE6), name => name == "XboxDevices"));
        Assert.Null(ControllerList.RunningToolFor(new(0x2DC8, 0x310B), _ => true));
    }
}
