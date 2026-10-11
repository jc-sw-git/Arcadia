using System.Globalization;
using System.Text;

namespace Arcadia.Launchers.Steam;

/// <summary>One non-Steam game entry in Steam's library.</summary>
/// <param name="AppId">The id Steam stores for the entry. Used for <c>steam://rungameid</c>.</param>
/// <param name="Executable">Path to the executable, without Steam's surrounding quotes.</param>
public sealed record SteamShortcut(uint AppId, string Name, string Executable, string LaunchOptions)
{
    /// <summary>The 64-bit game id Steam uses for non-Steam entries in <c>steam://rungameid/&lt;id&gt;</c>.</summary>
    public ulong GameId => ((ulong)AppId << 32) | 0x02000000;

    public string RunUri => $"steam://rungameid/{GameId.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Reads and edits <c>userdata/&lt;user&gt;/config/shortcuts.vdf</c>.</summary>
public static class SteamShortcuts
{
    public static IReadOnlyList<SteamShortcut> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var root = BinaryVdf.Parse(File.ReadAllBytes(path));
        return (root.GetMap("shortcuts") ?? [])
            .Select(entry => entry.Value as VdfMap)
            .OfType<VdfMap>()
            .Select(ToShortcut)
            .ToList();
    }

    /// <summary>Finds the entry that starts <paramref name="executable"/>, comparing full paths case-insensitively.</summary>
    public static SteamShortcut? FindByExecutable(IEnumerable<SteamShortcut> shortcuts, string executable)
    {
        var wanted = Normalize(executable);
        return shortcuts.FirstOrDefault(s => string.Equals(Normalize(s.Executable), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Appends a non-Steam entry and returns it. Steam must not be running: it keeps its own copy
    /// in memory and overwrites the file on exit. The previous file is kept as <c>shortcuts.vdf.bak</c>.
    /// </summary>
    public static SteamShortcut Add(string path, string name, string executable, string launchOptions = "")
    {
        var root = File.Exists(path) ? BinaryVdf.Parse(File.ReadAllBytes(path)) : [];
        var shortcuts = root.GetMap("shortcuts");
        if (shortcuts is null)
        {
            shortcuts = [];
            root["shortcuts"] = shortcuts;
        }

        var quotedExe = Quote(executable);
        var appId = ShortcutAppId(quotedExe, name);
        var entry = new VdfMap
        {
            new("appid", unchecked((int)appId)),
            new("AppName", name),
            new("Exe", quotedExe),
            // Field names, order and StartDir format as Steam itself writes them.
            new("StartDir", Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(executable) ?? string.Empty) + Path.DirectorySeparatorChar),
            new("icon", string.Empty),
            new("ShortcutPath", string.Empty),
            new("LaunchOptions", launchOptions),
            new("IsHidden", 0),
            new("AllowDesktopConfig", 1),
            new("AllowOverlay", 1),
            new("openvr", 0),
            new("Devkit", 0),
            new("DevkitGameID", string.Empty),
            new("DevkitOverrideAppID", 0),
            new("LastPlayTime", 0),
            new("FlatpakAppID", string.Empty),
            new("sortas", string.Empty),
            new("tags", new VdfMap()),
        };

        var nextIndex = shortcuts.Count == 0
            ? 0
            : shortcuts.Select(s => int.TryParse(s.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : -1).Max() + 1;
        shortcuts.Add(new(nextIndex.ToString(CultureInfo.InvariantCulture), entry));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, BinaryVdf.Write(root));
        File.Move(temp, path, overwrite: true);
        return ToShortcut(entry);
    }

    /// <summary>
    /// Id for an entry Arcadia creates. Steam keeps the stored id, so this only needs to be
    /// stable and have the top bit set like Steam's own ids — entries added through Steam's UI
    /// get ids that don't follow this formula.
    /// </summary>
    public static uint ShortcutAppId(string quotedExe, string name) =>
        Crc32(Encoding.UTF8.GetBytes(quotedExe + name)) | 0x80000000;

    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    private static SteamShortcut ToShortcut(VdfMap entry) => new(
        unchecked((uint)(entry.GetInt("appid") ?? 0)),
        entry.GetString("AppName") ?? string.Empty,
        Unquote(entry.GetString("Exe") ?? string.Empty),
        entry.GetString("LaunchOptions") ?? string.Empty);

    private static string Quote(string text) => text.StartsWith('"') ? text : $"\"{text}\"";

    private static string Unquote(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;

    private static string Normalize(string path)
    {
        // Steam stores Windows paths; treat both slashes as separators on every OS so the
        // lookup behaves the same in tests on Linux/macOS as on Windows.
        var unquoted = Unquote(path.Trim()).Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(unquoted);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return unquoted;
        }
    }
}
