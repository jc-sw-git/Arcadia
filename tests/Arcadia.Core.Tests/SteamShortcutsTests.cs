using System.Text;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class SteamShortcutsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arcadia-tests-" + Guid.NewGuid());

    public SteamShortcutsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Crc32_matches_the_standard_check_value()
    {
        Assert.Equal(0xCBF43926u, SteamShortcuts.Crc32(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Vdf_round_trips_byte_for_byte()
    {
        // shortcuts -> "0" -> { appid = 0xB8057314, AppName = "A", tags = {} }
        byte[] bytes =
        [
            .. "\0shortcuts\0\00\0\u0002appid\0"u8,
            0x14, 0x73, 0x05, 0xB8,
            .. "\u0001AppName\0A\0\0tags\0\u0008\u0008\u0008\u0008"u8,
        ];

        Assert.Equal(bytes, BinaryVdf.Write(BinaryVdf.Parse(bytes)));
    }

    [Fact]
    public void Add_creates_a_readable_entry_and_keeps_existing_ones()
    {
        var path = Path.Combine(_dir, "config", "shortcuts.vdf");
        var exe = Path.Combine(_dir, "Arcadia.exe");

        SteamShortcuts.Add(path, "Other", Path.Combine(_dir, "other.exe"));
        var added = SteamShortcuts.Add(path, "Arcadia", exe);

        var all = SteamShortcuts.Read(path);
        Assert.Equal(["Other", "Arcadia"], all.Select(s => s.Name));
        Assert.Equal(added, SteamShortcuts.FindByExecutable(all, exe));
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal(0x80000000u, added.AppId & 0x80000000u);
    }

    [Fact]
    public void Remove_drops_matching_entries_and_renumbers_the_rest()
    {
        var path = Path.Combine(_dir, "config", "shortcuts.vdf");
        var sisr = Path.Combine(_dir, "SISR", "SISR.exe");

        SteamShortcuts.Add(path, "SISR Marker", sisr);
        SteamShortcuts.Add(path, "Other", Path.Combine(_dir, "other.exe"));
        SteamShortcuts.Add(path, "SISR Marker 2", sisr.ToUpperInvariant());

        Assert.Equal(2, SteamShortcuts.RemoveByExecutable(path, sisr));

        Assert.Equal(["Other"], SteamShortcuts.Read(path).Select(s => s.Name));
        var keys = BinaryVdf.Parse(File.ReadAllBytes(path)).GetMap("shortcuts")!.Select(e => e.Key);
        Assert.Equal(["0"], keys);
        Assert.Equal(0, SteamShortcuts.RemoveByExecutable(path, sisr));
    }

    [Fact]
    public void Executable_lookup_ignores_quotes_slashes_and_case()
    {
        var shortcut = new SteamShortcut(1, "X", "C:/Games/Arcadia/ARCADIA.exe", string.Empty);

        Assert.Same(shortcut, SteamShortcuts.FindByExecutable([shortcut], @"c:\games\arcadia\Arcadia.exe"));
    }

    [Fact]
    public void Game_id_puts_the_app_id_in_the_upper_half()
    {
        var shortcut = new SteamShortcut(3675479786, "Arcadia.App", "x.exe", string.Empty);

        Assert.Equal(15786065478012633088ul, shortcut.GameId);
        Assert.Equal("steam://rungameid/15786065478012633088", shortcut.RunUri);
    }

    /// <summary>Set ARCADIA_VDF_SAMPLE to a real shortcuts.vdf to check the codec against Steam's own bytes.</summary>
    [Fact]
    public void Real_file_round_trips_when_provided()
    {
        var sample = Environment.GetEnvironmentVariable("ARCADIA_VDF_SAMPLE");
        if (string.IsNullOrEmpty(sample))
        {
            return;
        }

        var bytes = File.ReadAllBytes(sample);
        Assert.Equal(bytes, BinaryVdf.Write(BinaryVdf.Parse(bytes)));
    }
}
