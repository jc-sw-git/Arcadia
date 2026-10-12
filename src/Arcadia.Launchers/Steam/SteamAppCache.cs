using System.Buffers.Binary;
using System.Text;

namespace Arcadia.Launchers.Steam;

/// <summary>Name and kind of an app, as Steam caches it (<c>common/name</c>, <c>common/type</c>).</summary>
/// <param name="Type">Lower case: <c>game</c>, <c>dlc</c>, <c>tool</c>, <c>demo</c>, <c>music</c>, …</param>
public sealed record SteamAppInfo(string Name, string Type);

/// <summary>
/// Reads Steam's binary PICS caches in <c>&lt;Steam&gt;/appcache</c>. <c>packageinfo.vdf</c> holds
/// the packages (licenses) of the logged-in account with their app ids; <c>appinfo.vdf</c> holds name
/// and type of every app Steam knows about. Together they are the account's library, without a
/// login or API key (checked on the owner's PC, 2026-10-12: 651 packages, 463 games).
/// </summary>
public static class SteamAppCache
{
    // Header magics: version byte + "DV\x07" (appinfo) / "UV\x06" (packageinfo), little endian.
    private const uint AppInfoV27 = 0x07564427;
    private const uint AppInfoV28 = 0x07564428;
    private const uint AppInfoV29 = 0x07564429;
    private const uint PackageInfoV39 = 0x06565527;
    private const uint PackageInfoV40 = 0x06565528;

    /// <summary>Package 0 is the base "Steam" package every account has; it holds SDKs and test apps, no purchases.</summary>
    private const uint BasePackage = 0;

    /// <summary>The app ids of all packages in <c>packageinfo.vdf</c>, except the base package.</summary>
    /// <exception cref="FormatException">Unknown version or a truncated file.</exception>
    public static IReadOnlySet<uint> ReadOwnedAppIds(ReadOnlySpan<byte> packageInfo)
    {
        var reader = new Reader(packageInfo);
        var magic = reader.UInt32();
        if (magic is not (PackageInfoV39 or PackageInfoV40))
        {
            throw new FormatException($"Unknown packageinfo.vdf version 0x{magic:X8}.");
        }

        reader.Skip(4); // universe
        var apps = new HashSet<uint>();
        while (true)
        {
            var packageId = reader.UInt32();
            if (packageId == uint.MaxValue)
            {
                return apps;
            }

            reader.Skip(20 + 4 + (magic == PackageInfoV40 ? 8 : 0)); // SHA-1, change number, PICS token
            var root = reader.Map(strings: null);
            if (packageId == BasePackage || root.Count == 0 || root[0].Value is not VdfMap package)
            {
                continue;
            }

            foreach (var (_, value) in package.GetMap("appids") ?? [])
            {
                if (value is int appId and > 0)
                {
                    apps.Add((uint)appId);
                }
            }
        }
    }

    /// <summary>Name and type of the <paramref name="wanted"/> apps found in <c>appinfo.vdf</c>; others are skipped unread.</summary>
    /// <exception cref="FormatException">Unknown version or a truncated file.</exception>
    public static IReadOnlyDictionary<uint, SteamAppInfo> ReadAppInfo(ReadOnlySpan<byte> appInfo, IReadOnlySet<uint> wanted)
    {
        var reader = new Reader(appInfo);
        var magic = reader.UInt32();
        if (magic is not (AppInfoV27 or AppInfoV28 or AppInfoV29))
        {
            throw new FormatException($"Unknown appinfo.vdf version 0x{magic:X8}.");
        }

        reader.Skip(4); // universe
        string[]? strings = null;
        if (magic == AppInfoV29)
        {
            strings = ReadStringTable(appInfo, reader.Int64());
        }

        var result = new Dictionary<uint, SteamAppInfo>();
        while (true)
        {
            var appId = reader.UInt32();
            if (appId == 0)
            {
                return result;
            }

            var size = (int)reader.UInt32();
            var next = reader.Position + size;
            if (wanted.Contains(appId))
            {
                // info state, last updated, PICS token, text SHA-1, change number, binary SHA-1 (v28+).
                reader.Skip(4 + 4 + 8 + 20 + 4 + (magic == AppInfoV27 ? 0 : 20));
                var common = reader.Map(strings).GetMap("appinfo")?.GetMap("common");
                // Some names end in a space ("Battlefield 3™ ").
                if (common?.GetString("name")?.Trim() is { Length: > 0 } name)
                {
                    result[appId] = new SteamAppInfo(name, (common.GetString("type") ?? string.Empty).ToLowerInvariant());
                }
            }

            reader.Seek(next);
        }
    }

    private static string[] ReadStringTable(ReadOnlySpan<byte> data, long offset)
    {
        if (offset < 0 || offset >= data.Length)
        {
            throw new FormatException("String table offset outside the file.");
        }

        var reader = new Reader(data);
        reader.Seek((int)offset);
        var count = reader.UInt32();
        var strings = new string[Math.Min(count, (uint)(data.Length - offset))];
        for (var i = 0; i < strings.Length; i++)
        {
            strings[i] = reader.String();
        }

        return strings;
    }

    /// <summary>A bounds-checked cursor; every overrun becomes a <see cref="FormatException"/>.</summary>
    private ref struct Reader(ReadOnlySpan<byte> data)
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

        public int Position { get; private set; }

        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
            {
                throw new FormatException("Entry runs past the end of the file.");
            }

            Position = position;
        }

        public void Skip(int count) => Seek(Position + count);

        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

        public string String()
        {
            var length = _data[Position..].IndexOf((byte)0);
            if (length < 0)
            {
                throw new FormatException("Unterminated string.");
            }

            var value = Encoding.UTF8.GetString(_data.Slice(Position, length));
            Position += length + 1;
            return value;
        }

        /// <param name="strings">The string table keys index into (appinfo v29), or null for inline keys.</param>
        public VdfMap Map(string[]? strings, int depth = 0)
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

                var key = Key(strings);
                object value = type switch
                {
                    TypeMap => Map(strings, depth + 1),
                    TypeString => String(),
                    TypeInt => BinaryPrimitives.ReadInt32LittleEndian(Take(4)),
                    TypeFloat or TypePointer or TypeColor => Take(4).Length,
                    TypeUInt64 or TypeInt64 => BinaryPrimitives.ReadInt64LittleEndian(Take(8)),
                    _ => throw new FormatException($"Unsupported VDF type 0x{type:X2}."),
                };
                map.Add(new(key, value));
            }
        }

        private string Key(string[]? strings)
        {
            if (strings is null)
            {
                return String();
            }

            var index = UInt32();
            return index < strings.Length ? strings[index] : throw new FormatException("String index outside the table.");
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            if (Position + count > _data.Length)
            {
                throw new FormatException("Unexpected end of file.");
            }

            var span = _data.Slice(Position, count);
            Position += count;
            return span;
        }
    }
}
