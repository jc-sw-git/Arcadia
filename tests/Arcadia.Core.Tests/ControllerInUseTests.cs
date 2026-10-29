using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>Which controller's symbols the hints show (#215): the last one pressed or pushed, the first connected before that.</summary>
public sealed class ControllerInUseTests
{
    private static readonly StickPosition Noise = new(0.2, -0.25);
    private static readonly StickPosition Pushed = new(0.9, 0);

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void At_the_start_the_first_connected_controller_counts_and_without_one_none()
    {
        var withPads = new ControllerInUse(_time);
        Assert.True(withPads.Note([Pad(4, PadType.SwitchPro), Pad(2, PadType.PS5)]));
        Assert.Equal(PadType.PS5, withPads.Current);

        var withoutPads = new ControllerInUse(_time);
        Assert.True(withoutPads.Note([]), "the first poll decides, also for none");
        Assert.Null(withoutPads.Current);
        Assert.False(withoutPads.Note([]));
    }

    [Fact]
    public void A_controller_SDL_reports_shortly_after_the_start_still_counts_as_there_at_the_start()
    {
        var inUse = new ControllerInUse(_time);
        inUse.Note([]);
        _time.Advance(ControllerInUse.StartGrace / 2);

        Assert.True(inUse.Note([Pad(1, PadType.PS4)]));
        Assert.Equal(PadType.PS4, inUse.Current);
    }

    [Fact]
    public void Each_button_press_takes_the_symbols_to_its_controller()
    {
        var inUse = Started(Pad(1, PadType.XboxOne), Pad(2, PadType.PS5));

        Assert.True(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.PS5, PadButtons.A)]));
        Assert.Equal(PadType.PS5, inUse.Current);
        Assert.False(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.PS5, PadButtons.A)]), "a held button is no new press");

        Assert.True(inUse.Note([Pad(1, PadType.XboxOne, PadButtons.DPadDown), Pad(2, PadType.PS5, PadButtons.A)]));
        Assert.Equal(PadType.XboxOne, inUse.Current);
    }

    [Fact]
    public void Stick_noise_switches_nothing_a_push_past_the_dead_zone_does()
    {
        var inUse = Started(Pad(1, PadType.XboxOne), Pad(2, PadType.SwitchPro));

        Assert.False(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.SwitchPro, left: Noise, right: Noise)]));
        Assert.Equal(PadType.XboxOne, inUse.Current);

        Assert.True(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.SwitchPro, right: Pushed)]));
        Assert.Equal(PadType.SwitchPro, inUse.Current);

        // The stick still held while the other controller is pressed: it doesn't take the symbols back.
        inUse.Note([Pad(1, PadType.XboxOne, PadButtons.B), Pad(2, PadType.SwitchPro, right: Pushed)]);
        Assert.False(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.SwitchPro, right: Pushed)]));
        Assert.Equal(PadType.XboxOne, inUse.Current);
    }

    [Fact]
    public void A_controller_plugged_in_later_changes_nothing_until_it_is_used()
    {
        var inUse = Started(Pad(1, PadType.XboxOne));

        Assert.False(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.PS5, PadButtons.A)]), "its first reading is no press");
        Assert.Equal(PadType.XboxOne, inUse.Current);

        var none = Started();
        Assert.False(none.Note([Pad(3, PadType.PS5)]));
        Assert.Null(none.Current);
        Assert.True(none.Note([Pad(3, PadType.PS5, PadButtons.Y)]));
        Assert.Equal(PadType.PS5, none.Current);
    }

    [Fact]
    public void When_the_controller_in_use_goes_the_first_connected_of_the_others_takes_over_and_then_none()
    {
        var inUse = Started(Pad(1, PadType.XboxOne), Pad(2, PadType.PS5), Pad(3, PadType.SwitchPro));
        inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.PS5), Pad(3, PadType.SwitchPro, PadButtons.X)]);

        Assert.True(inUse.Note([Pad(1, PadType.XboxOne), Pad(2, PadType.PS5)]));
        Assert.Equal(PadType.XboxOne, inUse.Current);

        Assert.True(inUse.Note([Pad(2, PadType.PS5)]));
        Assert.Equal(PadType.PS5, inUse.Current);

        Assert.True(inUse.Note([]));
        Assert.Null(inUse.Current);
    }

    [Fact]
    public void A_controller_other_than_the_one_in_use_going_changes_nothing()
    {
        var inUse = Started(Pad(1, PadType.XboxOne), Pad(2, PadType.PS5));

        Assert.False(inUse.Note([Pad(1, PadType.XboxOne)]));
        Assert.Equal(PadType.XboxOne, inUse.Current);
    }

    [Fact]
    public void Lost_controllers_or_none_on_this_system_leave_none()
    {
        var inUse = Started(Pad(1, PadType.PS5));
        Assert.True(inUse.Drop());
        Assert.Null(inUse.Current);
        Assert.False(inUse.Drop());

        Assert.True(new ControllerInUse(_time).Drop(), "with no poll at all, dropping decides it");
    }

    private ControllerInUse Started(params GamepadReading[] pads)
    {
        var inUse = new ControllerInUse(_time);
        inUse.Note(pads);
        _time.Advance(ControllerInUse.StartGrace);
        inUse.Note(pads);
        return inUse;
    }

    private static GamepadReading Pad(int id, PadType type, PadButtons buttons = PadButtons.None, StickPosition left = default, StickPosition right = default) =>
        new(id, buttons, left, right) { Type = type };
}
