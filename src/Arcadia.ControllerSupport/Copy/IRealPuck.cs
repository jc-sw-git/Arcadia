namespace Arcadia.ControllerSupport.Copy;

/// <summary>HID's report types, as a SET_REPORT or GET_REPORT names them in the high byte of wValue.</summary>
public enum HidReportType : byte
{
    Input = 1,
    Output = 2,
    Feature = 3,
}

/// <summary>
/// The real puck as the copy needs it (#242 part C): the vendor collection of each HID interface, read and written raw
/// (Arcadia is on HidHide's allow list, Steam isn't). Reports are as on USB: led by the report id. One reader per
/// interface may block in <see cref="ReadInput"/> while another thread writes.
/// </summary>
public interface IRealPuck : IDisposable
{
    /// <summary>The HID interfaces whose vendor collection is open.</summary>
    IReadOnlyCollection<byte> Interfaces { get; }

    /// <summary>
    /// Waits for the interface's next input report and returns its length, or 0 once the puck was closed. Throws when the
    /// puck is gone (unplugged, restarted).
    /// </summary>
    int ReadInput(byte iface, Span<byte> report);

    /// <summary>Sends a feature report (SET_REPORT feature); throws when the puck refuses it or is gone.</summary>
    void SetFeature(byte iface, ReadOnlySpan<byte> report);

    /// <summary>Sends an output report (interrupt OUT, or SET_REPORT output); throws when the puck refuses it or is gone.</summary>
    void WriteOutput(byte iface, ReadOnlySpan<byte> report);

    /// <summary>Reads a feature or input report by id (GET_REPORT) into <paramref name="report"/> and returns its length.</summary>
    int GetReport(byte iface, HidReportType type, byte id, Span<byte> report);
}
