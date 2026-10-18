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
        HoldUntilDone();

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
        var events = new List<string>();
        gesture.Held += () => events.Add("held");
        gesture.FeedbackChanged += on => events.Add(on ? "feedback on" : "feedback off");

        gesture.Press();
        _time.Advance(FeedbackDelay);
        gesture.Release();
        gesture.Press();
        foreach (var action in posted.ToList())
        {
            action();
        }

        // The new press is only just down: no feedback yet.
        Assert.Empty(events);
    }

    [Fact]
    public void TheHoldLastsTheWholeFillTimeEvenWhenTheFeedbackStartsLate()
    {
        // The ring's animation runs for HoldTime - FeedbackDelay from the moment the feedback shows;
        // a busy UI thread shows it late, and the hold must not cut the animation short.
        var posted = new List<Action>();
        var gesture = new HoldGesture(FeedbackDelay, HoldTime, _time, posted.Add);
        var events = new List<string>();
        gesture.Held += () => events.Add("held");
        gesture.FeedbackChanged += on => events.Add(on ? "feedback on" : "feedback off");
        void RunPosted()
        {
            var actions = posted.ToList();
            posted.Clear();
            actions.ForEach(action => action());
        }

        gesture.Press();
        _time.Advance(FeedbackDelay);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        RunPosted();
        _time.Advance(HoldTime - FeedbackDelay - TimeSpan.FromMilliseconds(50));
        RunPosted();

        Assert.Equal(["feedback on"], events);

        _time.Advance(TimeSpan.FromMilliseconds(50));
        RunPosted();

        Assert.Equal(["feedback on", "feedback off", "held"], events);
    }

    [Fact]
    public void APressAfterAHoldWithoutReleaseStartsOver()
    {
        // The release can get lost, e.g. when the key goes up in another window.
        _gesture.Press();
        HoldUntilDone();
        _gesture.Press();
        _gesture.Release();

        Assert.Equal(["feedback on", "feedback off", "held", "tap"], _events);
    }

    // In steps, as the real clock passes: the hold timer starts only when the feedback shows.
    private void HoldUntilDone()
    {
        _time.Advance(FeedbackDelay);
        _time.Advance(HoldTime - FeedbackDelay);
    }

    [Fact]
    public void ReleaseWithoutPressDoesNothing()
    {
        _gesture.Release();

        Assert.Empty(_events);
    }
}
