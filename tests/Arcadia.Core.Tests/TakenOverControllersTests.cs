using System.Diagnostics;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Input;
using Arcadia.Pan;
using Arcadia.Tests.Pads;
using Arcadia.Tests.Sdl;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// Every controller but the Steam Controller, for games (#238): Pan hides each model within its emulation, then each
/// controller gets its own Xbox 360 pad in connection order, without Arcadia's combos, at rest while Arcadia is in front;
/// never emulated while it isn't hidden (D7), and "Nein" to the Steam Controller's takeover leaves the others alone (D2).
/// </summary>
public sealed class TakenOverControllersTests
{
    private static readonly GamepadReading DualSense = Reading(1, KnownPads.DualSense with { Path = "hid-dualsense" });
    private static readonly GamepadReading EightBitDo = Reading(2, KnownPads.EightBitDoUltimate2);
    private static readonly GamepadReading SteamController = Reading(3, KnownPads.SteamController) with { Type = PadType.Steam };

    private static readonly ControllerModel DualSenseModel = new(0x054C, 0x0CE6);
    private static readonly ControllerModel EightBitDoModel = new(0x2DC8, 0x310B);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private readonly List<string> _log = [];
    private readonly List<string> _rumbles = [];
    private readonly List<string> _lights = [];
    private readonly List<bool> _motion = [];
    private readonly List<bool> _reads = [];
    private readonly List<bool> _fast = [];
    private readonly FakeVirtualXboxPads _pads = new();
    private readonly FakeEmulationGuardian _pan = new();
    private readonly FakeSteamControllerHold _copy = new();
    private readonly TakenOverControllers _others;
    private readonly PanEmulation _emulation;
    private bool _steamControllerPresent;
    private bool _forwarding = true;
    private string? _runningTool;
    private readonly Dictionary<string, string> _layouts = [];

    public TakenOverControllersTests()
    {
        _pads.Events = _events;
        _pan.Events = _events;
        _copy.Events = _events;
        _others = new TakenOverControllers(_time, Log)
        {
            ForwardingAllowed = () => _forwarding,
            ReadControllers = on =>
            {
                lock (_events) _events.Add(on ? "read controllers" : "let go of controllers");
                _reads.Add(on);
            },
            Rumble = (padId, motors) => { lock (_rumbles) _rumbles.Add($"{padId}: {motors.Large}/{motors.Small}"); },
            Light = (padId, light) => { lock (_lights) _lights.Add($"{padId}: {light.Red},{light.Green},{light.Blue}"); },
            MotionSensors = _motion.Add,
            ProcessRuns = name => name == _runningTool,
            LayoutOf = model => ControllerLayouts.Find(_layouts, model),
            Forwarding = _fast.Add,
        };
        _emulation = new PanEmulation(_pads, _pan, _copy, _time, Log)
        {
            SteamControllerPresent = () => _steamControllerPresent,
            Others = _others,
        };
    }

    private void Log(string line)
    {
        lock (_log) _log.Add(line);
    }

    private static GamepadReading Reading(int padId, PadDevice device) =>
        new GamepadReading(padId, PadButtons.None, default, default) with { Device = device, Type = device.Type };

    /// <summary>#370: a throwing setting falls back to its safe value, is reported once, and the reads go on.</summary>
    [Fact]
    public void A_throwing_setting_is_reported_once_and_Feed_goes_on_with_its_safe_value()
    {
        var reported = new List<string>();
        var others = new TakenOverControllers(_time, Log)
        {
            ForwardingAllowed = () => throw new InvalidOperationException("forwarding broke"),
            Combos = () => throw new InvalidOperationException("combos broke"),
            LayoutOf = _ => throw new InvalidOperationException("layout broke"),
            Unexpected = (error, context) => reported.Add($"{context}: {error.Message}"),
        };

        others.Feed([DualSense]);
        others.Feed([DualSense]);

        Assert.Equal(DualSenseModel, Assert.Single(others.Connected).Model);
        Assert.Equal(
            ["Controller für Spiele: forwarding broke", "Controller für Spiele: combos broke", "Controller für Spiele: layout broke"],
            reported);
    }

    /// <summary>#238 D9, QE finding of #353: 1 ms reads only while a pad gets states, not while it rests for Arcadia in front.</summary>
    [Fact]
    [Trait("TestCase", "T238-11")]
    public async Task Fast_reads_are_wanted_only_while_a_plugged_pad_gets_the_controllers_states()
    {
        _others.Feed([DualSense]);
        Assert.Empty(_fast);

        await _emulation.TickAsync();
        _others.Feed([DualSense]);
        Assert.Equal([true], _fast);

        _forwarding = false;
        _others.Feed([DualSense]);
        _forwarding = true;
        _others.Feed([DualSense]);
        _others.Drop();

        Assert.Equal([true, false, true, false], _fast);
    }

    [Fact]
    [Trait("TestCase", "T238-1")]
    public async Task Without_a_Steam_Controller_the_emulation_starts_for_the_others_hides_each_model_and_plugs_a_pad_per_controller_in_connection_order()
    {
        _others.Feed([DualSense]);
        _others.Feed([DualSense, EightBitDo]);

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, _emulation.Status.State);
        Assert.Equal(
            ["started 1", "let go of controllers", $"hide {DualSenseModel}", $"hide {EightBitDoModel}", "read controllers", "pad plugged", "pad plugged"],
            _events);
        Assert.Empty(_pan.Hidden);
        Assert.False(_emulation.SteamControllerTakenOver);
        Assert.Equal(2, _others.Plugged);
        Assert.Contains("Controller für Spiele: DualSense für \"DualSense Wireless Controller\" (054C:0CE6) angesteckt", _log);
        Assert.Contains("Controller für Spiele: Xbox 360 Controller für \"8BitDo Ultimate 2 Wireless Controller for PC\" (2DC8:310B) angesteckt", _log);

        // Once hidden, a model isn't hidden again; Pan hides its new instances itself.
        _events.Clear();
        await _emulation.TickAsync();
        Assert.DoesNotContain(_events, e => e.StartsWith("hide", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T238-6")]
    public async Task Released_for_a_firmware_update_the_Steam_Controller_goes_to_Steam_and_the_others_stay_taken_over()
    {
        _steamControllerPresent = true;
        _others.Feed([DualSense]);
        await _emulation.TickAsync();
        Assert.Equal([1u], _pan.Hidden);

        await _emulation.ReleaseForFirmwareAsync();
        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, _emulation.Status.State);
        Assert.Equal([1u, 2u], _pan.Started);
        Assert.Equal([1u], _pan.Hidden);
        Assert.Contains(1u, _pan.Unhidden);
        Assert.False(_emulation.SteamControllerTakenOver);
        Assert.Equal([$"1 {DualSenseModel}", $"2 {DualSenseModel}"], _pan.ControllerHides);
    }

    [Fact]
    public async Task A_Steam_Controller_that_comes_later_is_hidden_within_the_running_emulation()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();
        Assert.Empty(_pan.Hidden);

        _steamControllerPresent = true;
        await _emulation.TickAsync();

        Assert.Equal([1u], _pan.Started);
        Assert.Equal([1u], _pan.Hidden);
        Assert.Equal(1, _copy.Attached);
        Assert.True(_emulation.SteamControllerTakenOver);
        Assert.Equal(1, _others.Plugged);
    }

    [Fact]
    [Trait("TestCase", "T238-7")]
    public async Task A_controller_Pan_cant_hide_isnt_emulated_and_is_listed_with_its_reason_until_it_reconnects()
    {
        _pan.ControllerHide = model => model == EightBitDoModel
            ? new ControllerHidingResult(false, PanReason.ControllerNotHideable)
            : new ControllerHidingResult(true, PanReason.None);
        _others.Feed([DualSense, EightBitDo]);
        // Counted from here: the feed told of the connected models already.
        var changes = 0;
        _others.Changed += () => changes++;

        await _emulation.TickAsync();
        await _emulation.TickAsync();

        Assert.Equal(1, _others.Plugged);
        Assert.Equal([new ControllerNotHidden(KnownPads.EightBitDoUltimate2.Name, EightBitDoModel, PanReason.ControllerNotHideable)], _others.NotHidden);
        Assert.Equal(1, changes);
        Assert.Equal(["1 054C:0CE6", "1 2DC8:310B"], _pan.ControllerHides);

        // Reconnected (another path): Pan tries again, and this time it works.
        _pan.ControllerHide = null;
        _time.Advance(TakenOverControllers.RereadGrace);
        _others.Feed([DualSense]);
        _others.Feed([DualSense, EightBitDo with { PadId = 9, Device = KnownPads.EightBitDoUltimate2 with { Path = "XInput#2" } }]);
        await _emulation.TickAsync();

        Assert.Equal(3, _pan.ControllerHides.Count);
        Assert.Empty(_others.NotHidden);
        Assert.Equal(2, _others.Plugged);
    }

    [Fact]
    [Trait("TestCase", "T238-12")]
    public async Task While_a_firmware_tool_runs_the_controller_waits_and_is_taken_over_once_it_has_ended()
    {
        _pan.ControllerHide = _ => new ControllerHidingResult(false, PanReason.FirmwareToolRuns);
        _others.Feed([DualSense]);
        await _emulation.TickAsync();

        Assert.Equal([new ControllerWaitingForTool(KnownPads.DualSense.Name, DualSenseModel)], _others.WaitingForTool);
        Assert.Empty(_others.NotHidden);
        Assert.Equal(0, _others.Plugged);

        _time.Advance(TimeSpan.FromSeconds(5));
        await _emulation.TickAsync();
        Assert.Single(_pan.ControllerHides);

        _pan.ControllerHide = null;
        _time.Advance(TimeSpan.FromSeconds(5));
        await _emulation.TickAsync();

        Assert.Equal(2, _pan.ControllerHides.Count);
        Assert.Empty(_others.WaitingForTool);
        Assert.Equal(1, _others.Plugged);
    }

    /// <summary>QE review of #338: each try lets go of every controller, so none is made while the tool still runs.</summary>
    [Fact]
    [Trait("TestCase", "T238-12")]
    public async Task While_the_firmware_tool_still_runs_the_other_controllers_are_never_let_go_of_again()
    {
        _pan.ControllerHide = model => model == DualSenseModel
            ? new ControllerHidingResult(false, PanReason.FirmwareToolRuns)
            : new ControllerHidingResult(true, PanReason.None);
        _runningTool = "FWupdater";
        _others.Feed([DualSense, EightBitDo]);
        await _emulation.TickAsync();
        Assert.Equal(1, _others.Plugged);

        for (var i = 0; i < 6; i++)
        {
            _time.Advance(TakenOverControllers.FirmwareToolRetry);
            _others.Feed([DualSense, EightBitDo]);
            await _emulation.TickAsync();
        }

        Assert.Equal([false, true], _reads);
        Assert.Equal(1, _others.Plugged);

        _runningTool = null;
        _pan.ControllerHide = null;
        _time.Advance(TakenOverControllers.FirmwareToolRetry);
        await _emulation.TickAsync();

        Assert.Equal([false, true, false, true], _reads);
        Assert.Empty(_others.WaitingForTool);
        Assert.Equal(2, _others.Plugged);
    }

    /// <summary>#335 A3: an XInput pad is hidden only from those who open it later; until it reconnects it isn't emulated (D7).</summary>
    [Fact]
    public async Task A_controller_Windows_restarts_only_on_reconnect_gets_its_pad_once_it_reconnected()
    {
        _pan.ControllerHide = _ => new ControllerHidingResult(true, PanReason.ControllerNeedsReplug);
        _others.Feed([EightBitDo]);
        await _emulation.TickAsync();
        Assert.Equal(0, _others.Plugged);
        Assert.Equal([new ControllerNotHidden(KnownPads.EightBitDoUltimate2.Name, EightBitDoModel, PanReason.ControllerNeedsReplug)], _others.NotHidden);

        _time.Advance(TakenOverControllers.RereadGrace);
        _others.Feed([]);
        await _emulation.TickAsync();
        _others.Feed([EightBitDo with { PadId = 7 }]);
        await _emulation.TickAsync();

        Assert.Equal(1, _others.Plugged);
        Assert.Single(_pan.ControllerHides);
        Assert.Empty(_others.NotHidden);
    }

    [Fact]
    [Trait("TestCase", "T238-3")]
    public async Task Each_pad_gets_only_its_controller_without_Arcadias_combos_and_with_Guide()
    {
        _others.Feed([DualSense, EightBitDo]);
        await _emulation.TickAsync();

        _others.Feed([DualSense with { Buttons = PadButtons.A, Guide = true }, EightBitDo with { Buttons = PadButtons.View | PadButtons.Menu }]);

        var dualSensePad = _pads.All[0];
        var eightBitDoPad = _pads.All[1];
        var sent = await dualSensePad.WaitForAsync(state => state.Buttons != 0);
        Assert.Equal(0x1000 | XboxPadState.GuideBit, sent.Buttons);
        _time.Advance(TimeSpan.FromSeconds(1));
        _others.Feed([DualSense, EightBitDo with { Buttons = PadButtons.View | PadButtons.Menu }]);
        await Task.Delay(50);
        Assert.DoesNotContain(eightBitDoPad.Sent, state => (state.Buttons & 0x0030) != 0);
    }

    [Fact]
    [Trait("TestCase", "T238-4")]
    public async Task While_Arcadia_is_in_front_every_pad_rests()
    {
        _others.Feed([DualSense, EightBitDo]);
        await _emulation.TickAsync();
        _others.Feed([DualSense with { Buttons = PadButtons.A }, EightBitDo with { Buttons = PadButtons.B }]);
        await _pads.All[1].WaitForAsync(state => state.Buttons == 0x2000);

        _forwarding = false;
        _others.Feed([DualSense with { Buttons = PadButtons.A }, EightBitDo with { Buttons = PadButtons.B }]);

        await _pads.All[0].WaitForAsync(state => state == XboxPadState.Neutral);
        await _pads.All[1].WaitForAsync(state => state == XboxPadState.Neutral);
    }

    [Fact]
    public async Task The_Steam_Controller_is_left_to_its_own_path()
    {
        _others.Feed([SteamController]);
        Assert.False(_others.AnyConnected);

        await _emulation.TickAsync();

        Assert.Empty(_pan.ControllerHides);
    }

    /// <summary>Scenario 3: a controller unplugged loses its pad at the next tick, and its rumble stops.</summary>
    [Fact]
    public async Task A_controller_unplugged_loses_its_pad_at_the_next_tick_and_a_games_rumble_reaches_it_until_then()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();
        _pads.All[0].Rumble(255, 10);
        Assert.Equal(["1: 255/10"], _rumbles);

        _time.Advance(TakenOverControllers.RereadGrace);
        _others.Feed([]);
        await _emulation.TickAsync();

        Assert.True(_pads.All[0].IsUnplugged);
        Assert.Equal(["1: 255/10", "1: 0/0"], _rumbles);
        Assert.Equal(0, _others.Plugged);
    }

    /// <summary>After SDL starts again (a hide), controllers come back over a few polls with new ids: their pads stay.</summary>
    [Fact]
    public async Task A_controller_missing_right_after_SDL_started_again_keeps_its_pad()
    {
        _others.Feed([DualSense, EightBitDo]);
        await _emulation.TickAsync();

        _others.Feed([DualSense with { PadId = 11 }]);
        await _emulation.TickAsync();

        Assert.Equal(2, _others.Plugged);
        Assert.Equal(0, _pads.Unplugged);
    }

    /// <summary>QE of #338: the new SDL handle after a let-go doesn't rumble, so a constant rumble is set on it again.</summary>
    [Fact]
    public async Task A_games_rumble_carries_over_to_the_controllers_new_id_after_SDL_started_again()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();
        _pads.All[0].Rumble(255, 10);

        _others.Feed([DualSense with { PadId = 11 }]);
        _others.Feed([DualSense with { PadId = 11 }]);

        Assert.Equal(["1: 255/10", "11: 255/10"], _rumbles);

        // Stopped by the game: nothing to carry over.
        _pads.All[0].Rumble(0, 0);
        _others.Feed([DualSense with { PadId = 12 }]);
        Assert.Equal(["1: 255/10", "11: 255/10", "11: 0/0"], _rumbles);
    }

    /// <summary>#238 part 2 (D3, D3b): a controller's own kind where VIIPER has it, an Xbox 360 pad otherwise.</summary>
    [Fact]
    [Trait("TestCase", "T238-1")]
    public async Task Each_controller_gets_a_pad_of_its_own_kind_and_one_without_a_1_to_1_kind_an_Xbox_360_pad()
    {
        var switch2 = Reading(4, KnownPads.Switch2Pro with { Path = "hid-switch2" });
        var gameCube = Reading(5, KnownPads.GameCube with { Path = "hid-gamecube" });
        _others.Feed([DualSense, EightBitDo, switch2, gameCube]);

        await _emulation.TickAsync();

        Assert.Equal([PadKind.DualSense, PadKind.Xbox360, PadKind.Switch2Pro, PadKind.Xbox360], _pads.All.Select(pad => pad.Kind));
    }

    /// <summary>D6: what the emulated kind has passes on, the touchpad, motion and mute of a DualSense included.</summary>
    [Fact]
    public async Task A_DualSense_passes_its_touchpad_motion_and_extra_buttons_to_its_pad()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();
        var motion = new MotionReading(true, new SensorAxes(1, 2, 3), new SensorAxes(0, 9.8f, 0));
        var touched = DualSense with
        {
            Guide = true,
            Extra = ExtraButtons.Misc1,
            LeftTouchpad = new TouchpadReading(true, 0.5, -0.5, Clicked: true),
            Motion = motion,
        };

        _others.Feed([touched]);

        var state = await WaitForStateAsync(_pads.All[0], state => state.Motion.IsOn);
        Assert.True(state.Guide);
        Assert.Equal(ExtraButtons.Misc1, state.Extra);
        Assert.Equal(new TouchpadReading(true, 0.5, -0.5, true), state.Touchpad);
        Assert.Equal(motion, state.Motion);
    }

    /// <summary>A5: the light bar a game sets reaches the controller, also after SDL started again.</summary>
    [Fact]
    public async Task A_games_light_bar_reaches_the_controller_and_carries_over_to_its_new_id()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();

        _pads.All[0].Light(255, 0, 64);
        _pads.All[0].Light(255, 0, 64);
        _others.Feed([DualSense with { PadId = 11 }]);

        Assert.Equal(["1: 255,0,64", "11: 255,0,64"], _lights);
    }

    /// <summary>The controllers' motion sensors cost battery: on only while a pad carries motion.</summary>
    [Fact]
    public async Task Motion_sensors_are_on_only_while_a_pad_that_carries_motion_is_plugged()
    {
        _others.Feed([EightBitDo]);
        await _emulation.TickAsync();
        Assert.Empty(_motion);

        _others.Feed([EightBitDo, DualSense]);
        await _emulation.TickAsync();
        await _emulation.TickAsync();
        Assert.Equal([true], _motion);

        _time.Advance(TakenOverControllers.RereadGrace);
        _others.Feed([EightBitDo]);
        await _emulation.TickAsync();
        Assert.Equal([true, false], _motion);
    }

    /// <summary>QE review of #346: Arcadia's own pads are known while plugged, so they are never taken for real controllers.</summary>
    [Fact]
    public async Task Arcadias_own_pads_are_known_by_their_ids_while_they_are_plugged()
    {
        _others.Feed([DualSense, EightBitDo]);
        await _emulation.TickAsync();

        Assert.True(_others.OwnPads.Has(0x054C, 0x0CE6));

        _time.Advance(TakenOverControllers.RereadGrace);
        _others.Feed([EightBitDo]);
        await _emulation.TickAsync();

        Assert.False(_others.OwnPads.Has(0x054C, 0x0CE6));
    }

    /// <summary>QE review of #346: a gyro read for another pad doesn't make an Xbox 360 pad's state change on every poll.</summary>
    [Fact]
    public async Task An_Xbox_360_pad_gets_no_new_state_when_only_motion_changes()
    {
        _others.Feed([EightBitDo]);
        await _emulation.TickAsync();
        var pad = _pads.All[0];

        // The forwarding runs on its own thread: its first state can take longer than a fixed wait on a slow CI runner.
        var firstState = Stopwatch.StartNew();
        while (pad.States.Count == 0 && firstState.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        await Task.Delay(50);
        var before = pad.States.Count;
        Assert.NotEqual(0, before);

        for (var i = 1; i <= 5; i++)
        {
            _others.Feed([EightBitDo with { Motion = new MotionReading(true, new SensorAxes(i, 0, 0), default) }]);
        }

        await Task.Delay(50);
        Assert.Equal(before, pad.States.Count);
    }

    private static async Task<VirtualPadState> WaitForStateAsync(FakeVirtualXboxPads.FakePad pad, Func<VirtualPadState, bool> test)
    {
        for (var i = 0; i < 200; i++)
        {
            if (pad.States is [.., var last] && test(last))
            {
                return last;
            }

            await Task.Delay(10);
        }

        throw new Xunit.Sdk.XunitException("never sent");
    }

    [Fact]
    public async Task While_the_controllers_are_let_go_Pan_still_hears_the_heartbeat()
    {
        var pulses = 0;
        var others = new TakenOverControllers(_time, Log) { Pulse = () => pulses++ };
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, Log) { SteamControllerPresent = () => false, Others = others };
        _pan.DuringControllerHide = () => _time.Advance(TimeSpan.FromSeconds(1));
        others.Feed([DualSense]);

        await emulation.TickAsync();

        Assert.True(pulses >= 1);
    }

    [Fact]
    [Trait("TestCase", "T238-9")]
    public async Task Pads_lost_too_often_give_up_with_a_toast_and_the_emulation_stops()
    {
        string? gaveUp = null;
        _others.GaveUp += text =>
        {
            gaveUp = text;
            _emulation.PadGaveUp();
        };
        _others.Feed([DualSense]);
        await _emulation.TickAsync();

        for (var i = 0; i <= TakenOverControllers.MaxLossesPerWindow && gaveUp is null; i++)
        {
            _pads.Current!.Lose();
            await _emulation.TickAsync();
        }

        Assert.NotNull(gaveUp);
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Stopped, _emulation.Status.State);
        Assert.Contains(1u, _pan.Stopped);
    }

    [Fact]
    public async Task The_emulations_end_takes_every_pad_out_and_a_new_one_hides_the_models_again()
    {
        _others.Feed([DualSense]);
        await _emulation.TickAsync();

        _pan.TearDown();
        await _emulation.TickAsync();

        Assert.Equal([1u, 2u], _pan.Started);
        Assert.Equal([$"1 {DualSenseModel}", $"2 {DualSenseModel}"], _pan.ControllerHides);
        Assert.Equal(1, _others.Plugged);
        Assert.Equal(2, _pads.Plugged);
    }

    private static readonly ControllerModel GameCubeModel = new(0x057E, 0x0337);
    private static readonly GamepadReading GameCube = Reading(5, KnownPads.GameCube with { Path = "hid-gamecube" });

    private void Choose(ControllerModel model, ControllerLayout layout) => _layouts[ControllerLayouts.Key(model)] = ControllerLayouts.Value(layout);

    /// <summary>#238 part 3 (D3a): the kind chosen per model, also for one with a 1:1 kind of its own.</summary>
    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task A_chosen_layout_gives_every_controller_of_the_model_that_kind()
    {
        Choose(DualSenseModel, ControllerLayout.Of(PadKind.Xbox360));
        Choose(GameCubeModel, ControllerLayout.Of(PadKind.Switch2Pro));
        _others.Feed([DualSense, GameCube, DualSense with { PadId = 7, Device = DualSense.Device with { Path = "hid-dualsense-2" } }]);

        await _emulation.TickAsync();

        Assert.Equal([PadKind.Xbox360, PadKind.Switch2Pro, PadKind.Xbox360], _pads.All.Select(pad => pad.Kind));
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task A_new_layout_mid_game_replaces_the_pad_once_and_keeps_the_controller_hidden()
    {
        _others.Feed([DualSense, GameCube]);
        await _emulation.TickAsync();
        _events.Clear();

        Choose(DualSenseModel, ControllerLayout.Of(PadKind.Xbox360));
        await _emulation.TickAsync();

        Assert.Equal(1, _pads.Unplugged);
        Assert.Equal(["pad unplugged", "pad plugged"], _events);
        Assert.Equal([PadKind.DualSense, PadKind.Xbox360, PadKind.Xbox360], _pads.All.Select(pad => pad.Kind));
        Assert.Contains(_log, line => line.StartsWith("Controller für Spiele: DualSense für \"DualSense Wireless Controller\" abgesteckt (Layout Xbox 360 Controller)", StringComparison.Ordinal));

        // Applied once: the next tick changes nothing.
        _events.Clear();
        await _emulation.TickAsync();
        Assert.Empty(_events);
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task Not_taken_over_unplugs_the_pad_first_then_Pan_shows_the_model_and_taken_over_again_hides_it()
    {
        _others.Feed([DualSense, GameCube]);
        await _emulation.TickAsync();
        _events.Clear();

        Choose(GameCubeModel, ControllerLayout.NotTakenOver);
        await _emulation.TickAsync();

        Assert.Equal(["pad unplugged", "let go of controllers", $"show {GameCubeModel}", "read controllers"], _events);
        Assert.Equal(1, _others.Plugged);
        Assert.Empty(_others.NotHidden);

        // Left alone from now on: never hidden again while the choice stands.
        _events.Clear();
        _others.Feed([DualSense, GameCube]);
        await _emulation.TickAsync();
        Assert.Empty(_events);

        _layouts.Clear();
        await _emulation.TickAsync();
        Assert.Equal(["let go of controllers", $"hide {GameCubeModel}", "read controllers", "pad plugged"], _events);
        Assert.Equal(2, _others.Plugged);
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task A_model_not_taken_over_is_never_hidden_and_alone_starts_no_emulation()
    {
        Choose(GameCubeModel, ControllerLayout.NotTakenOver);
        _others.Feed([GameCube]);

        Assert.False(_others.AnyConnected);
        await _emulation.TickAsync();
        Assert.Empty(_pan.Started);

        _others.Feed([GameCube, DualSense]);
        await _emulation.TickAsync();
        Assert.Equal([$"1 {DualSenseModel}"], _pan.ControllerHides);
        Assert.Equal([PadKind.DualSense], _pads.All.Select(pad => pad.Kind));
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task A_model_not_taken_over_isnt_listed_as_not_hideable()
    {
        _pan.ControllerHide = model => new ControllerHidingResult(false, PanReason.ControllerNotHideable);
        _others.Feed([GameCube]);
        await _emulation.TickAsync();
        Assert.Single(_others.NotHidden);

        Choose(GameCubeModel, ControllerLayout.NotTakenOver);
        await _emulation.TickAsync();

        Assert.Empty(_others.NotHidden);
        Assert.Empty(_pan.ControllerShows);
    }

    /// <summary>
    /// QE review of #350: Pan answers HidingFailed also when it showed the model and only its restart failed, so a pad
    /// could make two. No pad then, and taken over again, the model is hidden anew.
    /// </summary>
    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task A_show_Pan_refuses_plugs_no_pad_and_taken_over_again_the_model_is_hidden_anew()
    {
        _pan.ControllerShow = _ => new ControllerHidingResult(false, PanReason.HidingFailed);
        _others.Feed([GameCube]);
        await _emulation.TickAsync();

        Choose(GameCubeModel, ControllerLayout.NotTakenOver);
        await _emulation.TickAsync();
        await _emulation.TickAsync();

        Assert.Equal(0, _others.Plugged);
        Assert.Equal([$"1 {GameCubeModel}"], _pan.ControllerShows);
        Assert.Contains(_log, line => line.StartsWith("Controller für Spiele: 057E:0337 nicht sicher wieder gezeigt (HidingFailed), kein Pad", StringComparison.Ordinal));

        _events.Clear();
        _layouts.Clear();
        await _emulation.TickAsync();
        Assert.Equal(["let go of controllers", $"hide {GameCubeModel}", "read controllers", "pad plugged"], _events);
        Assert.Equal(1, _others.Plugged);
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public async Task The_Edge_is_offered_only_to_a_real_Edge()
    {
        Choose(DualSenseModel, ControllerLayout.Of(PadKind.DualSenseEdge));
        _others.Feed([DualSense]);

        await _emulation.TickAsync();

        Assert.Equal([PadKind.DualSense], _pads.All.Select(pad => pad.Kind));
    }

    [Fact]
    [Trait("TestCase", "T238-13")]
    public void The_connected_models_are_listed_once_each_in_connection_order_also_those_not_taken_over()
    {
        Choose(GameCubeModel, ControllerLayout.NotTakenOver);
        var changes = 0;
        _others.Changed += () => changes++;

        _others.Feed([GameCube, DualSense, DualSense with { PadId = 7, Device = DualSense.Device with { Path = "hid-dualsense-2" } }, SteamController]);
        _others.Feed([GameCube, DualSense, DualSense with { PadId = 7, Device = DualSense.Device with { Path = "hid-dualsense-2" } }, SteamController]);

        Assert.Equal(
            [new ConnectedModel(GameCubeModel, "Nintendo GameCube Controller", PadKind.Xbox360), new ConnectedModel(DualSenseModel, "DualSense Wireless Controller", PadKind.DualSense)],
            _others.Connected);
        Assert.Equal(1, changes);

        _others.Feed([DualSense]);
        Assert.Equal([DualSenseModel], _others.Connected.Select(model => model.Model));
        Assert.Equal(2, changes);
    }
}
