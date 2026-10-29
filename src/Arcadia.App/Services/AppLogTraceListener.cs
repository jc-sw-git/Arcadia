using System.Diagnostics;

namespace Arcadia.App.Services;

/// <summary>
/// Puts what Core and the launchers report through <see cref="Trace"/> (they know no <see cref="AppLog"/>) into
/// <c>arcadia.log</c>, e.g. a launcher whose scan failed or Steam's unreadable <c>appinfo.vdf</c> (#141).
/// </summary>
public sealed class AppLogTraceListener : TraceListener
{
    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            AppLog.Write(message);
        }
    }

    // Without the "Arcadia.exe Warning: 0 :" header TraceListener writes before each event.
    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
        WriteLine(message);

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
        WriteLine(args is { Length: > 0 } && format is not null ? string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args) : format);
}
