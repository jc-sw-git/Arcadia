using System.Diagnostics;
using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>
/// The steps of a start, in milliseconds since the process started (#637): the budget in <c>CLAUDE.md</c> is the whole way to
/// a usable library, so one log line names where that time goes, from the runtime to the first frame and the library.
/// </summary>
public static class StartTimeline
{
    private static readonly Lock Gate = new();
    private static readonly List<(string Step, long At)> Marks = [];
    private static DateTime? _processStart;
    private static bool _written;

    /// <summary>Notes that <paramref name="step"/> is done now; once the line is written, later steps are ignored.</summary>
    public static void Mark(string step)
    {
        var at = SinceProcessStart();
        lock (Gate)
        {
            if (!_written)
            {
                Marks.Add((step, at));
            }
        }
    }

    /// <summary>Writes the line once, with <paramref name="step"/> as its last step: the start is over.</summary>
    public static void Finish(string step)
    {
        Mark(step);
        string line;
        lock (Gate)
        {
            if (_written)
            {
                return;
            }

            _written = true;
            line = Format(Marks);
        }

        AppLog.Write($"Start: {line}"); // not a UI text (log)
    }

    /// <summary>"Avalonia 300 ms · Fenster offen 900 ms · …", in the order the steps happened.</summary>
    internal static string Format(IEnumerable<(string Step, long At)> marks) =>
        string.Join(" · ", marks.OrderBy(m => m.At).Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Step} {m.At} ms")));

    private static long SinceProcessStart()
    {
        if (_processStart is null)
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                _processStart = process.StartTime;
            }
            catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                // Without the start time the steps are still in order, counted from the first one.
                _processStart = DateTime.Now;
            }
        }

        return (long)(DateTime.Now - _processStart.Value).TotalMilliseconds;
    }
}
