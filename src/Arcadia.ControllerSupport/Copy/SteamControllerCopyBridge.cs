using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using Arcadia.Input;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>
/// Runs one copy of the puck (#242 part C), free of Windows: the real puck's input reports go through
/// <see cref="SteamControllerCopyFilter.ToSteam"/> to VIIPER's <c>hidproxy</c> stream, and what Steam sends comes back on
/// it and reaches the real puck only if <see cref="SteamControllerCopyFilter.FromSteam"/> lets it. Nothing else talks to
/// the puck for Steam: once <see cref="DisposeAsync"/> began, nothing does.
/// </summary>
/// <remarks>
/// One thread per HID interface reads the puck (a read blocks until the puck sends); a task reads VIIPER's frames and
/// answers its control requests in order, as VIIPER asks them one at a time. Writes to VIIPER share one lock and a time
/// limit (a VIIPER that stops reading breaks the copy rather than hanging it), and calls to the puck on Steam's behalf
/// share another. Arcadia's own SDL also sends feature reports to the puck (lizard mode every 3 s),
/// as it did before #242: a GET_REPORT of Steam's may then read SDL's answer, which Steam already had to live with.
/// </remarks>
public sealed class SteamControllerCopyBridge : IAsyncDisposable
{
    private const int MaxReport = 64;
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(1);

    private readonly IRealPuck _puck;
    private readonly Stream _viiper;
    private readonly SteamControllerCopyFilter _filter;
    private readonly Func<bool> _forwarding;
    private readonly Func<ComboWithholdingOptions> _combos;
    private readonly PuckDescriptors _descriptors;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _send = new();
    private readonly Lock _toPuck = new();
    private readonly Lock _toSteam = new();
    private readonly List<Thread> _readers = [];
    private readonly CopyLatency _latency = new();
    private readonly Dictionary<(byte Interface, byte Id), byte[]> _shown = [];
    private readonly Dictionary<byte, byte> _lastMessage = [];
    private readonly Dictionary<(PuckCall Call, string Error), int> _refusals = [];
    private readonly Lock _refusalsLock = new();
    private readonly TimeSpan _sendTimeout;
    private Timer? _sendWatch;
    private long _sendingSince;
    private Task _frames = Task.CompletedTask;
    private volatile bool _closed;
    private volatile bool _broken;
    private int _brokenLogged;

    /// <param name="forwarding">The copy passes input now (<see cref="SteamControllerCopyRules.CopyPasses"/>); asked per report.</param>
    /// <param name="sendTimeout">How long a write to VIIPER may hang before the copy counts as broken; 1 s by default.</param>
    public SteamControllerCopyBridge(IRealPuck puck, Stream viiper, SteamControllerCopyFilter filter, Func<bool> forwarding,
        Func<ComboWithholdingOptions> combos, PuckDescriptors descriptors, Action<string> log, TimeSpan? sendTimeout = null)
    {
        _sendTimeout = sendTimeout ?? DefaultSendTimeout;
        _puck = puck;
        _viiper = viiper;
        _filter = filter;
        _forwarding = forwarding;
        _combos = combos;
        _descriptors = descriptors;
        _log = log;
    }

    /// <summary>False once the puck or VIIPER's stream broke off.</summary>
    public bool IsConnected => !_broken && !_closed;

    public void Start()
    {
        foreach (var iface in _puck.Interfaces)
        {
            var reader = new Thread(() => ReadPuck(iface)) { IsBackground = true, Name = $"Steam-Controller-Kopie {iface}", Priority = ThreadPriority.AboveNormal };
            _readers.Add(reader);
            reader.Start();
        }

        _frames = Task.Run(ReadViiperAsync);
        var every = _sendTimeout / 4;
        _sendWatch = new Timer(_ => WatchSend(), null, every, every);
    }

    /// <summary>
    /// Stops all forwarding first, then closes the stream and the puck. Throws when a call to the puck on Steam's behalf
    /// still runs after that (or the puck won't close): forwarding isn't confirmed stopped, so Pan's full teardown must
    /// follow before the real puck shows.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_closed)
        {
            return;
        }

        // A call to the puck already under way finishes; none starts after this.
        var idle = _toPuck.TryEnter(CloseWait);
        _closed = true;
        if (idle)
        {
            _toPuck.Exit();
        }
        else
        {
            _log("Steam-Controller-Kopie: ein Aufruf an den Puck hängt beim Beenden");
        }

        await _stop.CancelAsync();
        if (_sendWatch is { } watch)
        {
            await watch.DisposeAsync();
        }

        Quietly(_viiper.Dispose);
        var puckClosed = Quietly(_puck.Dispose);
        try
        {
            await _frames.WaitAsync(CloseWait);
        }
        catch (Exception)
        {
            // The stream is gone; whatever the loop ended with no longer matters.
        }

        foreach (var reader in _readers)
        {
            reader.Join(CloseWait);
        }

        // Closing the puck ends a call that hung in it; if even that didn't, it may still reach the puck.
        var callsEnded = idle || _toPuck.TryEnter(CloseWait);
        if (callsEnded && !idle)
        {
            _toPuck.Exit();
        }

        _log($"Steam-Controller-Kopie beendet; Verzögerung Puck → VIIPER: {_latency.Summary()}");
        if (RefusalSummary() is { } refusals)
        {
            _log($"Steam-Controller-Kopie: vom Puck abgelehnt: {refusals}");
        }

        _stop.Dispose();
        if (!callsEnded || !puckClosed)
        {
            throw new InvalidOperationException(callsEnded
                ? "Der Puck ließ sich nicht schließen; die Kopie ist nicht sicher beendet"
                : "Ein Aufruf an den Puck endet nicht; die Kopie ist nicht sicher beendet");
        }
    }

    private void ReadPuck(byte iface)
    {
        var report = new byte[MaxReport];
        var toSteam = new byte[MaxReport];
        var frame = new byte[HidProxyFrames.HeaderSize + MaxReport];
        try
        {
            while (!_closed)
            {
                var length = _puck.ReadInput(iface, report);
                if (length == 0)
                {
                    return;
                }

                var read = Stopwatch.GetTimestamp();
                int passed;
                lock (_toSteam)
                {
                    // One filter holds the combo state for every interface.
                    passed = _filter.ToSteam(report.AsSpan(0, Math.Min(length, MaxReport)), toSteam, _forwarding(), _combos());
                    Remember(iface, report[0], toSteam.AsSpan(0, passed));
                }

                if (passed == 0)
                {
                    continue;
                }

                var size = HidProxyFrames.Write(frame, HidProxyFrames.Input, iface, toSteam.AsSpan(0, passed));
                Send(frame, size);

                _latency.Add(Stopwatch.GetElapsedTime(read));
                if (_latency.Due())
                {
                    _log($"Steam-Controller-Kopie: Verzögerung Puck → VIIPER {_latency.Summary()}");
                }
            }
        }
        catch (Exception ex) when (!_closed)
        {
            Broke($"Puck (Interface {iface}) nicht mehr lesbar: {ex.Message}");
        }
        catch (Exception)
        {
            // Closed on purpose: the read ended because the puck was closed.
        }
    }

    private async Task ReadViiperAsync()
    {
        try
        {
            while (await HidProxyFrames.ReadAsync(_viiper, _stop.Token) is { } frame)
            {
                switch (frame.Kind)
                {
                    case HidProxyFrames.Output:
                        Output(frame.Interface, frame.Payload);
                        break;
                    case HidProxyFrames.Control:
                        Control(frame.Interface, frame.Payload);
                        break;
                    default:
                        throw new InvalidDataException($"VIIPER schickt einen unbekannten Frame 0x{frame.Kind:X2}");
                }
            }

            Broke("VIIPER hat die Verbindung beendet");
        }
        catch (Exception ex) when (!_closed)
        {
            Broke($"Verbindung zu VIIPER abgebrochen: {ex.Message}");
        }
        catch (Exception)
        {
            // Closed on purpose.
        }
    }

    /// <summary>An interrupt OUT transfer: an output report for a HID interface; anything for another interface is dropped.</summary>
    private void Output(byte iface, byte[] report)
    {
        var kind = _descriptors.IsHid(iface) ? SteamReportKind.Output : SteamReportKind.Other;
        if (_filter.FromSteam(kind, report, _forwarding()))
        {
            ToPuck(() => _puck.WriteOutput(iface, report), OutputCall(iface, report));
        }
    }

    private void Control(byte iface, byte[] payload)
    {
        if (payload.Length < 2 + SetupPacket.Size)
        {
            throw new InvalidDataException($"VIIPER schickt eine Steueranfrage mit {payload.Length} Bytes");
        }

        var sequence = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var setup = SetupPacket.Read(payload.AsSpan(2));
        var data = payload.AsSpan(2 + SetupPacket.Size).ToArray();
        var (kind, type) = SteamControlRequests.Classify(setup, _descriptors.IsHid(iface), IsCdc(iface));
        var passes = _filter.FromSteam(kind, data, _forwarding());
        if (kind == SteamReportKind.SetFeature && passes && data.Length >= 2)
        {
            // The GET_REPORT that follows reads the puck's answer to it: named in the log if that fails. A dropped
            // message never reached the puck, so it can't be what the puck answers.
            _lastMessage[iface] = data[1];
        }

        switch (kind)
        {
            case SteamReportKind.GetReport:
                Reply(iface, sequence, GetReport(iface, type, (byte)setup.Value, setup.Length));
                break;
            case SteamReportKind.SetFeature when passes:
                Reply(iface, sequence, ToPuck(() => _puck.SetFeature(iface, data), FeatureCall(iface, data)) ? [] : null);
                break;
            case SteamReportKind.Output when passes:
                Reply(iface, sequence, ToPuck(() => _puck.WriteOutput(iface, data), OutputCall(iface, data)) ? [] : null);
                break;
            case SteamReportKind.SetFeature or SteamReportKind.Output or SteamReportKind.Housekeeping:
                // Dropped by the allow list, or Windows' own business: Steam sees it done, as with the real puck.
                Reply(iface, sequence, []);
                break;
            default:
                Reply(iface, sequence, null);
                break;
        }
    }

    private bool IsCdc(byte iface) => _descriptors.InterfaceClasses.TryGetValue(iface, out var cls) && cls is 0x02 or 0x0A;

    /// <summary>
    /// The last input report of this id as Steam got it (after <see cref="SteamControllerCopyFilter.ToSteam"/>), so a
    /// GET_REPORT can't read past the filter what the interrupt path keeps at rest; nothing when none passed lately.
    /// </summary>
    private void Remember(byte iface, byte id, ReadOnlySpan<byte> shown)
    {
        if (shown.IsEmpty)
        {
            _shown.Remove((iface, id));
        }
        else if (_shown.TryGetValue((iface, id), out var last) && last.Length == shown.Length)
        {
            shown.CopyTo(last);
        }
        else
        {
            _shown[(iface, id)] = shown.ToArray();
        }
    }

    /// <returns>The report Steam reads, or null when the puck didn't give one.</returns>
    private byte[]? GetReport(byte iface, HidReportType type, byte id, ushort length)
    {
        if (type == HidReportType.Input)
        {
            lock (_toSteam)
            {
                return _shown.TryGetValue((iface, id), out var shown) ? shown[..Math.Min(shown.Length, Math.Max((int)length, 1))] : null;
            }
        }

        var report = new byte[Math.Clamp((int)length, 1, HidProxyFrames.MaxPayload - 16)];
        var read = 0;
        var after = _lastMessage.TryGetValue(iface, out var message) ? $"nach {SteamControllerMessages.NameOfFeature(message)}" : null;
        var call = new PuckCall($"GET_REPORT {type}", iface, id, after);
        return ToPuck(() => read = _puck.GetReport(iface, type, id, report), call) ? report[..Math.Min(read, report.Length)] : null;
    }

    private static PuckCall FeatureCall(byte iface, byte[] data) =>
        new("Feature-Report", iface, data.Length >= 1 ? data[0] : (byte)0, data.Length >= 2 ? SteamControllerMessages.NameOfFeature(data[1]) : null);

    private static PuckCall OutputCall(byte iface, byte[] data) => new("Ausgabe", iface, data.Length >= 1 ? data[0] : (byte)0, null);

    /// <summary>A call to the puck on Steam's behalf; never once the copy is closing. False when it failed.</summary>
    private bool ToPuck(Action call, PuckCall what)
    {
        lock (_toPuck)
        {
            if (_closed)
            {
                return false;
            }

            try
            {
                call();
                return true;
            }
            catch (Exception ex)
            {
                Refused(what, ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Logs the first refusal of each kind and only counts the same one after that (#314): Steam asks the puck's empty
    /// slots every few seconds, which the real puck refuses as it would without the copy, and a line each time would
    /// bury the log. A new request, slot or error still gets its line; <see cref="DisposeAsync"/> logs the counts.
    /// </summary>
    private void Refused(PuckCall call, Exception ex)
    {
        var error = ex is Win32Exception win32 ? $"Fehler {win32.NativeErrorCode}" : ex.GetType().Name;
        int count;
        lock (_refusalsLock)
        {
            count = _refusals[(call, error)] = _refusals.GetValueOrDefault((call, error)) + 1;
        }

        if (count == 1)
        {
            _log($"Steam-Controller-Kopie: {call} an den Puck fehlgeschlagen ({error}: {ex.Message}); weitere gleiche werden nur gezählt");
        }
    }

    /// <summary>"Feature-Report (Interface 3, …) Fehler 31: 85×; …", or null when the puck refused nothing.</summary>
    private string? RefusalSummary()
    {
        lock (_refusalsLock)
        {
            return _refusals.Count == 0
                ? null
                : string.Join("; ", _refusals.OrderBy(r => r.Key.Call.Interface).Select(r => $"{r.Key.Call} {r.Key.Error}: {r.Value}×"));
        }
    }

    /// <param name="data">The data Steam reads, or null to refuse the request.</param>
    private void Reply(byte iface, ushort sequence, byte[]? data)
    {
        var frame = new byte[HidProxyFrames.HeaderSize + 3 + (data?.Length ?? 0)];
        var size = HidProxyFrames.WriteReply(frame, iface, sequence, data is not null, data ?? []);
        Send(frame, size);
    }

    private void Send(byte[] frame, int size)
    {
        lock (_send)
        {
            Volatile.Write(ref _sendingSince, Stopwatch.GetTimestamp());
            try
            {
                _viiper.Write(frame, 0, size);
            }
            finally
            {
                Volatile.Write(ref _sendingSince, 0);
            }
        }
    }

    /// <summary>A write to VIIPER that hangs past the limit (VIIPER stopped reading, its buffer is full): the copy broke.</summary>
    private void WatchSend()
    {
        var since = Volatile.Read(ref _sendingSince);
        if (since == 0 || _closed || Stopwatch.GetElapsedTime(since) < _sendTimeout)
        {
            return;
        }

        Broke($"VIIPER nimmt seit {_sendTimeout.TotalSeconds:0.#} s keine Daten an");
        // Ends the hanging write, so the thread that waits in it gets free.
        Quietly(_viiper.Dispose);
    }

    private void Broke(string why)
    {
        _broken = true;
        if (Interlocked.Exchange(ref _brokenLogged, 1) == 0)
        {
            _log($"Steam-Controller-Kopie: {why}");
        }
    }

    /// <returns>False when it threw.</returns>
    private static bool Quietly(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception)
        {
            // Closing what is already broken.
            return false;
        }
    }
}

/// <summary>
/// A call to the puck on Steam's behalf as the log names it (#314): never the payload, which can hold the serial number.
/// </summary>
/// <param name="Message">The feature message it sends, or for a GET_REPORT the one it reads the answer to.</param>
internal readonly record struct PuckCall(string What, byte Interface, byte ReportId, string? Message)
{
    public override string ToString() =>
        $"{What} (Interface {Interface}, Report 0x{ReportId:X2}{(Message is null ? "" : $", {Message}")})";
}

/// <summary>
/// The delay from a puck report read to it written to VIIPER (T242-18, budget: p99 under 1 ms): the last 8192 reports,
/// summarised every 10 minutes and when the copy ends.
/// </summary>
public sealed class CopyLatency
{
    private const int Window = 8192;
    private static readonly long Every = Stopwatch.Frequency * 600;

    private readonly double[] _microseconds = new double[Window];
    private readonly Lock _lock = new();
    private long _count;
    private long _lastSummary = Stopwatch.GetTimestamp();

    public void Add(TimeSpan delay)
    {
        lock (_lock)
        {
            _microseconds[_count++ % Window] = delay.TotalMicroseconds;
        }
    }

    /// <summary>True once every 10 minutes.</summary>
    public bool Due()
    {
        var now = Stopwatch.GetTimestamp();
        var last = Interlocked.Read(ref _lastSummary);
        return now - last >= Every && Interlocked.CompareExchange(ref _lastSummary, now, last) == last;
    }

    /// <summary>"n …, p50 … µs, p99 … µs, max … µs" over the window.</summary>
    public string Summary()
    {
        double[] sorted;
        long count;
        lock (_lock)
        {
            count = _count;
            sorted = _microseconds[..(int)Math.Min(count, Window)];
        }

        if (sorted.Length == 0)
        {
            return "keine Berichte";
        }

        Array.Sort(sorted);
        return FormattableString.Invariant($"n {count}, p50 {Percentile(sorted, 0.50):0} µs, p99 {Percentile(sorted, 0.99):0} µs, max {sorted[^1]:0} µs");
    }

    private static double Percentile(double[] sorted, double p) => sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * p))];
}
