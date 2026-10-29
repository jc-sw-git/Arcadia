using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Reads the app type (<c>appinfo/common/type</c>: <c>Game</c>, <c>Application</c>, <c>Tool</c>, <c>Music</c>, <c>Demo</c>, …)
/// from Steam's binary app cache <c>appcache/appinfo.vdf</c> (#141). App manifests hold no type. The file holds every app
/// Steam has seen and can be hundreds of MB, so only the entry headers are read and only the asked-for entries parsed.
/// </summary>
/// <remarks>
/// Format (checked against the owner's file in #80, 2026-10-12): magic and universe, in v29 the offset of a string table at
/// the file's end (the entries' keys are indices into it); then per app its id, the size of the rest of the entry, info
/// state, last change, PICS token, SHA-1, change number, in v28+ a second SHA-1, then the app as binary VDF; app id 0 ends
/// the entries.
/// </remarks>
public static class SteamAppInfo
{
    // Header magics: version byte + "DV\x07", little endian.
    private const uint V27 = 0x07564427;
    private const uint V28 = 0x07564428;
    private const uint V29 = 0x07564429;

    // Info state, last change, PICS token, text SHA-1, change number; v28+ adds the binary SHA-1.
    private const int EntryHeaderV27 = 4 + 4 + 8 + 20 + 4;
    private const int EntryHeaderV28 = EntryHeaderV27 + 20;

    // No real entry comes near this; a larger size is a broken file, not a reason to allocate that much.
    private const int MaxEntrySize = 64 * 1024 * 1024;

    /// <summary>The type of every app of <paramref name="appIds"/> found in the file, as Steam writes it (e.g. <c>Game</c>).</summary>
    /// <exception cref="FormatException">An unknown version or a broken file.</exception>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read.</exception>
    public static IReadOnlyDictionary<uint, string> ReadTypes(string path, IReadOnlySet<uint> appIds) =>
        ReadApps(path, appIds).ToDictionary(app => app.Key, app => app.Value.Type);

    /// <inheritdoc cref="ReadTypes(string, IReadOnlySet{uint})"/>
    public static IReadOnlyDictionary<uint, string> ReadTypes(SafeFileHandle file, IReadOnlySet<uint> appIds) =>
        ReadApps(file, appIds).ToDictionary(app => app.Key, app => app.Value.Type);

    /// <summary>
    /// Type and name of every app of <paramref name="appIds"/> found in the file; the name names an owned app that isn't
    /// installed (#83), which has no manifest.
    /// </summary>
    /// <inheritdoc cref="ReadTypes(string, IReadOnlySet{uint})"/>
    public static IReadOnlyDictionary<uint, SteamAppEntry> ReadApps(string path, IReadOnlySet<uint> appIds)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ReadApps(file, appIds);
    }

    /// <inheritdoc cref="ReadApps(string, IReadOnlySet{uint})"/>
    public static IReadOnlyDictionary<uint, SteamAppEntry> ReadApps(SafeFileHandle file, IReadOnlySet<uint> appIds)
    {
        var length = RandomAccess.GetLength(file);
        Span<byte> header = stackalloc byte[16];
        Read(file, header[..8], 0, length);
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic is not (V27 or V28 or V29))
        {
            throw new FormatException($"Unknown appinfo.vdf version 0x{magic:X8}.");
        }

        long position = 8;
        var end = length;
        string[]? strings = null;
        if (magic == V29)
        {
            Read(file, header[..8], position, length);
            position += 8;
            var tableOffset = BinaryPrimitives.ReadInt64LittleEndian(header);
            if (tableOffset < position || tableOffset > length)
            {
                throw new FormatException("String table offset outside the file.");
            }

            strings = ReadStringTable(file, tableOffset, length);
            end = tableOffset;
        }

        var entryHeader = magic == V27 ? EntryHeaderV27 : EntryHeaderV28;
        var types = new Dictionary<uint, SteamAppEntry>();
        while (types.Count < appIds.Count && position + 4 <= end)
        {
            // Id and size in one read: one system call per app, and there are tens of thousands.
            Read(file, header[..(int)Math.Min(8, end - position)], position, end);
            var appId = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (appId == 0)
            {
                break;
            }

            if (position + 8 > end)
            {
                throw new FormatException($"Entry of app {appId} runs past the end of the file.");
            }

            var size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            var data = position + 8;
            if (size > MaxEntrySize || data + size > end)
            {
                throw new FormatException($"Entry of app {appId} runs past the end of the file.");
            }

            if (appIds.Contains(appId) && size > entryHeader)
            {
                var entry = new byte[size - entryHeader];
                Read(file, entry, data + entryHeader, end);
                if (EntryOf(entry, strings) is { } app)
                {
                    types[appId] = app;
                }
            }

            position = data + size;
        }

        return types;
    }

    private static SteamAppEntry? EntryOf(ReadOnlySpan<byte> entry, string[]? strings)
    {
        var reader = new Reader(entry, strings);
        var common = reader.Map().GetMap("appinfo")?.GetMap("common");
        return common?.GetString("type") is { Length: > 0 } type
            ? new SteamAppEntry(type, common.GetString("name") is { Length: > 0 } name ? name : null)
            : null;
    }

    private static string[] ReadStringTable(SafeFileHandle file, long offset, long length)
    {
        if (length - offset > MaxEntrySize)
        {
            throw new FormatException("String table too large.");
        }

        var table = new byte[length - offset];
        Read(file, table, offset, length);
        if (table.Length < 4)
        {
            throw new FormatException("String table too short.");
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(table);
        var strings = new List<string>();
        var position = 4;
        for (var i = 0u; i < count && position < table.Length; i++)
        {
            var size = table.AsSpan(position).IndexOf((byte)0);
            if (size < 0)
            {
                throw new FormatException("Unterminated string in the string table.");
            }

            strings.Add(Encoding.UTF8.GetString(table, position, size));
            position += size + 1;
        }

        return [.. strings];
    }

    private static void Read(SafeFileHandle file, Span<byte> buffer, long offset, long end)
    {
        if (offset + buffer.Length > end)
        {
            throw new FormatException("Unexpected end of appinfo.vdf.");
        }

        var done = 0;
        while (done < buffer.Length)
        {
            var read = RandomAccess.Read(file, buffer[done..], offset + done);
            if (read == 0)
            {
                throw new FormatException("Unexpected end of appinfo.vdf.");
            }

            done += read;
        }
    }

    /// <summary>A bounds-checked cursor over binary VDF; every overrun becomes a <see cref="FormatException"/>.</summary>
    /// <param name="strings">appinfo.vdf v29's string table, which the keys index; null: keys are strings.</param>
    internal ref struct Reader(ReadOnlySpan<byte> data, string[]? strings)
    {
        private const byte TypeMap = 0x00;
        private const byte TypeString = 0x01;
        private const byte TypeInt = 0x02;
        private const byte TypeFloat = 0x03;
        private const byte TypePointer = 0x04;
        private const byte TypeColor = 0x06;
        private const byte TypeUInt64 = 0x07;
        private const byte TypeEnd = 0x08;
        private const byte TypeInt64 = 0x0A;

        // Deeper nesting than any real file; stops a crafted file from overflowing the stack.
        private const int MaxDepth = 64;

        private readonly ReadOnlySpan<byte> _data = data;
        private int _position;

        /// <summary>How far the reader got; packageinfo.vdf's entries have no size, so the next one starts here.</summary>
        public readonly int Position => _position;


        public VdfMap Map(int depth = 0)
        {
            if (depth > MaxDepth)
            {
                throw new FormatException("Nesting too deep.");
            }

            var map = new VdfMap();
            while (true)
            {
                var type = Take(1)[0];
                if (type == TypeEnd)
                {
                    return map;
                }

                var key = Key();
                object value = type switch
                {
                    TypeMap => Map(depth + 1),
                    TypeString => String(),
                    TypeInt => BinaryPrimitives.ReadInt32LittleEndian(Take(4)),
                    TypeFloat or TypePointer or TypeColor => Take(4).Length,
                    TypeUInt64 or TypeInt64 => (int)BinaryPrimitives.ReadInt64LittleEndian(Take(8)),
                    _ => throw new FormatException($"Unsupported VDF type 0x{type:X2}."),
                };
                map.Add(new(key, value));
            }
        }

        private string Key()
        {
            if (strings is null)
            {
                return String();
            }

            var index = BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
            return index < strings.Length ? strings[index] : throw new FormatException("String index outside the table.");
        }

        private string String()
        {
            var length = _data[_position..].IndexOf((byte)0);
            if (length < 0)
            {
                throw new FormatException("Unterminated string.");
            }

            var value = Encoding.UTF8.GetString(_data.Slice(_position, length));
            _position += length + 1;
            return value;
        }

        public ReadOnlySpan<byte> Take(int count)
        {
            if (_position + count > _data.Length)
            {
                throw new FormatException("Unexpected end of an entry.");
            }

            var span = _data.Slice(_position, count);
            _position += count;
            return span;
        }
    }
}

/// <summary>An app as <c>appinfo.vdf</c> knows it: Steam's type (e.g. <c>Game</c>) and its name, if it has one.</summary>
public sealed record SteamAppEntry(string Type, string? Name);
