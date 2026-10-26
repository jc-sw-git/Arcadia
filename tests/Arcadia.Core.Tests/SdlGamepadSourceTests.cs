using Arcadia.Input;
using Arcadia.Tests.Sdl;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.Core.Tests;

public sealed class SdlGamepadSourceTests : IDisposable
{
    private readonly FakeSdlGamepads _sdl = new();
    private readonly List<string> _log = [];
    private readonly List<Exception> _failures = [];
    private readonly List<string> _events = [];
    private readonly List<IReadOnlyList<GamepadReading>> _polls = [];
    private readonly SdlGamepadSource _source;
    private long _now;

    public SdlGamepadSourceTests()
    {
        _source = new SdlGamepadSource(_sdl, line => { lock (_log) { _log.Add(line); } }, error => { lock (_failures) { _failures.Add(error); } });
        _source.ButtonPressed += button => _events.Add($"+{button}");
        _source.ButtonReleased += button => _events.Add($"-{button}");
        _source.Polled += pads => _polls.Add([.. pads]);
    }

    [Fact]
    public void Connects_and_disconnects_are_logged_with_type_and_USB_ids()
    {
        Assert.True(_source.Open());
        var pad = _sdl.Plug(KnownPads.DualSense);
        Poll();
        _sdl.Unplug(pad);
        Poll();

        Assert.Equal(
        [
            "Controller: SDL 3.4.18 bereit",
            $"Controller #{pad} verbunden: \"DualSense Wireless Controller\" (PS5, 054C:0CE6)",
            $"Controller #{pad} getrennt: \"DualSense Wireless Controller\" (PS5, 054C:0CE6)",
        ], _log);
        Assert.Equal([pad], _sdl.Closed);
    }

    [Fact]
    public void Steam_s_virtual_pad_and_the_Steam_Deck_are_never_opened()
    {
        _source.Open();
        var virtualPad = _sdl.Plug(KnownPads.SteamVirtual);
        _sdl.Plug(new PadDevice("Steam Deck", PadDevice.ValveVendor, 0x1205, PadType.Standard, StickDeviceType.Gamepad));
        _sdl.Hold(virtualPad, B.South);
        Poll();

        Assert.Empty(_sdl.OpenPads);
        Assert.Empty(_events);
        Assert.Empty(_polls[^1]);
        Assert.Contains(_log, line => line.Contains("ignoriert (virtueller Controller von Steam)"));
        Assert.Contains(_log, line => line.Contains("ignoriert (Gerät von Valve, kein Steam Controller)"));
    }

    /// <summary>#212: the Steam Controller raw; its Steam button, "…" and back grips do nothing, the hands resting on it neither.</summary>
    [Fact]
    public void The_Steam_Controller_is_read_and_its_Steam_button_its_dots_button_and_back_grips_do_nothing()
    {
        _source.Open();
        var pad = _sdl.Plug(KnownPads.SteamController);
        Poll();
        Assert.Equal([pad], _sdl.OpenPads);
        Assert.Contains($"Controller #{pad} verbunden: \"Steam Controller\" (Steam, 28DE:1304)", _log);

        foreach (var button in new[] { B.Guide, B.Misc1, B.RightPaddle1, B.LeftPaddle1, B.RightPaddle2, B.LeftPaddle2 })
        {
            _sdl.Hold(pad, button);
            Poll();
            Assert.False(ControllerActivity.IsInUse(_polls[^1][0]), $"{button} gilt nicht als Bedienung");
        }

        _sdl.Hold(pad, B.South, B.Back);
        Poll();

        Assert.Equal(["+A", "+View"], _events.Order());
        Assert.True(_polls[^1][0].IsSteamController);
    }

    [Fact]
    public void Only_the_Steam_Controller_s_reading_says_it_is_one()
    {
        _source.Open();
        _sdl.Plug(KnownPads.DualSense);
        _sdl.Plug(KnownPads.SteamController);
        Poll();

        Assert.Equal([false, true], _polls[^1].Select(reading => reading.IsSteamController));
    }

    /// <summary>#212: both trackpads come along (touch, position, click), without a function yet (#111).</summary>
    [Fact]
    public void The_readings_carry_both_trackpads_with_touch_position_and_click()
    {
        _source.Open();
        var pad = _sdl.Plug(KnownPads.SteamController);
        _sdl.Set(pad, new SdlPadState(SdlPadState.Bit(B.Misc2), 0, 0, 0, 0, 0, 0)
        {
            FirstTouchpad = new SdlTouch(true, 0.25f, 0.25f, 0.4f),
            SecondTouchpad = new SdlTouch(true, 1, 0.5f, 1),
        });
        Poll();

        var reading = Assert.Single(_polls[^1]);
        Assert.Equal(new TouchpadReading(true, -0.5, 0.5, false), reading.LeftTouchpad);
        Assert.Equal(new TouchpadReading(true, 1, 0, true), reading.RightTouchpad);
        Assert.Empty(_events);
    }

    /// <summary>#212: the IMU only goes on when a feature asks (battery; Steam's own gyro untouched), also for pads connected later.</summary>
    [Fact]
    public void Gyro_and_accelerometer_are_off_until_asked_for_and_go_on_for_a_controller_connected_later()
    {
        _source.Open();
        var first = _sdl.Plug(KnownPads.SteamController);
        _sdl.Plug(KnownPads.Xbox360);
        Poll();
        Assert.Empty(_sdl.MotionOn);
        Assert.All(_polls[^1], reading => Assert.False(reading.Motion.IsOn));

        _source.UseMotionSensors(true);
        Poll();
        Assert.Equal([first], _sdl.MotionOn);
        Assert.True(_polls[^1].Single(reading => reading.IsSteamController).Motion.IsOn);

        _sdl.Unplug(first);
        var second = _sdl.Plug(KnownPads.SteamController);
        Poll();
        Assert.Equal([second], _sdl.MotionOn);

        _source.UseMotionSensors(false);
        Poll();
        Assert.Empty(_sdl.MotionOn);
    }

    /// <summary>#212, scenario 3: SISR's virtual Xbox pad mirrors the Steam Controller; every press counts once.</summary>
    [Fact]
    public void SISR_s_virtual_Xbox_pad_is_ignored_and_a_press_counts_once()
    {
        var log = new List<string>();
        using var source = new SdlGamepadSource(_sdl, log.Add, _ => { }, new DeviceTreeEmulatedPads(() => [new XusbDevice(true, 0)], log.Add));
        var events = new List<string>();
        source.ButtonPressed += button => events.Add($"+{button}");
        source.Open();
        var steamController = _sdl.Plug(KnownPads.SteamController);
        var sisrPad = _sdl.Plug(KnownPads.SisrXbox360);
        source.Poll(0);

        _sdl.Hold(steamController, B.South);
        _sdl.Hold(sisrPad, B.South);
        source.Poll(8);

        Assert.Equal(["+A"], events);
        Assert.Equal([steamController], _sdl.OpenPads);
        Assert.Contains($"Controller #{sisrPad} ignoriert (virtueller Xbox Controller, z. B. von SISR): \"Xbox 360 Controller\" (Xbox360, 045E:028E)", log);
    }

    [Fact]
    public void A_real_Xbox_360_pad_beside_SISR_s_is_read()
    {
        using var source = new SdlGamepadSource(_sdl, _ => { }, _ => { },
            new DeviceTreeEmulatedPads(() => [new XusbDevice(false, 0), new XusbDevice(true, 1)], _ => { }));
        source.Open();
        var real = _sdl.Plug(KnownPads.Xbox360);
        _sdl.Plug(KnownPads.SisrXbox360 with { Path = "XInput#1" });
        source.Poll(0);

        Assert.Equal([real], _sdl.OpenPads);
    }

    [Fact]
    public void Several_controllers_are_read_at_once_each_with_its_own_id()
    {
        _source.Open();
        var xbox = _sdl.Plug(KnownPads.Xbox360);
        var switchPro = _sdl.Plug(KnownPads.SwitchPro);
        _sdl.Hold(xbox, B.South);
        _sdl.Hold(switchPro, B.East);
        Poll();

        Assert.Equal([((int)xbox, PadButtons.A), ((int)switchPro, PadButtons.B)], _polls[^1].Select(p => (p.PadId, p.Buttons)));
        Assert.Equal(["+A", "+B"], _events.Order());
    }

    [Fact]
    public void A_pad_plugged_in_while_Arcadia_runs_works_and_one_pulled_out_lets_go_of_its_buttons()
    {
        _source.Open();
        Poll();
        var pad = _sdl.Plug(KnownPads.EightBitDoUltimate2);
        _sdl.Hold(pad, B.DPadDown);
        Poll();
        _sdl.Unplug(pad);
        Poll();

        Assert.Equal(["+DPadDown", "-DPadDown"], _events);
    }

    /// <summary>T72-4: only the mapping of a DualShock 4 is checked (no such controller at hand), mechanically.</summary>
    [Fact]
    [Trait("TestCase", "T72-4")]
    public void A_DualShock_4_maps_by_position_and_its_PS_button_and_touchpad_click_do_nothing()
    {
        _source.Open();
        var pad = _sdl.Plug(KnownPads.DualShock4);
        foreach (var (button, expected) in new[] { (B.South, "A"), (B.East, "B"), (B.West, "X"), (B.North, "Y"), (B.Back, "View"), (B.Start, "Menu") })
        {
            _sdl.Hold(pad, button);
            Poll();
            _sdl.Hold(pad);
            Poll();
            Assert.Equal([$"+{expected}", $"-{expected}"], _events);
            _events.Clear();
        }

        _sdl.Hold(pad, B.Guide, B.Touchpad);
        Poll();
        Assert.Empty(_events);
        Assert.Equal(PadButtons.None, _polls[^1].Single().Buttons);
    }

    [Fact]
    public void Rumble_goes_to_the_pad_asked_for_and_a_gone_pad_is_skipped()
    {
        _source.Open();
        _sdl.Plug(KnownPads.Xbox360);
        var dualSense = _sdl.Plug(KnownPads.DualSense);
        Poll();

        _source.Rumble((int)dualSense);
        _source.Rumble(99);
        Poll();

        Assert.Equal([$"pad {dualSense}: 30000 for 150 ms"], _sdl.Rumbles);
    }

    [Fact]
    public void Without_a_usable_SDL3_the_error_is_reported_once_and_nothing_is_polled()
    {
        _sdl.InitError = new DllNotFoundException("Unable to load DLL 'SDL3'");

        Assert.False(_source.Open());
        Assert.False(_source.AnyNow(_ => true));
        _source.Close();

        Assert.IsType<DllNotFoundException>(Assert.Single(_failures));
        Assert.False(_sdl.IsQuit);
    }

    [Fact]
    public async Task A_native_error_while_polling_is_reported_and_SDL_is_closed_instead_of_ending_Arcadia()
    {
        _sdl.Plug(KnownPads.Xbox360);
        _sdl.ReadError = new InvalidOperationException("SDL broke");

        _source.Start();
        // The error is reported last, after SDL is closed: waiting for IsQuit alone can look before the report.
        await WaitUntil(() => { lock (_failures) { return _failures.Count > 0; } });

        Assert.True(_sdl.IsQuit);
        lock (_failures)
        {
            Assert.Equal("SDL broke", Assert.Single(_failures).Message);
        }
    }

    [Fact]
    public void A_failing_poll_closes_SDL_raises_Lost_once_and_reports_the_error_last()
    {
        var order = new List<string>();
        _source.Lost += () => order.Add($"lost, SDL quit: {_sdl.IsQuit}, reported: {_failures.Count}");
        _source.Open();
        var pad = _sdl.Plug(KnownPads.DualSense);
        _sdl.Hold(pad, B.South);
        Poll();
        _sdl.ReadError = new InvalidOperationException("SDL broke");

        Assert.False(_source.Poll(_now));
        Assert.False(_source.Poll(_now + 8));

        Assert.Equal(["lost, SDL quit: True, reported: 0"], order);
        Assert.Equal("SDL broke", Assert.Single(_failures).Message);
        Assert.Equal(["+A"], _events);
    }

    [Fact]
    public void Every_handler_of_Lost_runs_even_if_one_throws()
    {
        var ran = new List<int>();
        _source.Lost += () => throw new InvalidOperationException("handler broke");
        _source.Lost += () => ran.Add(2);
        _source.Open();
        _sdl.Plug(KnownPads.Xbox360);
        _sdl.ReadError = new InvalidOperationException("SDL broke");

        _source.Poll(0);

        Assert.Equal([2], ran);
        Assert.Equal(["handler broke", "SDL broke"], _failures.Select(f => f.Message));
    }

    [Fact]
    public async Task The_poll_thread_reads_on_its_own_and_closes_everything_on_dispose()
    {
        var pad = _sdl.Plug(KnownPads.SwitchPro);
        _sdl.Hold(pad, B.South);
        var pressed = new TaskCompletionSource();
        _source.ButtonPressed += button => pressed.TrySetResult();

        _source.Start();
        await pressed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _source.Dispose();

        Assert.True(_sdl.IsQuit);
        Assert.Equal([pad], _sdl.Closed);
    }

    [Fact]
    public void A_fresh_look_reads_the_controllers_anew()
    {
        _source.Open();
        var pad = _sdl.Plug(KnownPads.DualSense);
        Poll();
        var updates = _sdl.Updates;

        Assert.False(_source.AnyNow(ControllerActivity.IsInUse));
        _sdl.Hold(pad, B.South);
        Assert.True(_source.AnyNow(ControllerActivity.IsInUse));
        Assert.Equal(updates + 2, _sdl.Updates);
    }

    [Fact]
    public void A_fresh_look_that_fails_answers_no_instead_of_throwing_into_the_input_hook()
    {
        _source.Open();
        _sdl.Plug(KnownPads.Xbox360);
        Poll();
        _sdl.ReadError = new InvalidOperationException("SDL broke");

        Assert.False(_source.AnyNow(_ => true));
    }

    [Fact]
    public void Close_lets_go_of_every_pad_and_of_SDL()
    {
        _source.Open();
        var a = _sdl.Plug(KnownPads.Xbox360);
        var b = _sdl.Plug(KnownPads.DualShock4);
        Poll();

        _source.Close();

        Assert.Equal([a, b], _sdl.Closed.Order());
        Assert.True(_sdl.IsQuit);
        Assert.False(_source.AnyNow(_ => true));
    }

    public void Dispose() => _source.Dispose();

    private void Poll()
    {
        _source.Poll(_now);
        _now += SdlGamepadSource.PollIntervalMs;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "timed out");
            await Task.Delay(10);
        }
    }
}
