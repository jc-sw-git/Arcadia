namespace Arcadia.Input;

/// <summary>
/// A button that does one thing when tapped and another when held. A tap acts on release; a hold
/// acts as soon as the hold time has passed, without waiting for the release. Letting go in
/// between cancels, so a press that turned into a hold by mistake can still be taken back. From
/// the feedback delay on, the UI shows that a hold is building up.
/// </summary>
/// <remarks>
/// Call it from one thread (the UI thread). Its timers fire on the thread pool and reach that
/// thread through <c>post</c>, so all state changes and events happen there.
/// </remarks>
public sealed class HoldGesture(TimeSpan feedbackDelay, TimeSpan holdTime, TimeProvider time, Action<Action> post) : IDisposable
{
    private enum Phase
    {
        Idle,

        /// <summary>Pressed; letting go now is a tap.</summary>
        Pressed,

        /// <summary>The feedback shows; letting go now cancels.</summary>
        Building,

        /// <summary>The hold fired; the release that follows does nothing.</summary>
        Held,
    }

    private Phase _phase;

    // Counts presses, so a timer of an earlier press that fires late is ignored.
    private int _press;
    private ITimer? _feedbackTimer;
    private ITimer? _holdTimer;

    /// <summary>Released before the feedback started.</summary>
    public event Action? Tapped;

    /// <summary>Held for the whole hold time.</summary>
    public event Action? Held;

    /// <summary>True when the hold feedback should start (filling over the rest of the hold time), false when it should go.</summary>
    public event Action<bool>? FeedbackChanged;

    public void Press()
    {
        // A key held down repeats its press; only the first one counts.
        if (_phase is Phase.Pressed or Phase.Building)
        {
            return;
        }

        Reset();
        _phase = Phase.Pressed;
        var press = _press;
        _feedbackTimer = time.CreateTimer(_ => post(() => StartFeedback(press)), null, feedbackDelay, Timeout.InfiniteTimeSpan);
        _holdTimer = time.CreateTimer(_ => post(() => CompleteHold(press)), null, holdTime, Timeout.InfiniteTimeSpan);
    }

    public void Release()
    {
        var phase = _phase;
        Reset();
        if (phase == Phase.Pressed)
        {
            Tapped?.Invoke();
        }
    }

    /// <summary>Drops a running press without acting on it, e.g. when another button is pressed or the window loses focus.</summary>
    public void Cancel() => Reset();

    public void Dispose() => Reset();

    private void StartFeedback(int press)
    {
        if (press != _press || _phase != Phase.Pressed)
        {
            return;
        }

        _phase = Phase.Building;
        FeedbackChanged?.Invoke(true);
    }

    private void CompleteHold(int press)
    {
        if (press != _press || _phase is not (Phase.Pressed or Phase.Building))
        {
            return;
        }

        var showedFeedback = _phase == Phase.Building;
        StopTimers();
        _phase = Phase.Held;
        if (showedFeedback)
        {
            FeedbackChanged?.Invoke(false);
        }

        Held?.Invoke();
    }

    private void Reset()
    {
        var showedFeedback = _phase == Phase.Building;
        StopTimers();
        _phase = Phase.Idle;
        _press++;
        if (showedFeedback)
        {
            FeedbackChanged?.Invoke(false);
        }
    }

    private void StopTimers()
    {
        _feedbackTimer?.Dispose();
        _holdTimer?.Dispose();
        _feedbackTimer = _holdTimer = null;
    }
}
