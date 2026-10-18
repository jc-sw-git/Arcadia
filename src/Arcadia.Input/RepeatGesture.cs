namespace Arcadia.Input;

/// <summary>
/// A button that acts once when pressed and, held down, again and again like a held key on a
/// keyboard: after <c>delay</c>, every <c>interval</c>, until it is let go.
/// </summary>
/// <remarks>
/// Call it from one thread (the UI thread). Its timer fires on the thread pool and reaches that
/// thread through <c>post</c>, so <see cref="Repeated"/> is raised there.
/// </remarks>
public sealed class RepeatGesture(TimeSpan delay, TimeSpan interval, TimeProvider time, Action<Action> post) : IDisposable
{
    // Counts presses, so a tick of an earlier press that arrives late is ignored.
    private int _press;
    private ITimer? _timer;

    /// <summary>On the press and on every repeat.</summary>
    public event Action? Repeated;

    public bool IsPressed => _timer is not null;

    public void Press()
    {
        // Pressed twice without a release in between (e.g. a lost release): the running repeat goes on.
        if (IsPressed)
        {
            return;
        }

        var press = ++_press;
        _timer = time.CreateTimer(_ => post(() => Tick(press)), null, delay, interval);
        Repeated?.Invoke();
    }

    public void Release() => Stop();

    public void Dispose() => Stop();

    private void Tick(int press)
    {
        if (press == _press && IsPressed)
        {
            Repeated?.Invoke();
        }
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _press++;
    }
}
