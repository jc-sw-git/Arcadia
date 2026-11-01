using System.Buffers.Binary;
using System.Text;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// An ordered binary-VDF map. Values are <see cref="string"/>, <see cref="int"/> or nested
/// <see cref="VdfMap"/>. Key order is preserved so files round-trip byte for byte, including
/// fields Arcadia doesn't know about.
/// </summary>
public sealed class VdfMap : List<KeyValuePair<string, object>>
{
    /// <summary>
    /// Deeper nesting than any real Steam file. The parsers recurse once per level, so without this limit a
    /// crafted file would overflow the stack, which no <c>catch</c> survives; above it they throw
    /// <see cref="FormatException"/> like for any other broken file.
    /// </summary>
    internal const int MaxDepth = 64;

    public object? this[string key]
    {
        get => Find(key) is { } index ? this[index].Value : null;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Find(key) is { } index)
            {
                this[index] = new(key, value);
            }
            else
            {
                Add(new(key, value));
            }
        }
    }

    public string? GetString(string key) => this[key] as string;

    public int? GetInt(string key) => this[key] as int?;

    public VdfMap? GetMap(string key) => this[key] as VdfMap;

    private int? Find(string key)
    {
        for (var i = 0; i < Count; i++)
        {
            if (string.Equals(this[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }
}

/// <summary>
/// Reads and writes Steam's binary VDF (e.g. <c>shortcuts.vdf</c>):
/// <c>0x00 key\0 … 0x08</c> nested map, <c>0x01 key\0 value\0</c> string,
/// <c>0x02 key\0 int32-LE</c> integer, <c>0x08</c> end of map.
/// </summary>
public static class BinaryVdf
{
    private const byte TypeMap = 0x00;
    private const byte TypeString = 0x01;
    private const byte TypeInt = 0x02;
    private const byte TypeEnd = 0x08;

    /// <exception cref="FormatException">The data is not valid binary VDF (unknown type, truncated, nested too deep).</exception>
    public static VdfMap Parse(ReadOnlySpan<byte> data)
    {
        var position = 0;
        var root = ReadMap(data, ref position, depth: 0);
        return root;
    }

    public static byte[] Write(VdfMap root)
    {
        using var stream = new MemoryStream();
        WriteMap(root, stream);
        return stream.ToArray();
    }

    private static VdfMap ReadMap(ReadOnlySpan<byte> data, ref int position, int depth)
    {
        if (depth > VdfMap.MaxDepth)
        {
            throw new FormatException($"VDF nested deeper than {VdfMap.MaxDepth} levels at byte {position}.");
        }

        var map = new VdfMap();
        while (true)
        {
            if (position >= data.Length)
            {
                // Steam's files end the root without a final 0x08 in some versions. A nested map cut off is a
                // truncated file: reading it as complete would let an edit write the rest of the file away.
                return depth == 0 ? map : throw new FormatException("Unexpected end of VDF data inside a map.");
            }

            var type = data[position++];
            if (type == TypeEnd)
            {
                return map;
            }

            var key = ReadString(data, ref position);
            object value = type switch
            {
                TypeMap => ReadMap(data, ref position, depth + 1),
                TypeString => ReadString(data, ref position),
                TypeInt => ReadInt(data, ref position),
                _ => throw new FormatException($"Unsupported VDF type 0x{type:X2} at byte {position - 1}."),
            };
            map.Add(new(key, value));
        }
    }

    private static string ReadString(ReadOnlySpan<byte> data, ref int position)
    {
        var length = data[position..].IndexOf((byte)0);
        if (length < 0)
        {
            throw new FormatException($"Unterminated VDF string at byte {position}.");
        }

        var value = Encoding.UTF8.GetString(data.Slice(position, length));
        position += length + 1;
        return value;
    }

    private static int ReadInt(ReadOnlySpan<byte> data, ref int position)
    {
        if (position + 4 > data.Length)
        {
            throw new FormatException($"Truncated VDF integer at byte {position}.");
        }

        var value = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(position, 4));
        position += 4;
        return value;
    }

    private static void WriteMap(VdfMap map, Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        foreach (var (key, value) in map)
        {
            switch (value)
            {
                case VdfMap child:
                    stream.WriteByte(TypeMap);
                    WriteString(key, stream);
                    WriteMap(child, stream);
                    break;
                case string text:
                    stream.WriteByte(TypeString);
                    WriteString(key, stream);
                    WriteString(text, stream);
                    break;
                case int number:
                    stream.WriteByte(TypeInt);
                    WriteString(key, stream);
                    BinaryPrimitives.WriteInt32LittleEndian(buffer, number);
                    stream.Write(buffer);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported VDF value for '{key}': {value?.GetType().Name}.");
            }
        }

        stream.WriteByte(TypeEnd);
    }

    private static void WriteString(string text, Stream stream)
    {
        stream.Write(Encoding.UTF8.GetBytes(text));
        stream.WriteByte(0);
    }
}
