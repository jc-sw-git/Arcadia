using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class HoldGestureTests
{
    private static readonly TimeSpan FeedbackDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private readonly HoldGesture _gesture;

    public HoldGestureTests()
    {
        _gesture = new HoldGesture(FeedbackDelay, HoldTime, _time, action => action());
        _gesture.Tapped += () => _events.Add("tap");
        _gesture.Held += () => _events.Add("held");
        _gesture.FeedbackChanged += on => _events.Add(on ? "feedback on" : "feedback off");
    }

    [Fact]
    public void ReleaseBeforeTheFeedbackIsATap()
    {
        _gesture.Press();
        _time.Advance(TimeSpan.FromMilliseconds(150));
        _gesture.Release();
        _time.Advance(HoldTime);

        Assert.Equal(["tap"], _events);
    }

    [Fact]
    public void ReleaseWhileTheFeedbackShowsCancels()
    {
        _gesture.Press();
        _time.Advance(TimeSpan.FromMilliseconds(600));
        _gesture.Release();
        _time.Advance(HoldTime);

        Assert.Equal(["feedback on", "feedback off"], _events);
    }

    [Fact]
    public void HoldingFiresOnceWithoutWaitingForTheRelease()
    {
        _gesture.Press();
        _time.Advance(HoldTime);

        Assert.Equal(["feedback on", "feedback off", "held"], _events);

        _gesture.Release();
        Assert.Equal(["feedback on", "feedback off", "held"], _events);
    }

    [Fact]
    public void RepeatedPressesOfAHeldKeyDontRestartTheHold()
    {
        _gesture.Press();
        for (var i = 0; i < 9; i++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(100));
            _gesture.Press();
        }

        _time.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(["feedback on", "feedback off", "held"], _events);
    }

    [Fact]
    public void CancelDropsThePressAndItsTimers()
    {
        _gesture.Press();
        _time.Advance(TimeSpan.FromMilliseconds(300));
        _gesture.Cancel();
        _time.Advance(HoldTime);
        _gesture.Release();

        Assert.Equal(["feedback on", "feedback off"], _events);
    }

    [Fact]
    public void ATimerThatFiresLateBelongsToItsOwnPress()
    {
        // Timer callbacks are posted to the UI thread and may run after a release and a new press.
        var posted = new List<Action>();
        var gesture = new HoldGesture(FeedbackDelay, HoldTime, _time, posted.Add);
        var held = 0;
        gesture.Held += () => held++;

        gesture.Press();
        _time.Advance(HoldTime);
        gesture.Release();
        gesture.Press();
        foreach (var action in posted.ToList())
        {
            action();
        }

        Assert.Equal(0, held);
    }

    [Fact]
    public void APressAfterAHoldWithoutReleaseStartsOver()
    {
        // The release can get lost, e.g. when the key goes up in another window.
        _gesture.Press();
        _time.Advance(HoldTime);
        _gesture.Press();
        _gesture.Release();

        Assert.Equal(["feedback on", "feedback off", "held", "tap"], _events);
    }

    [Fact]
    public void ReleaseWithoutPressDoesNothing()
    {
        _gesture.Release();

        Assert.Empty(_events);
    }
}
