using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>The buttons while the mouse mode's on-screen keyboard is open (#110).</summary>
public sealed class MouseModeKeyboardTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(8);
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReturnHoldTime = TimeSpan.FromSeconds(1);
    private static readonly StickPosition Centre = new(0, 0);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingOutput _output = new();
    private readonly List<string> _keyboard = [];
    private MouseModeOptions _options = new(Enabled: true, HoldTime, PointerSpeed: 5, ReturnHoldTime: ReturnHoldTime);
    private readonly MouseMode _mode;

    public MouseModeKeyboardTests()
    {
        _mode = new MouseMode(_output, () => _options, _time);
        _mode.KeyboardOpened += () => _keyboard.Add("opened");
        _mode.KeyboardButtonChanged += (button, down) => _keyboard.Add($"{button} {(down ? "down" : "up")}");
        _mode.Request(true);
        Update();
    }

    [Fact]
    public void Y_opens_the_keyboard_and_types_no_space_on_its_release()
    {
        Tap(PadButtons.Y);

        Assert.True(_mode.IsKeyboardOpen);
        Assert.Equal(["opened"], _keyboard);
        Assert.Empty(_output.Events);
    }

    [Fact]
    public void Arcadia_can_open_the_keyboard_as_Y_would_and_a_request_while_off_is_dropped()
    {
        _mode.OpenKeyboard();
        Update();

        Assert.True(_mode.IsKeyboardOpen);
        Assert.Equal(["opened"], _keyboard);

        // Asked for while the mode goes off: it doesn't open by itself the next time the mode comes on.
        _mode.OpenKeyboard();
        _mode.Request(false);
        Update();
        _mode.Request(true);
        Update();
        Update();
        Assert.False(_mode.IsKeyboardOpen);
    }

    [Fact]
    public void With_the_keyboard_open_the_buttons_are_its_own_and_send_nothing_themselves()
    {
        OpenKeyboard();

        foreach (var button in new[]
        {
            PadButtons.A, PadButtons.X, PadButtons.Y, PadButtons.B, PadButtons.LeftShoulder, PadButtons.RightShoulder,
            PadButtons.RightStickClick, PadButtons.DPadUp, PadButtons.DPadDown, PadButtons.DPadLeft, PadButtons.DPadRight,
        })
        {
            Tap(button);
        }

        Assert.Equal(
            [
                "Type down", "Type up", "Erase down", "Erase up", "Space down", "Space up", "Hide down", "Hide up",
                "Shift down", "Shift up", "AltGr down", "AltGr up", "Layout down", "Layout up",
                "Up down", "Up up", "Down down", "Down up", "Left down", "Left up", "Right down", "Right up",
            ],
            _keyboard);
        Assert.Empty(_output.Events);
    }

    [Fact]
    public void The_triggers_and_the_sticks_stay_the_mouse()
    {
        OpenKeyboard();

        Tap(PadButtons.RightTrigger);
        Tap(PadButtons.LeftTrigger);
        for (var i = 0; i < 10; i++)
        {
            Update(Pad(PadButtons.None, left: new StickPosition(1, 0), right: new StickPosition(0, 1)));
        }

        Assert.Equal(["LeftButton down", "LeftButton up", "RightButton down", "RightButton up"], _output.Events);
        Assert.True(_output.PointerX > 0);
        Assert.True(_output.WheelVertical > 0);
        Assert.Empty(_keyboard);
    }

    [Fact]
    public void Menu_alone_is_the_keyboards_enter_on_release()
    {
        OpenKeyboard();

        HoldFor(PadButtons.Menu, TimeSpan.FromMilliseconds(500));
        Assert.Empty(_keyboard);
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["Enter down", "Enter up"], _keyboard);
        Assert.Empty(_output.Events);
    }

    [Fact]
    public void View_tapped_docks_and_held_for_the_way_back_to_arcadia_does_not()
    {
        OpenKeyboard();

        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(600));
        HoldFor(PadButtons.None, Poll);
        HoldFor(PadButtons.View, ReturnHoldTime + Poll);
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["Dock down", "Dock up"], _keyboard);
    }

    [Fact]
    public void Without_the_way_back_to_arcadia_view_docks_however_long_it_was_held()
    {
        _options = _options with { ReturnHoldTime = null };
        OpenKeyboard();

        HoldFor(PadButtons.View, TimeSpan.FromSeconds(4));
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["Dock down", "Dock up"], _keyboard);
    }

    [Fact]
    public void Back_and_start_end_the_mouse_mode_and_with_it_the_keyboard_without_enter_or_dock()
    {
        OpenKeyboard();

        HoldFor(PadButtons.View | PadButtons.Menu, HoldTime + Poll);
        HoldFor(PadButtons.None, Poll);

        Assert.False(_mode.IsOn);
        Assert.False(_mode.IsKeyboardOpen);
        Assert.Empty(_keyboard);
    }

    [Fact]
    public void Closed_the_buttons_are_the_mouses_again_but_one_held_across_counts_only_once_let_go()
    {
        OpenKeyboard();
        HoldFor(PadButtons.A, Poll);
        _keyboard.Clear();

        _mode.CloseKeyboard();
        HoldFor(PadButtons.A, Poll * 3);
        Assert.Empty(_output.Events);
        HoldFor(PadButtons.None, Poll);
        Tap(PadButtons.A);
        Tap(PadButtons.B);

        Assert.False(_mode.IsKeyboardOpen);
        Assert.Empty(_keyboard);
        Assert.Equal(["LeftButton down", "LeftButton up", "Escape down", "Escape up"], _output.Events);
    }

    [Fact]
    public void Opening_lets_go_of_an_arrow_the_mouse_mode_held()
    {
        HoldFor(PadButtons.DPadDown, Poll);
        HoldFor(PadButtons.DPadDown | PadButtons.Y, Poll);
        HoldFor(PadButtons.DPadDown, Poll * 3);
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["Down down", "Down up"], _output.Events);
        Assert.Equal(["opened"], _keyboard);
    }

    [Fact]
    public void The_mouse_mode_ending_closes_the_keyboard_and_it_starts_closed_next_time()
    {
        OpenKeyboard();

        _mode.Request(false);
        Update();
        _mode.Request(true);
        Update();
        Tap(PadButtons.B);

        Assert.False(_mode.IsKeyboardOpen);
        Assert.Equal(["Escape down", "Escape up"], _output.Events);
    }

    private void OpenKeyboard()
    {
        Tap(PadButtons.Y);
        _keyboard.Clear();
        _output.Events.Clear();
    }

    private void Tap(PadButtons buttons)
    {
        HoldFor(buttons, Poll);
        HoldFor(PadButtons.None, Poll);
    }

    /// <summary>Polls with <paramref name="buttons"/> held on controller 0, every 8 ms, for <paramref name="duration"/>.</summary>
    private void HoldFor(PadButtons buttons, TimeSpan duration)
    {
        var held = TimeSpan.Zero;
        do
        {
            Update(Pad(buttons));
            held += Poll;
        }
        while (held < duration);
    }

    private void Update(params GamepadReading[] pads)
    {
        _time.Advance(Poll);
        _mode.Update(pads.Length == 0 ? [Pad(PadButtons.None)] : pads);
    }

    private static GamepadReading Pad(PadButtons buttons, StickPosition? left = null, StickPosition? right = null) =>
        new(0, buttons, left ?? Centre, right ?? Centre);
}
