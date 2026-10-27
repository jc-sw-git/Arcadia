using System.Text;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// Just enough of Protocol Buffers for Steam's sign-in service (<c>IAuthenticationService</c>), which takes and returns
/// its messages as protobuf (<c>docs/launchers/steam/account.md</c>). Field numbers are in
/// <c>steammessages_auth.steamclient.proto</c>.
/// </summary>
internal sealed class ProtoWriter
{
    private readonly MemoryStream _buffer = new();

    public ProtoWriter String(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value));

    public ProtoWriter Bytes(int field, ReadOnlySpan<byte> value)
    {
        Tag(field, 2);
        Varint((ulong)value.Length);
        _buffer.Write(value);
        return this;
    }

    public ProtoWriter Message(int field, ProtoWriter message) => Bytes(field, message.ToArray());

    /// <summary>A uint64, uint32, enum or bool field; an int32 goes through <see cref="Int32"/>.</summary>
    public ProtoWriter UInt(int field, ulong value)
    {
        Tag(field, 0);
        Varint(value);
        return this;
    }

    /// <summary>A negative int32 is sign-extended to ten bytes, as protobuf wants it.</summary>
    public ProtoWriter Int32(int field, int value) => UInt(field, unchecked((ulong)(long)value));

    public ProtoWriter Fixed64(int field, ulong value)
    {
        Tag(field, 1);
        Span<byte> bytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        _buffer.Write(bytes);
        return this;
    }

    public byte[] ToArray() => _buffer.ToArray();

    private void Tag(int field, int wireType) => Varint((ulong)((field << 3) | wireType));

    private void Varint(ulong value)
    {
        while (value >= 0x80)
        {
            _buffer.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        _buffer.WriteByte((byte)value);
    }
}

/// <summary>
/// The fields of one protobuf message by number, the last value of each kept. Unknown fields are skipped by their wire
/// type; a message that ends inside a field throws <see cref="FormatException"/>.
/// </summary>
internal sealed class ProtoMessage
{
    private readonly Dictionary<int, ulong> _numbers = [];
    private readonly Dictionary<int, byte[]> _bytes = [];

    private ProtoMessage()
    {
    }

    public static ProtoMessage Parse(ReadOnlySpan<byte> data)
    {
        var message = new ProtoMessage();
        var position = 0;
        while (position < data.Length)
        {
            var tag = ReadVarint(data, ref position);
            var field = (int)(tag >> 3);
            switch ((int)(tag & 7))
            {
                case 0:
                    message._numbers[field] = ReadVarint(data, ref position);
                    break;
                case 1:
                    message._numbers[field] = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(Take(data, ref position, 8));
                    break;
                case 2:
                    var length = ReadVarint(data, ref position);
                    if (length > int.MaxValue)
                    {
                        throw new FormatException("Protobuf: Feld zu lang");
                    }

                    message._bytes[field] = Take(data, ref position, (int)length).ToArray();
                    break;
                case 5:
                    message._numbers[field] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(Take(data, ref position, 4));
                    break;
                default:
                    throw new FormatException($"Protobuf: unbekannter Feldtyp {tag & 7}");
            }
        }

        return message;
    }

    public ulong? UInt(int field) => _numbers.TryGetValue(field, out var value) ? value : null;

    /// <summary>A float field (wire type 5) as its value.</summary>
    public float? Float(int field) => _numbers.TryGetValue(field, out var value) ? BitConverter.Int32BitsToSingle(unchecked((int)value)) : null;

    public byte[]? Bytes(int field) => _bytes.GetValueOrDefault(field);

    public string? String(int field) => _bytes.TryGetValue(field, out var value) ? Encoding.UTF8.GetString(value) : null;

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int position)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            var b = Take(data, ref position, 1)[0];
            value |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80)
            {
                return value;
            }
        }

        throw new FormatException("Protobuf: Zahl zu lang");
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> data, ref int position, int count)
    {
        if (count < 0 || position + count > data.Length)
        {
            throw new FormatException("Protobuf: Nachricht endet mitten im Feld");
        }

        var slice = data.Slice(position, count);
        position += count;
        return slice;
    }
}
