using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class MouseModeTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(8);
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(3);
    private static readonly StickPosition Centre = new(0, 0);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingOutput _output = new();
    private readonly List<string> _events = [];
    private MouseModeOptions _options = new(Enabled: true, ComboEnabled: true, HoldTime, PointerSpeed: 5);
    private readonly MouseMode _mode;

    public MouseModeTests()
    {
        _mode = new MouseMode(_output, () => _options, _time);
        _mode.HoldStarted += rest => _events.Add($"hold {rest.TotalSeconds:0.0}");
        _mode.HoldCancelled += () => _events.Add("cancelled");
        _mode.Switched += s => _events.Add($"{(s.IsOn ? "on" : "off")} {(s.IsAutomatic ? "auto" : $"slot {s.Slot}")}");
    }

    [Fact]
    public void Holding_back_and_start_switches_on_after_the_hold_time_with_feedback_from_0_2_s()
    {
        HoldFor(PadButtons.View | PadButtons.Menu, TimeSpan.FromMilliseconds(100));
        Assert.Empty(_events);

        HoldFor(PadButtons.View | PadButtons.Menu, TimeSpan.FromMilliseconds(150));
        Assert.Equal(["hold 2.8"], _events);

        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime);
        Assert.Equal(["hold 2.8", "on slot 0"], _events);
        Assert.True(_mode.IsOn);
        Assert.False(_mode.IsAutomatic);
    }

    [Fact]
    public void Letting_go_early_cancels()
    {
        HoldFor(PadButtons.View | PadButtons.Menu, TimeSpan.FromSeconds(1));
        HoldFor(PadButtons.View, Poll);

        Assert.Equal(["hold 2.8", "cancelled"], _events);
        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Keeping_the_combination_held_switches_only_once()
    {
        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime * 3);

        Assert.Equal(["hold 2.8", "on slot 0"], _events);
    }

    [Fact]
    public void The_combination_switches_back_off()
    {
        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime + Poll);
        HoldFor(PadButtons.None, Poll);
        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime + Poll);

        Assert.Equal(["hold 2.8", "on slot 0", "hold 2.8", "off slot 0"], _events);
        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Back_on_one_controller_and_start_on_another_is_no_combination()
    {
        for (var held = TimeSpan.Zero; held < HoldTime * 2; held += Poll)
        {
            Update(Pad(0, PadButtons.View), Pad(1, PadButtons.Menu));
        }

        Assert.Empty(_events);
    }

    [Fact]
    public void Any_controller_can_switch()
    {
        for (var held = TimeSpan.Zero; held <= HoldTime; held += Poll)
        {
            Update(Pad(0, PadButtons.None), Pad(2, PadButtons.View | PadButtons.Menu));
        }

        Assert.Equal(["hold 2.8", "on slot 2"], _events);
    }

    [Fact]
    public void Switched_off_in_the_settings_the_combination_does_nothing()
    {
        _options = _options with { ComboEnabled = false };

        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime * 2);

        Assert.Empty(_events);
    }

    [Fact]
    public void The_mouse_mode_switched_off_in_the_settings_ends_and_stays_off()
    {
        SwitchOn();
        HoldFor(PadButtons.A, Poll);
        _events.Clear();

        _options = _options with { Enabled = false };
        HoldFor(PadButtons.None, Poll);
        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime * 2);
        HoldFor(PadButtons.None, Poll);
        _mode.Request(on: true);
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["off auto"], _events);
        Assert.False(_mode.IsOn);
        Assert.Equal(["LeftButton down", "LeftButton up"], _output.Events);
    }

    [Fact]
    public void The_hold_time_comes_from_the_settings()
    {
        _options = _options with { HoldTime = TimeSpan.FromSeconds(1) };

        HoldFor(PadButtons.View | PadButtons.Menu, TimeSpan.FromSeconds(1) + Poll);

        Assert.Equal(["hold 0.8", "on slot 0"], _events);
    }

    [Fact]
    public void A_requested_switch_is_automatic_and_shows_no_hold()
    {
        _mode.Request(true);
        Update();

        Assert.Equal(["on auto"], _events);
        Assert.True(_mode.IsAutomatic);
    }

    [Fact]
    public void Buttons_do_nothing_while_the_mode_is_off()
    {
        HoldFor(PadButtons.A | PadButtons.B | PadButtons.DPadUp, Poll * 3);
        Update(Pad(0, PadButtons.None, left: new StickPosition(1, 0)));

        Assert.Empty(_output.Events);
    }

    [Fact]
    public void A_and_rt_press_the_left_button_and_x_and_lt_the_right_one()
    {
        SwitchOn();

        HoldFor(PadButtons.A, Poll);
        HoldFor(PadButtons.None, Poll);
        HoldFor(PadButtons.RightTrigger, Poll);
        HoldFor(PadButtons.X | PadButtons.RightTrigger, Poll);
        HoldFor(PadButtons.LeftTrigger, Poll);
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(
            ["LeftButton down", "LeftButton up", "LeftButton down", "RightButton down", "LeftButton up", "RightButton up"],
            _output.Events);
    }

    [Fact]
    public void A_button_held_when_the_mode_starts_does_not_click_until_pressed_again()
    {
        HoldFor(PadButtons.A, Poll);
        _mode.Request(true);
        HoldFor(PadButtons.A, Poll * 5);
        HoldFor(PadButtons.None, Poll);
        Assert.Empty(_output.Events);

        HoldFor(PadButtons.A, Poll);
        Assert.Equal(["LeftButton down"], _output.Events);
    }

    [Fact]
    public void Switching_off_lets_go_of_a_held_mouse_button()
    {
        SwitchOn();
        HoldFor(PadButtons.A, Poll);

        _mode.Request(false);
        HoldFor(PadButtons.A, Poll);

        Assert.Equal(["LeftButton down", "LeftButton up"], _output.Events);
    }

    [Fact]
    public void The_keys_follow_the_buttons()
    {
        SwitchOn();

        Tap(PadButtons.B);
        Tap(PadButtons.RightShoulder);
        Tap(PadButtons.LeftShoulder);

        Assert.Equal(
            ["Escape down", "Escape up", "Tab down", "Tab up", "Shift down", "Tab down", "Tab up", "Shift up"],
            _output.Events);
    }

    [Fact]
    public void A_held_dpad_direction_repeats_like_a_keyboard()
    {
        SwitchOn();

        HoldFor(PadButtons.DPadDown, TimeSpan.FromMilliseconds(400) + (Poll * 14));
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["Down down", "Down down", "Down down", "Down up"], _output.Events);
    }

    [Fact]
    public void Menu_alone_presses_enter_on_release_and_view_presses_nothing()
    {
        SwitchOn();

        HoldFor(PadButtons.Menu, TimeSpan.FromSeconds(1));
        Assert.Empty(_output.Events);
        HoldFor(PadButtons.None, Poll);
        Tap(PadButtons.View);

        Assert.Equal(["Enter down", "Enter up"], _output.Events);
    }

    [Fact]
    public void Back_and_start_to_leave_the_mode_press_no_enter()
    {
        SwitchOn();
        HoldFor(PadButtons.Menu, TimeSpan.FromMilliseconds(500));
        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime + Poll);
        HoldFor(PadButtons.None, Poll);

        Assert.Empty(_output.Events);
        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void The_left_stick_moves_the_pointer_and_the_right_one_the_wheel()
    {
        SwitchOn();

        for (var i = 0; i < 125; i++)
        {
            Update(Pad(0, PadButtons.None, left: new StickPosition(1, 0), right: new StickPosition(0, 1)));
        }

        // One second at full deflection and speed 5: 1,500 pixels, give or take the last poll's rest.
        Assert.InRange(_output.PointerX, 1495, 1500);
        Assert.Equal(0, _output.PointerY);

        // Twelve notches a second, upwards.
        Assert.InRange(_output.WheelVertical, 1410, 1440);
    }

    [Fact]
    public void L3_tapped_makes_the_pointer_slow_until_tapped_again()
    {
        SwitchOn();
        Tap(PadButtons.LeftStickClick);
        _output.Events.Clear();

        MoveRightFor(TimeSpan.FromSeconds(1));
        Assert.InRange(_output.PointerX, 495, 500);

        Tap(PadButtons.LeftStickClick);
        _output.PointerX = 0;
        MoveRightFor(TimeSpan.FromSeconds(1));
        Assert.InRange(_output.PointerX, 1495, 1500);
    }

    [Fact]
    public void L3_clicked_briefly_while_pointing_toggles_the_slow_pointer()
    {
        SwitchOn();
        MoveRightFor(TimeSpan.FromMilliseconds(200));
        for (var i = 0; i < 20; i++)
        {
            Update(Pad(0, PadButtons.LeftStickClick, left: new StickPosition(1, 0)));
        }

        _output.PointerX = 0;
        MoveRightFor(TimeSpan.FromSeconds(1));
        Assert.InRange(_output.PointerX, 495, 500);
    }

    [Fact]
    public void L3_held_while_pointing_is_slow_only_while_held()
    {
        SwitchOn();

        for (var i = 0; i < 125; i++)
        {
            Update(Pad(0, PadButtons.LeftStickClick, left: new StickPosition(1, 0)));
        }

        Assert.InRange(_output.PointerX, 495, 500);

        Update();
        _output.PointerX = 0;
        MoveRightFor(TimeSpan.FromSeconds(1));
        Assert.InRange(_output.PointerX, 1495, 1500);
    }

    [Fact]
    public void Two_controllers_point_together()
    {
        SwitchOn();

        Update(Pad(0, PadButtons.A), Pad(1, PadButtons.None));
        Update(Pad(0, PadButtons.None), Pad(1, PadButtons.A));
        Update(Pad(0, PadButtons.None), Pad(1, PadButtons.None));

        // The left button stays down while either controller holds A.
        Assert.Equal(["LeftButton down", "LeftButton up"], _output.Events);
    }

    [Fact]
    public void Stop_lets_go_of_everything_without_announcing_a_switch()
    {
        SwitchOn();
        HoldFor(PadButtons.A | PadButtons.DPadLeft, Poll);
        _events.Clear();

        _mode.Stop();

        Assert.Equal(["LeftButton down", "Left down", "LeftButton up", "Left up"], _output.Events);
        Assert.Empty(_events);
        Assert.False(_mode.IsOn);
    }

    private void SwitchOn()
    {
        _mode.Request(true);
        Update();
        _output.Events.Clear();
    }

    private void Tap(PadButtons buttons)
    {
        HoldFor(buttons, Poll);
        HoldFor(PadButtons.None, Poll);
    }

    private void MoveRightFor(TimeSpan duration)
    {
        for (var held = TimeSpan.Zero; held < duration; held += Poll)
        {
            Update(Pad(0, PadButtons.None, left: new StickPosition(1, 0)));
        }
    }

    /// <summary>Polls with <paramref name="buttons"/> held on controller 0, every 8 ms, for <paramref name="duration"/>.</summary>
    private void HoldFor(PadButtons buttons, TimeSpan duration)
    {
        var held = TimeSpan.Zero;
        do
        {
            Update(Pad(0, buttons));
            held += Poll;
        }
        while (held < duration);
    }

    private void Update(params GamepadReading[] pads)
    {
        _time.Advance(Poll);
        _mode.Update(pads);
    }

    private static GamepadReading Pad(int slot, PadButtons buttons, StickPosition? left = null, StickPosition? right = null) =>
        new(slot, buttons, left ?? Centre, right ?? Centre);

    private sealed class RecordingOutput : IMouseKeyboardOutput
    {
        public List<string> Events { get; } = [];

        public int PointerX { get; set; }

        public int PointerY { get; set; }

        public int WheelVertical { get; private set; }

        public void MovePointer(int dx, int dy)
        {
            PointerX += dx;
            PointerY += dy;
        }

        public void SetButton(MouseButton button, bool down) => Events.Add($"{button}Button {(down ? "down" : "up")}");

        public void Scroll(int vertical, int horizontal) => WheelVertical += vertical;

        public void SetKey(OutputKey key, bool down) => Events.Add($"{key} {(down ? "down" : "up")}");
    }
}
