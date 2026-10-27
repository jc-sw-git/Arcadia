using System.Text;

namespace Arcadia.Pan;

/// <summary>
/// HidHide's driver interface as its own client and CLI use it (nefarius/HidHide, stable since 1.2): the control
/// device <c>\\.\HidHide</c>, buffered IOCTLs of device type 32769, lists as double-null-terminated UTF-16 strings,
/// switches as one byte. Kept free of Windows so the encoding is tested everywhere.
/// </summary>
public static class HidHideWire
{
    public const string ControlDevice = @"\\.\HidHide";

    private const uint DeviceType = 32769;
    private const uint MethodBuffered = 0;
    private const uint FileReadData = 1;

    public static readonly uint GetAllowList = Ioctl(2048);
    public static readonly uint SetAllowList = Ioctl(2049);
    public static readonly uint GetHiddenList = Ioctl(2050);
    public static readonly uint SetHiddenList = Ioctl(2051);
    public static readonly uint GetCloaking = Ioctl(2052);
    public static readonly uint SetCloaking = Ioctl(2053);
    public static readonly uint GetInverse = Ioctl(2054);

    /// <summary>Windows' CTL_CODE macro.</summary>
    public static uint Ioctl(uint function) => (DeviceType << 16) | (FileReadData << 14) | (function << 2) | MethodBuffered;

    /// <summary>The strings as REG_MULTI_SZ: each null-terminated, one more null at the end (two for an empty list).</summary>
    public static byte[] EncodeList(IReadOnlyList<string> entries)
    {
        if (entries.Any(entry => entry.Length == 0 || entry.Contains('\0')))
        {
            throw new ArgumentException("an entry is empty or contains a null character", nameof(entries));
        }

        var text = entries.Count == 0 ? "\0\0" : string.Concat(entries.Select(entry => entry + "\0")) + "\0";
        return Encoding.Unicode.GetBytes(text);
    }

    /// <summary>The strings of a REG_MULTI_SZ buffer; stops at the first empty string, tolerates a missing terminator.</summary>
    public static List<string> DecodeList(ReadOnlySpan<byte> buffer)
    {
        var text = Encoding.Unicode.GetString(buffer[..(buffer.Length & ~1)]);
        var entries = new List<string>();
        foreach (var entry in text.Split('\0'))
        {
            if (entry.Length == 0)
            {
                break;
            }

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// The driver file from the service's <c>ImagePath</c> (<c>\SystemRoot\System32\drivers\HidHide.sys</c>, a
    /// relative <c>System32\…</c>, <c>\??\C:\…</c>, or a plain path in the driver store), or null if it isn't one.
    /// </summary>
    public static string? DriverFile(string imagePath, string windowsDirectory)
    {
        var path = imagePath.Trim().Trim('"');
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            path = windowsDirectory.TrimEnd('\\') + "\\" + path[@"\SystemRoot\".Length..];
        }
        else if (path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            path = path[4..];
        }
        else if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
        {
            path = windowsDirectory.TrimEnd('\\') + "\\" + path;
        }

        return path.Length > 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' ? path : null;
    }

    /// <summary>
    /// HidHide was uninstalled and Windows removes it only at the next restart (owner's B2 test, 2026-10-27): until then
    /// its service key and driver file stay, so its version still reads as installed. Windows marks such a service with
    /// <c>DeleteFlag</c> = 1, and a driver file in use is listed in <c>PendingFileRenameOperations</c> as a source with
    /// an empty target (pairs of <c>\??\path</c> and target).
    /// </summary>
    public static bool RemovalPending(int? deleteFlag, IReadOnlyList<string>? pendingFileOperations, string? driverFile)
    {
        if (deleteFlag is { } flag && flag != 0)
        {
            return true;
        }

        if (pendingFileOperations is null || driverFile is null)
        {
            return false;
        }

        for (var i = 0; i + 1 < pendingFileOperations.Count; i += 2)
        {
            var source = pendingFileOperations[i].StartsWith(@"\??\", StringComparison.Ordinal) ? pendingFileOperations[i][4..] : pendingFileOperations[i];
            if (pendingFileOperations[i + 1].Length == 0 && string.Equals(source, driverFile, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
