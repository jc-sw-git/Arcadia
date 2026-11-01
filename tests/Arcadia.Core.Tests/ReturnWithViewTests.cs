using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class ReturnWithViewTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(8);
    private static readonly StickPosition Centre = new(0, 0);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private ReturnWithViewOptions _options = new(Enabled: true, TimeSpan.FromSeconds(1));
    private bool _arcadiaInFront;
    private readonly ReturnWithView _return;

    public ReturnWithViewTests()
    {
        _return = new ReturnWithView(() => _options, () => _arcadiaInFront, _time);
        _return.HoldStarted += rest => _events.Add(FormattableString.Invariant($"hold {rest.TotalSeconds:0.0}"));
        _return.HoldCancelled += () => _events.Add("cancelled");
        _return.Requested += () => _events.Add("arcadia");
    }

    [Fact]
    public void View_held_for_the_hold_time_brings_arcadia_back_once_with_feedback_from_0_2_s()
    {
        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(150));
        Assert.Empty(_events);

        HoldFor(PadButtons.View, TimeSpan.FromSeconds(3));
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["hold 0.8", "arcadia"], _events);
    }

    [Fact]
    public void View_let_go_early_cancels()
    {
        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(600));
        HoldFor(PadButtons.None, Poll);

        Assert.Equal(["hold 0.8", "cancelled"], _events);
    }

    [Fact]
    public void Menu_joining_makes_it_back_and_start_and_nothing_happens_until_view_is_let_go()
    {
        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(500));
        HoldFor(PadButtons.View | PadButtons.Menu, TimeSpan.FromSeconds(1));
        HoldFor(PadButtons.View, TimeSpan.FromSeconds(2));

        Assert.Equal(["hold 0.8", "cancelled"], _events);
    }

    [Fact]
    public void A_tap_does_nothing_so_it_can_move_the_on_screen_keyboard()
    {
        Tap(PadButtons.View);
        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(150));
        HoldFor(PadButtons.None, Poll);

        Assert.Empty(_events);
    }

    [Fact]
    public void Nothing_happens_while_arcadia_is_in_front_or_when_switched_off()
    {
        _arcadiaInFront = true;
        HoldFor(PadButtons.View, TimeSpan.FromSeconds(2));
        HoldFor(PadButtons.None, Poll);

        _arcadiaInFront = false;
        _options = _options with { Enabled = false };
        HoldFor(PadButtons.View, TimeSpan.FromSeconds(2));
        HoldFor(PadButtons.None, Poll);

        Assert.Empty(_events);
    }

    [Fact]
    public void Arcadia_coming_to_the_front_during_the_hold_cancels_it()
    {
        HoldFor(PadButtons.View, TimeSpan.FromMilliseconds(500));
        _arcadiaInFront = true;
        HoldFor(PadButtons.View, TimeSpan.FromSeconds(2));

        Assert.Equal(["hold 0.8", "cancelled"], _events);
    }

    [Fact]
    public void View_on_any_controller_counts()
    {
        for (var held = TimeSpan.Zero; held <= TimeSpan.FromSeconds(1); held += Poll)
        {
            Update(Pad(0, PadButtons.A), Pad(3, PadButtons.View));
        }

        Assert.Equal(["hold 0.8", "arcadia"], _events);
    }

    private void Tap(PadButtons buttons)
    {
        HoldFor(buttons, Poll);
        HoldFor(PadButtons.None, Poll);
    }

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
        _return.Update(pads);
    }

    private static GamepadReading Pad(int slot, PadButtons buttons) => new(slot, buttons, Centre, Centre);
}
