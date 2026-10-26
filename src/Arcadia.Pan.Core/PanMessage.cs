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

    /// <summary>Arcadia starts emulating a controller: Pan takes its forwarding processes and answers with the generation.</summary>
    EmulationStarted = 2,

    /// <summary>Arcadia removed its emulation itself; the generation ends.</summary>
    EmulationStopped = 3,

    /// <summary>The thread that feeds the emulated controller is alive; sent every half second while emulating.</summary>
    Heartbeat = 4,

    /// <summary>Arcadia has dropped its devices after a teardown; only then may a new emulation start.</summary>
    TeardownAcknowledged = 5,

    /// <summary>Arcadia exits on purpose and has cleaned up itself; Pan has nothing to do.</summary>
    PlannedExit = 6,

    /// <summary>Hide the real Steam Controller from every other program (part B of #243, #242).</summary>
    Hide = 7,

    /// <summary>Show the real Steam Controller again (part B).</summary>
    Unhide = 8,
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
    public const ushort ProtocolVersion = 1;

    public PanCommand Command => (PanCommand)Kind;

    public PanReply Reply => (PanReply)Kind;

    public PanReason Reason => (PanReason)Value;

    public static PanMessage Of(PanCommand command, uint generation = 0, uint value = 0) => new((byte)command, ProtocolVersion, generation, value);

    public static PanMessage Of(PanReply reply, uint generation = 0, PanReason reason = PanReason.None) => new((byte)reply, ProtocolVersion, generation, (uint)reason);

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
        ? $"{Reply}(gen {Generation}{(Value != 0 ? $", {Reason}" : string.Empty)})"
        : $"{Command}(gen {Generation}{(Command == PanCommand.Hello ? $", v{Version}" : string.Empty)})";
}
