using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Services;

/// <summary>
/// How long a press takes to show (#229): from the moment it arrived to the first frame drawn after it was handled, one line
/// per press in arcadia.log. It stays on in every build, so the owner can read the numbers of his own library and PC.
/// </summary>
/// <remarks>
/// "Shown" is when the UI thread has laid out and handed the frame to the renderer; the render thread and the screen add a
/// few milliseconds more that no app can see. UI thread only, apart from the timestamp a press carries in.
/// </remarks>
public sealed class InputLatency
{
    private readonly Action<Action> _afterNextFrame;
    private readonly Action<string> _log;

    /// <param name="afterNextFrame">Runs its action once the next frame is laid out and handed to the renderer.</param>
    /// <param name="log">Where the lines go.</param>
    public InputLatency(Action<Action> afterNextFrame, Action<string> log)
    {
        _afterNextFrame = afterNextFrame;
        _log = log;
    }

    /// <summary>Measures in <paramref name="window"/> and writes to arcadia.log.</summary>
    public static InputLatency For(TopLevel window) => new(
        // The animation frame callback runs before the frame's layout and render pass, which is one dispatcher job of
        // Render priority; a Background job posted from it runs once that pass is done.
        shown => window.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(shown, DispatcherPriority.Background)),
        AppLog.Write);

    /// <summary>The current time, as a press that arrives now carries it.</summary>
    public static long Now() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Handles the press <paramref name="what"/> that arrived at <paramref name="pressedAt"/> (<see cref="Now"/>) and logs, after
    /// the next frame, "Bild: Right nach 14.2 ms (Warten 0.3, Verarbeitung 1.1)": waiting is the time before the UI thread got
    /// to it, handling the view models' share; the rest is binding, layout and render.
    /// </summary>
    public void Measure(string what, long pressedAt, Action handle)
    {
        var handlingStarted = Stopwatch.GetTimestamp();
        handle();
        var handled = Stopwatch.GetTimestamp();
        _afterNextFrame(() => _log(string.Create(
            CultureInfo.InvariantCulture,
            $"Bild: {what} nach {Milliseconds(pressedAt, Stopwatch.GetTimestamp()):0.0} ms (Warten {Milliseconds(pressedAt, handlingStarted):0.0}, Verarbeitung {Milliseconds(handlingStarted, handled):0.0})")));
    }

    private static double Milliseconds(long from, long to) => Stopwatch.GetElapsedTime(from, to).TotalMilliseconds;
}
