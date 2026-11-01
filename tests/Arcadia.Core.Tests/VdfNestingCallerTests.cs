using System.Text;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

/// <summary>
/// The player's side of #363: a crafted Steam file deep enough to overflow the stack is skipped like any broken file
/// by every reader Arcadia uses, so the scan, the install monitor and the SISR cleanup go on with the other files.
/// </summary>
public sealed class VdfNestingCallerTests : IDisposable
{
    // Deep enough to end the test host with a stack overflow if a reader had no limit.
    private const int CraftedDepth = 100_000;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-vdf-depth-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task The_scan_skips_a_crafted_app_manifest_and_keeps_the_other_games()
    {
        var steam = Path.Combine(_root, "Steam");
        Write(Path.Combine(steam, "steamapps", "appmanifest_239030.acf"), """
            "AppState"
            {
            	"appid"		"239030"
            	"name"		"Papers, Please"
            	"StateFlags"		"4"
            	"installdir"		"PapersPlease"
            	"LastOwner"		"76561197960278073"
            }
            """);
        Write(Path.Combine(steam, "steamapps", "appmanifest_100.acf"), Crafted("AppState"));

        var games = await new SteamScanner(() => new SteamInstallation(steam, null)).ScanAsync();

        Assert.Equal(["Papers, Please"], games.Select(g => g.Title));
    }

    [Fact]
    public void A_crafted_libraryfolders_vdf_falls_back_to_the_main_library()
    {
        var steam = Path.Combine(_root, "Steam");

        Assert.Equal([steam], SteamScanner.LibraryPaths(steam, Crafted("libraryfolders")));
    }

    [Fact]
    public void A_crafted_localconfig_gives_no_play_stats_and_no_steam_input_setting()
    {
        var localConfig = Crafted("UserLocalConfigStore");

        Assert.Empty(SteamScanner.ReadPlayStats(localConfig));
        Assert.Null(SteamInputSetting.Read(localConfig, 239030));
    }

    [Fact]
    public void The_install_monitor_treats_a_crafted_manifest_as_broken()
    {
        Assert.Null(SteamInstallMonitor.ParseState(Crafted("AppState")));
    }

    [Fact]
    public void A_crafted_loginusers_vdf_names_no_last_account()
    {
        var steam = Path.Combine(_root, "Steam");
        Write(Path.Combine(steam, "config", "loginusers.vdf"), Crafted("users"));

        Assert.Null(new SteamInstallation(steam, null).LastLoggedInUserId());
    }

    [Fact]
    public void A_cut_off_shortcuts_vdf_is_left_untouched_when_removing_an_entry()
    {
        var path = Path.Combine(_root, "shortcuts.vdf");
        var marker = Path.Combine(_root, "sisr.exe");
        SteamShortcuts.Add(path, "SISR Marker", marker);
        SteamShortcuts.Add(path, "Spiel", Path.Combine(_root, "spiel.exe"));
        File.Delete(path + ".bak");
        var whole = File.ReadAllBytes(path);
        var cutOff = whole[..^3]; // the ends of the last entry, of "shortcuts" and of the root are gone
        File.WriteAllBytes(path, cutOff);

        Assert.Throws<FormatException>(() => SteamShortcuts.RemoveByExecutable(path, marker));

        Assert.Equal(cutOff, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void A_crafted_shortcuts_vdf_is_rejected_by_every_shortcut_operation()
    {
        var path = Path.Combine(_root, "shortcuts.vdf");
        var crafted = CraftedBinary();
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(path, crafted);

        Assert.Throws<FormatException>(() => SteamShortcuts.Read(path));
        Assert.Throws<FormatException>(() => SteamShortcuts.Add(path, "Spiel", Path.Combine(_root, "spiel.exe")));
        Assert.Throws<FormatException>(() => SteamShortcuts.RemoveByExecutable(path, Path.Combine(_root, "spiel.exe")));
        Assert.Equal(crafted, File.ReadAllBytes(path));
    }

    private static string Crafted(string rootKey)
    {
        var text = new StringBuilder($"\"{rootKey}\" {{ ", CraftedDepth * 8);
        for (var i = 0; i < CraftedDepth; i++)
        {
            text.Append("\"m\" { ");
        }

        text.Append('}', CraftedDepth + 1);
        return text.ToString();
    }

    private static byte[] CraftedBinary()
    {
        var data = new List<byte>(CraftedDepth * 3 + 32);
        data.AddRange([0x00, .. Encoding.ASCII.GetBytes("shortcuts"), 0x00]);
        for (var i = 0; i < CraftedDepth; i++)
        {
            data.AddRange([0x00, (byte)'m', 0x00]);
        }

        data.AddRange(Enumerable.Repeat((byte)0x08, CraftedDepth + 2));
        return [.. data];
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
