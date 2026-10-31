using System.Buffers.Binary;

namespace Arcadia.Pan;

/// <summary>
/// What Arcadia asks of Pan over the pipe and what Pan answers (#243). The command set is fixed and tiny on purpose: no
/// path, command line or process id travels in a message, so nothing Pan does as SYSTEM can be steered from outside
/// beyond what the console user may do anyway.
/// </summary>
public enum PanCommand : byte
{
    None = 0,

    /// <summary>First message of a client; <see cref="PanMessage.Version"/> is its protocol version.</summary>
    Hello = 1,

    /// <summary>
    /// Arcadia starts emulating a controller: Pan takes its forwarding processes and answers with the generation.
    /// <see cref="PanMessage.Value"/> is the usbip port of Arcadia's VIIPER on <c>127.0.0.1</c> (#304), which Pan checks
    /// and journals; its teardown detaches only devices imported from there.
    /// </summary>
    EmulationStarted = 2,

    /// <summary>Arcadia removed its emulation itself; the generation ends.</summary>
    EmulationStopped = 3,

    /// <summary>The thread that feeds the emulated controller is alive; sent every half second while emulating.</summary>
    Heartbeat = 4,

    /// <summary>Arcadia has dropped its devices after a teardown; only then may a new emulation start.</summary>
    TeardownAcknowledged = 5,

    /// <summary>Arcadia exits on purpose and has cleaned up itself; Pan has nothing to do.</summary>
    PlannedExit = 6,

    /// <summary>
    /// Hide the real Steam Controller from every other program, the client excepted (part B of #243, for #242). Only
    /// within the current generation's emulation, so every teardown undoes it. Pan finds the devices itself.
    /// </summary>
    Hide = 7,

    /// <summary>Show the real Steam Controller again; Arcadia sends it before <see cref="EmulationStopped"/>.</summary>
    Unhide = 8,

    /// <summary>
    /// Whether Pan could hide now (HidHide installed, supported, in a usable state); needs no emulation. Asked with the
    /// generation Pan hides for (#242), it also checks that hiding still holds (<see cref="PanReason.HidingLost"/>).
    /// </summary>
    CheckHiding = 9,

    // 10 was HoldForFirmware (protocol 2): Steam's updater never reaches the real hardware through the copy (#295).

    /// <summary>
    /// Arcadia has let go of the puck after <see cref="PanReply.ReleasePuck"/>; <see cref="PanMessage.Value"/> is that
    /// notice's request number (#308). Pan answers <see cref="PanReply.Acknowledged"/>.
    /// </summary>
    PuckReleased = 11,

    /// <summary>
    /// Hide every connected controller of one model from every program but the client (#335); <see cref="PanMessage.Value"/>
    /// is the model (<see cref="ControllerModel.Value"/>). The client has let go of it first, as Pan restarts it. Pan
    /// answers <see cref="PanReply.ControllerHidden"/> or <see cref="PanReply.Rejected"/>.
    /// </summary>
    HideController = 12,

    /// <summary>Show one model again (excluded, or no longer emulated); the others and the puck stay hidden. Answer: <see cref="PanReply.ControllerShown"/>.</summary>
    ShowController = 13,
}

/// <summary>Pan's answers; the high bit tells them from commands.</summary>
public enum PanReply : byte
{
    None = 0,
    HelloOk = 0x81,
    Started = 0x82,
    Stopped = 0x83,
    HeartbeatOk = 0x84,
    Acknowledged = 0x85,

    /// <summary><see cref="PanMessage.Value"/> is the <see cref="PanReason"/>.</summary>
    Rejected = 0x90,

    /// <summary>Pan tore the emulation down; <see cref="PanMessage.Value"/> is the <see cref="PanReason"/>, <see cref="PanMessage.Generation"/> the generation it ended.</summary>
    TornDown = 0x91,

    /// <summary>The command exists in the protocol but this Pan can't do it (hiding before part B).</summary>
    Unsupported = 0x92,

    /// <summary>The connection is refused as a whole: another client is registered, or the client isn't in the console session.</summary>
    Refused = 0x93,

    /// <summary>The Steam Controller is hidden; <see cref="PanMessage.Value"/> is how many device instances Pan hides (0: none connected yet).</summary>
    Hidden = 0x86,

    /// <summary>The Steam Controller is shown again.</summary>
    Shown = 0x87,

    /// <summary>The answer to <see cref="PanCommand.CheckHiding"/>; <see cref="PanMessage.Value"/> is why Pan can't hide, or <see cref="PanReason.None"/>.</summary>
    HidingState = 0x88,

    /// <summary>
    /// A notice: Pan is about to restart the puck on its own (a teardown's show, its retry, a device arrival while
    /// hiding) and asks the client to close its handles first, as Windows refuses the restart of a device a program holds
    /// (#308). <see cref="PanMessage.Value"/> is the request number the client returns with <see cref="PanCommand.PuckReleased"/>.
    /// </summary>
    ReleasePuck = 0x94,

    /// <summary>A notice: the restart announced by <see cref="ReleasePuck"/> (same <see cref="PanMessage.Value"/>) is over, done or not.</summary>
    PuckRestarted = 0x95,

    /// <summary>
    /// The model is hidden; <see cref="PanMessage.Value"/> is <see cref="PanReason.None"/>, or
    /// <see cref="PanReason.ControllerNeedsReplug"/> when programs that opened it before keep it until it is reconnected.
    /// </summary>
    ControllerHidden = 0x89,

    /// <summary>The model is shown again; <see cref="PanMessage.Value"/> as for <see cref="ControllerHidden"/>.</summary>
    ControllerShown = 0x8A,
}

/// <summary>Why a command was rejected or an emulation torn down.</summary>
public enum PanReason : uint
{
    None = 0,
    WrongProtocolVersion = 1,
    WrongGeneration = 2,
    NotRegistered = 3,
    TeardownNotAcknowledged = 4,
    AlreadyEmulating = 5,
    NotEmulating = 6,
    Hang = 7,
    ClientGone = 8,
    ServiceStopping = 9,
    JournalUnreadable = 10,
    OtherClient = 11,
    ForeignSession = 12,

    /// <summary>HidHide isn't installed, or its driver isn't loaded (a restart pending after its setup).</summary>
    HidHideMissing = 13,

    /// <summary>HidHide's version or driver interface isn't one Pan knows.</summary>
    HidHideUnsupported = 14,

    /// <summary>HidHide's application list is inverted: allow-listing Arcadia would hide the controller from it.</summary>
    HidHideInverseMode = 15,

    /// <summary>Cloaking is off while the user has other devices listed: turning it on would hide those too.</summary>
    HidHideCloakingOffWithForeignEntries = 16,

    /// <summary>Another program (e.g. HidHide's configuration client) has HidHide open; it allows one at a time.</summary>
    HidHideBusy = 17,

    /// <summary>Changing HidHide or restarting the controller failed; Pan's log says why. What Pan changed stays journaled.</summary>
    HidingFailed = 18,

    /// <summary>A teardown after <see cref="PanCommand.EmulationStopped"/> while the Steam Controller was still hidden.</summary>
    EmulationStoppedWhileHidden = 19,

    /// <summary>A teardown at the client's planned exit, because it left the Steam Controller hidden.</summary>
    PlannedExit = 20,

    /// <summary>
    /// <see cref="PanCommand.EmulationStarted"/> while Pan shows the Steam Controller after an earlier emulation (a
    /// retry, or the teardown after a stop while hidden; usually milliseconds, a puck restart ~2.5 s): start again in a moment.
    /// </summary>
    SteamControllerBeingShown = 21,

    /// <summary>
    /// The answer to <see cref="PanCommand.CheckHiding"/> of the generation Pan hides for, when that hiding no longer
    /// holds (#242): a hidden device, the client's allow-list entry or cloaking is gone, e.g. changed in HidHide's
    /// configuration client. Arcadia ends its emulation at once.
    /// </summary>
    HidingLost = 22,

    // 23 was HeldForFirmware (protocol 2), dropped with the firmware hold (#295).

    /// <summary><see cref="PanCommand.EmulationStarted"/> without a <c>viiper.exe</c> of the client's to hold (#304).</summary>
    NoForwardingProcess = 24,

    /// <summary>
    /// The usbip port of <see cref="PanCommand.EmulationStarted"/> is missing, or not served by the held
    /// <c>viiper.exe</c> alone (another process listens on it, in IPv4 or IPv6), or the listeners can't be read (#304).
    /// </summary>
    UsbipPortNotServed = 25,

    /// <summary>No controller of the model is connected.</summary>
    ControllerNotConnected = 26,

    /// <summary>
    /// The model can't be hidden: none of its devices is a gamepad HID collection or an XInput interface Pan knows (e.g.
    /// an Xbox pad on the Xbox Wireless Adapter), it sits on a software host, or it is a Valve device (the puck's own path).
    /// </summary>
    ControllerNotHideable = 27,

    /// <summary>The vendor's firmware tool runs (Xbox Accessories, Sony's DualSense updater): nothing of that vendor is hidden meanwhile (#238 D8).</summary>
    FirmwareToolRuns = 28,

    /// <summary>Hidden or shown, but Windows restarts the device only when it is reconnected (an XInput interface, #335 A3).</summary>
    ControllerNeedsReplug = 29,

    /// <summary>
    /// A Valve bootloader (<c>28DE:1005</c> controller by cable, <c>28DE:1007</c> puck) is connected or was a moment ago:
    /// a firmware update is being flashed, and a hide or restart of the puck could cut it off (#295). Not a failure: the
    /// client tries again at its next tick.
    /// </summary>
    BootloaderPresent = 30,
}

/// <summary>
/// One message on the pipe, both ways: 12 bytes, little endian, fixed size, so a reader never allocates and a message
/// can't be malformed beyond an unknown command (which is rejected). Field use per command: <see cref="Version"/> with
/// <see cref="PanCommand.Hello"/> and <see cref="PanReply.HelloOk"/>, <see cref="Generation"/> with everything after,
/// <see cref="Value"/> for reasons.
/// </summary>
public readonly record struct PanMessage(byte Kind, ushort Version, uint Generation, uint Value)
{
    public const int Size = 12;

    /// <summary>Raised when the wire format or the meaning of a command changes; both sides must agree on it.</summary>
    public const ushort ProtocolVersion = 3;

    public PanCommand Command => (PanCommand)Kind;

    public PanReply Reply => (PanReply)Kind;

    public PanReason Reason => (PanReason)Value;

    public static PanMessage Of(PanCommand command, uint generation = 0, uint value = 0) => new((byte)command, ProtocolVersion, generation, value);

    public static PanMessage Of(PanReply reply, uint generation = 0, PanReason reason = PanReason.None) => new((byte)reply, ProtocolVersion, generation, (uint)reason);

    /// <summary>A notice about Pan's own restart of the puck, with its request number.</summary>
    public static PanMessage PuckRestart(PanReply notice, uint request) => new((byte)notice, ProtocolVersion, 0, request);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = Kind;
        buffer[1] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[2..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], Generation);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[8..], Value);
    }

    public static PanMessage Read(ReadOnlySpan<byte> buffer) => new(
        buffer[0],
        BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..]),
        BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]),
        BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..]));

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        Write(bytes);
        return bytes;
    }

    public override string ToString() => Kind >= 0x80
        ? $"{Reply}(gen {Generation}{(Value == 0 ? string.Empty : Reply == PanReply.Hidden ? $", {Value} device(s)" : Reply is PanReply.ReleasePuck or PanReply.PuckRestarted ? $", request {Value}" : $", {Reason}")})"
        : $"{Command}(gen {Generation}{(Command == PanCommand.Hello ? $", v{Version}" : Command == PanCommand.PuckReleased ? $", request {Value}" : Command is PanCommand.HideController or PanCommand.ShowController ? $", {ControllerModel.From(Value)}" : string.Empty)})";
}
