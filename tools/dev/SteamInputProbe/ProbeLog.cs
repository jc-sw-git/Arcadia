using System.Diagnostics;

namespace SteamInputProbe;

/// <summary>Writes every line with the milliseconds since the probe started, to the console and a log file.</summary>
internal sealed class ProbeLog : IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly StreamWriter _file;
    private readonly Lock _lock = new();

    public ProbeLog(string path)
    {
        Path = path;
        _file = new StreamWriter(path, append: false) { AutoFlush = true };
    }

    public string Path { get; }

    public long ElapsedMs => _clock.ElapsedMilliseconds;

    public void Line(string text)
    {
        // The QPC ticks (Stopwatch.GetTimestamp) let spike #239 line up this log with steamproxy239's, which reads the same clock.
        var line = $"{_clock.ElapsedMilliseconds,7} ms  q{Stopwatch.GetTimestamp()}  {text}";
        lock (_lock)
        {
            Console.WriteLine(line);
            _file.WriteLine(line);
        }
    }

    public void Dispose() => _file.Dispose();
}
