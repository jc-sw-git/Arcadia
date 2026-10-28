using System.Buffers.Binary;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>A frame on the stream of VIIPER's <c>hidproxy</c> device (#242 part B, <c>docs/devices/hidproxy.md</c> in the fork).</summary>
public readonly record struct HidProxyFrame(byte Kind, byte Interface, byte[] Payload);

/// <summary>
/// The stream protocol of VIIPER's <c>hidproxy</c> device, the copy of the puck (fork <c>jc-sw-git/VIIPER</c>): a 4-byte
/// header (kind, interface number, payload length as little-endian 16 bits), then the payload.
/// </summary>
public static class HidProxyFrames
{
    /// <summary>Arcadia to VIIPER: an input report for the interface's IN endpoint, as the puck sends it over USB.</summary>
    public const byte Input = 0x01;

    /// <summary>Arcadia to VIIPER: the answer to a <see cref="Control"/>: sequence (16 bits), status (0 done), data.</summary>
    public const byte ControlReply = 0x02;

    /// <summary>VIIPER to Arcadia: what Steam sent to an OUT endpoint of the interface.</summary>
    public const byte Output = 0x81;

    /// <summary>VIIPER to Arcadia: a control request: sequence (16 bits), the 8-byte setup packet, the OUT data.</summary>
    public const byte Control = 0x82;

    public const int HeaderSize = 4;

    /// <summary>A control transfer's data stage is at most 4096 bytes, plus the sequence and the setup packet.</summary>
    public const int MaxPayload = 4096 + 2 + 8;

    public const byte StatusDone = 0;
    public const byte StatusRefused = 1;

    /// <summary>Writes the frame into <paramref name="buffer"/> and returns its length.</summary>
    public static int Write(Span<byte> buffer, byte kind, byte iface, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayload)
        {
            throw new ArgumentException($"Nutzlast von {payload.Length} Bytes, höchstens {MaxPayload}", nameof(payload));
        }

        buffer[0] = kind;
        buffer[1] = iface;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[2..], (ushort)payload.Length);
        payload.CopyTo(buffer[HeaderSize..]);
        return HeaderSize + payload.Length;
    }

    /// <summary>The reply to the control request <paramref name="sequence"/>: status, then the data Steam reads.</summary>
    public static int WriteReply(Span<byte> buffer, byte iface, ushort sequence, bool done, ReadOnlySpan<byte> data)
    {
        var payload = buffer[HeaderSize..];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, sequence);
        payload[2] = done ? StatusDone : StatusRefused;
        data.CopyTo(payload[3..]);
        return Write(buffer, ControlReply, iface, payload[..(3 + data.Length)]);
    }

    /// <summary>Reads one frame; null at the stream's end. Throws on a payload longer than <see cref="MaxPayload"/>.</summary>
    public static async Task<HidProxyFrame?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        if (!await FillAsync(stream, header, cancellationToken))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
        if (length > MaxPayload)
        {
            throw new InvalidDataException($"VIIPER schickt einen Frame mit {length} Bytes, höchstens {MaxPayload}");
        }

        var payload = new byte[length];
        if (!await FillAsync(stream, payload, cancellationToken))
        {
            throw new EndOfStreamException("VIIPER hat die Verbindung mitten im Frame beendet");
        }

        return new HidProxyFrame(header[0], header[1], payload);
    }

    private static async Task<bool> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0)
            {
                return read == 0 ? false : throw new EndOfStreamException("VIIPER hat die Verbindung mitten im Frame beendet");
            }

            read += n;
        }

        return true;
    }
}

/// <summary>A USB setup packet as VIIPER's <see cref="HidProxyFrames.Control"/> carries it.</summary>
public readonly record struct SetupPacket(byte RequestType, byte Request, ushort Value, ushort Index, ushort Length)
{
    public const int Size = 8;

    public static SetupPacket Read(ReadOnlySpan<byte> bytes) => new(
        bytes[0],
        bytes[1],
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]),
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]));

    /// <summary>Device to host: Steam reads data.</summary>
    public bool IsIn => (RequestType & 0x80) != 0;

    public override string ToString() => $"0x{RequestType:X2}/0x{Request:X2}";
}
