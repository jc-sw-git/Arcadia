using System.Diagnostics;
using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>
/// The steps of a start, in milliseconds since the process started (#637): the budget in <c>CLAUDE.md</c> is the whole way to
/// a usable library, so one log line names where that time goes, from the runtime to the first frame and the library.
/// </summary>
public static class StartTimeline
{
    private static readonly Timeline Start = new(SinceProcessStart, line => AppLog.Write($"Start: {line}")); // not a UI text (log)
    private static DateTime? _processStart;

    /// <summary>Notes that <paramref name="step"/> is done now; once the line is written, later steps are ignored.</summary>
    public static void Mark(string step) => Start.Mark(step);

    /// <summary>
    /// Notes <paramref name="step"/>, the end of the start; the line is written once <paramref name="after"/> is done too, as
    /// the library can come in the very frame the first one is (#637).
    /// </summary>
    public static void Finish(string step, string after) => Start.Finish(step, after);

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

    /// <summary>The steps noted with <paramref name="clock"/>'s time; <paramref name="write"/> gets the line once, when the start ended.</summary>
    internal sealed class Timeline(Func<long> clock, Action<string> write)
    {
        private readonly Lock _gate = new();
        private readonly List<(string Step, long At)> _marks = [];
        private (string Step, string After)? _end;
        private bool _written;

        public void Mark(string step)
        {
            var at = clock();
            string? line;
            lock (_gate)
            {
                if (_written)
                {
                    return;
                }

                _marks.Add((step, at));
                line = TakeLineIfEnded();
            }

            if (line is not null)
            {
                write(line);
            }
        }

        public void Finish(string step, string after)
        {
            lock (_gate)
            {
                _end ??= (step, after);
            }

            Mark(step);
        }

        private string? TakeLineIfEnded()
        {
            if (_end is not { } end || !_marks.Any(m => m.Step == end.Step) || !_marks.Any(m => m.Step == end.After))
            {
                return null;
            }

            _written = true;
            return Format(_marks);
        }
    }
}
