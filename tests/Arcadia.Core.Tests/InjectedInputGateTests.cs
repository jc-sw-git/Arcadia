using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class InjectedInputGateTests
{
    private const int Alt = 0x12;
    private const int Tab = 0x09;

    private readonly InjectedInputGate _gate = new();
    private bool _inUse;

    [Fact]
    public void Foreign_input_while_the_controller_is_in_use_is_dropped_with_its_release()
    {
        _inUse = true;
        Assert.True(Press(Alt, down: true));
        Assert.True(Press(Tab, down: true));
        Assert.True(Press(Tab, down: false));

        // Released after the controller was let go: still dropped, so nothing is left down.
        _inUse = false;
        Assert.True(Press(Alt, down: false));
        Assert.True(_gate.Motion(foreign: true, () => true));
        Assert.Equal(5, _gate.TakeDroppedCount());
        Assert.Equal(0, _gate.TakeDroppedCount());
    }

    [Fact]
    public void Real_devices_arcadias_own_input_and_input_without_the_controller_pass()
    {
        _inUse = true;
        Assert.False(_gate.Press(Alt, down: true, foreign: false, () => _inUse));
        Assert.False(_gate.Press(Alt, down: false, foreign: false, () => _inUse));
        Assert.False(_gate.Motion(foreign: false, () => _inUse));

        _inUse = false;
        Assert.False(Press(Tab, down: true));
        Assert.False(_gate.Motion(foreign: true, () => _inUse));
    }

    [Fact]
    public void A_key_pressed_before_the_controller_was_touched_is_let_go_normally()
    {
        Assert.False(Press(Alt, down: true));

        _inUse = true;
        Assert.False(Press(Alt, down: false));
    }

    [Fact]
    public void A_dropped_key_stays_dropped_while_it_repeats()
    {
        _inUse = true;
        Assert.True(Press(Tab, down: true));

        _inUse = false;
        Assert.True(Press(Tab, down: true));
        Assert.True(Press(Tab, down: false));
        Assert.False(Press(Tab, down: true));
    }

    [Fact]
    public void The_controller_counts_as_in_use_while_pushed_and_a_moment_after()
    {
        var time = new FakeTimeProvider();
        var activity = new ControllerActivity(time);
        var centre = new StickPosition(0, 0);

        activity.Note([new GamepadReading(0, PadButtons.None, new StickPosition(0.1, -0.2), centre)]);
        Assert.False(activity.IsInUse());

        activity.Note([new GamepadReading(0, PadButtons.None, centre, new StickPosition(0, 0.9))]);
        Assert.True(activity.IsInUse());

        activity.Note([new GamepadReading(0, PadButtons.None, centre, centre), new GamepadReading(2, PadButtons.None, centre, centre)]);
        time.Advance(ControllerActivity.Grace);
        Assert.True(activity.IsInUse());
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(activity.IsInUse());
        Assert.Equal(2, activity.ConnectedPads);

        activity.Note([new GamepadReading(2, PadButtons.View, centre, centre)]);
        Assert.True(activity.IsInUse());
    }

    [Fact]
    public void The_combination_counts_while_view_and_menu_are_held_together_and_a_moment_after()
    {
        var time = new FakeTimeProvider();
        var activity = new ControllerActivity(time);
        var centre = new StickPosition(0, 0);

        activity.Note([new GamepadReading(0, PadButtons.View | PadButtons.A, centre, centre)]);
        Assert.False(activity.IsComboHeld());

        activity.Note([new GamepadReading(0, PadButtons.None, centre, centre), new GamepadReading(1, PadButtons.View | PadButtons.Menu, centre, centre)]);
        Assert.True(activity.IsComboHeld());

        activity.Note([new GamepadReading(1, PadButtons.Menu, centre, centre)]);
        time.Advance(ControllerActivity.Grace);
        Assert.True(activity.IsComboHeld());
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(activity.IsComboHeld());
        Assert.True(activity.IsInUse());
    }

    private bool Press(int key, bool down) => _gate.Press(key, down, foreign: true, () => _inUse);
}
