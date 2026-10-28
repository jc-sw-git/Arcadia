using System.Buffers.Binary;
using Arcadia.Input;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>How a report from Steam reaches the copy (#242): USB's HID requests, as VIIPER hands them on.</summary>
public enum SteamReportKind
{
    /// <summary>An interrupt OUT transfer or a SET_REPORT of type output; the first byte is the report id.</summary>
    Output,

    /// <summary>A SET_REPORT of type feature: report id 1 or 2, then the message type.</summary>
    SetFeature,

    /// <summary>A GET_REPORT (feature or input): Steam reads the answer to its last message.</summary>
    GetReport,

    /// <summary>Anything else, e.g. the puck's serial interfaces: never passed on.</summary>
    Other,
}

/// <summary>
/// The rules of the Steam Controller copy (#242), free of Windows and VIIPER: which of Steam's reports reach the real puck
/// (<see cref="FromSteam"/>, the allow list of <see cref="SteamControllerMessages"/>) and what Steam reads from the
/// copy (<see cref="ToSteam"/>): everything but Arcadia's combos while the copy passes input, otherwise rest with only
/// the Steam button and "…" (they belong to Steam, <c>docs/product/controls.md</c>). One filter per copy.
/// </summary>
/// <remarks>
/// <see cref="ToSteam"/> runs on the copy's reader thread only (it holds the combo state); <see cref="FromSteam"/> may
/// run on another thread. Report layouts: SDL's <c>TritonMTUFull_t</c> and <c>TritonMTUNoQuat32TS_t</c>, offsets
/// counted with the report id at 0.
/// </remarks>
public sealed class SteamControllerCopyFilter(TimeProvider time, Action<string> log)
{
    public const byte StateReport = 0x42;
    public const byte StateReportBle = 0x45;
    public const byte StateReportWithTimestamp = 0x47;
    public const byte BatteryReport = 0x43;
    public const byte WirelessReportX = 0x46;
    public const byte WirelessReport = 0x79;

    public const uint ViewButton = 0x00000040;
    public const uint MenuButton = 0x00004000;
    public const uint QamButton = 0x00000010;
    public const uint SteamButton = 0x00010000;

    /// <summary>Report id, sequence number, then the 32-bit button mask.</summary>
    private const int ButtonsAt = 2;
    private const int AxesAt = 6;
    private const int SticksEnd = 18;
    private const int GyroAt = 40;
    private const int GyroEnd = 46;

    private readonly ComboWithholding _withholding = new(time);
    private readonly HashSet<string> _logged = [];
    private int _firmwareSeen;

    /// <summary>Steam sent a known firmware or flash update message (dropped). Raised once per filter; any thread.</summary>
    public event Action<string>? FirmwareBlocked;

    /// <summary>
    /// Whether a report Steam sent goes on to the real puck. <paramref name="forwarding"/>: the copy passes input now, so
    /// haptics may pass too. Dropped types are logged once each (type and length, never the payload: it can hold the
    /// serial number). Never throws.
    /// </summary>
    public bool FromSteam(SteamReportKind kind, ReadOnlySpan<byte> report, bool forwarding)
    {
        try
        {
            return Classify(kind, report, forwarding);
        }
        catch (Exception)
        {
            // A handler or the log failed: dropped, so nothing reaches the puck by an error's path.
            return false;
        }
    }

    private bool Classify(SteamReportKind kind, ReadOnlySpan<byte> report, bool forwarding)
    {
        switch (kind)
        {
            case SteamReportKind.GetReport:
                // Reading the answer to an allowed message changes nothing on the controller.
                return true;
            case SteamReportKind.Output when report.Length >= 1:
                return Decide(SteamControllerMessages.OfOutput(report[0]), $"Ausgabe {SteamControllerMessages.NameOfOutput(report[0])}", report.Length, forwarding);
            case SteamReportKind.SetFeature when report.Length >= 2 && report[0] is 1 or 2:
                return Decide(SteamControllerMessages.OfFeature(report[1]), $"Nachricht {SteamControllerMessages.NameOfFeature(report[1])}", report.Length, forwarding);
            case SteamReportKind.SetFeature:
                return Decide(SteamMessageKind.Blocked, $"Feature-Report {(report.Length >= 1 ? $"0x{report[0]:X2}" : "leer")}", report.Length, forwarding);
            default:
                return Decide(SteamMessageKind.Blocked, kind.ToString(), report.Length, forwarding);
        }
    }

    /// <summary>
    /// What Steam reads for a report the real puck sent: written to <paramref name="output"/> (at least as long as
    /// <paramref name="report"/>), the length to send, or 0 to send nothing. Battery and connection state always pass;
    /// a state report passes without Arcadia's combos while <paramref name="forwarding"/>, otherwise as rest; any other
    /// report only while forwarding. Never throws.
    /// </summary>
    public int ToSteam(ReadOnlySpan<byte> report, Span<byte> output, bool forwarding, ComboWithholdingOptions combos)
    {
        if (report.IsEmpty || output.Length < report.Length)
        {
            return 0;
        }

        var id = report[0];
        if (id is BatteryReport or WirelessReport or WirelessReportX)
        {
            report.CopyTo(output);
            return report.Length;
        }

        if (id is not (StateReport or StateReportBle or StateReportWithTimestamp))
        {
            if (!forwarding)
            {
                return 0;
            }

            report.CopyTo(output);
            return report.Length;
        }

        if (report.Length < AxesAt)
        {
            // Too short to hold the buttons: nothing safe to say about it.
            return 0;
        }

        report.CopyTo(output);
        var raw = BinaryPrimitives.ReadUInt32LittleEndian(report[ButtonsAt..]);

        // Filtered on every report, also while resting, so a combo begun in Arcadia stays Arcadia's (as for the pad).
        var combo = (raw & ViewButton) != 0 ? PadButtons.View : PadButtons.None;
        combo |= (raw & MenuButton) != 0 ? PadButtons.Menu : PadButtons.None;
        var shown = _withholding.Filter(combo, combos);
        var buttons = raw & ~(ViewButton | MenuButton);
        buttons |= (shown & PadButtons.View) != 0 ? ViewButton : 0;
        buttons |= (shown & PadButtons.Menu) != 0 ? MenuButton : 0;

        if (!forwarding)
        {
            buttons &= SteamButton | QamButton;
            Rest(output[..report.Length], id);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(output[ButtonsAt..], buttons);
        return report.Length;
    }

    /// <summary>
    /// Triggers, sticks, trackpads and the gyro at rest; the accelerometer, the orientation and the timestamps stay, so
    /// Steam sees a controller lying still rather than one that broke.
    /// </summary>
    private static void Rest(Span<byte> state, byte id)
    {
        Zero(state, AxesAt, SticksEnd);

        // With the timestamp report the trackpads come after a 16-bit trackpad timestamp.
        var padsAt = id == StateReportWithTimestamp ? SticksEnd + 2 : SticksEnd;
        Zero(state, padsAt, padsAt + 12);
        Zero(state, GyroAt, GyroEnd);
    }

    private static void Zero(Span<byte> state, int from, int to)
    {
        if (from < state.Length)
        {
            state[from..Math.Min(to, state.Length)].Clear();
        }
    }

    private bool Decide(SteamMessageKind kind, string what, int length, bool forwarding)
    {
        switch (kind)
        {
            case SteamMessageKind.Allowed:
                return true;
            case SteamMessageKind.Haptics:
                return forwarding;
            case SteamMessageKind.Firmware:
                LogOnce($"Steam-Controller-Kopie: Firmware-{what} verworfen ({length} Bytes)");
                if (Interlocked.Exchange(ref _firmwareSeen, 1) == 0)
                {
                    FirmwareBlocked?.Invoke(what);
                }

                return false;
            default:
                LogOnce($"Steam-Controller-Kopie: {what} verworfen ({length} Bytes)");
                return false;
        }
    }

    private void LogOnce(string line)
    {
        bool first;
        lock (_logged)
        {
            first = _logged.Add(line[..line.LastIndexOf(" (", StringComparison.Ordinal)]);
        }

        if (first)
        {
            log(line);
        }
    }
}
