using Arcadia.ControllerSupport;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class ControllerSupportRemovalTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\USBip\uninstall.exe""", @"C:\Program Files\USBip\uninstall.exe", "")]
    [InlineData(@"""C:\Program Files\USBip\uninstall.exe"" /S", @"C:\Program Files\USBip\uninstall.exe", "/S")]
    [InlineData(@"C:\USBip\uninstall.exe", @"C:\USBip\uninstall.exe", "")]
    public void Uninstall_command_is_split_into_file_and_arguments(string command, string file, string arguments)
    {
        Assert.Equal((file, arguments), ControllerSupportRemoval.SplitCommand(command));
    }

    [Fact]
    public void Removal_covers_the_roaming_data_of_SISR_and_VIIPER()
    {
        var names = ControllerSupportRemoval.DataDirectories.Select(Path.GetFileName);

        Assert.Equal(["SISR", "SISR.exe", "VIIPER"], names);
    }

    [Fact]
    public void The_marker_goes_from_every_account_that_has_it_and_nothing_else_goes()
    {
        var steamRoot = Path.Combine(Path.GetTempPath(), "arcadia-steam-" + Guid.NewGuid());
        try
        {
            var sisr = @"C:\Users\X\AppData\Local\SISR\SISR.exe";
            var current = Shortcuts(steamRoot, 111);
            var removedFromLogin = Shortcuts(steamRoot, 222);
            var withoutMarker = Shortcuts(steamRoot, 333);
            SteamShortcuts.Add(current, "SISR Marker", sisr, "--marker");
            SteamShortcuts.Add(current, "Spiel", @"D:\Spiele\spiel.exe");
            SteamShortcuts.Add(removedFromLogin, "SISR Marker", sisr, "--marker");
            SteamShortcuts.Add(withoutMarker, "Spiel", @"D:\Spiele\spiel.exe");

            var removed = ControllerSupportRemoval.RemoveMarkers(new SteamInstallation(steamRoot, 111), sisr);

            Assert.Equal(2, removed);
            Assert.Equal(["Spiel"], SteamShortcuts.Read(current).Select(s => s.Name));
            Assert.Empty(SteamShortcuts.Read(removedFromLogin));
            Assert.Equal(["Spiel"], SteamShortcuts.Read(withoutMarker).Select(s => s.Name));
        }
        finally
        {
            Directory.Delete(steamRoot, recursive: true);
        }
    }

    private static string Shortcuts(string steamRoot, uint accountId)
    {
        var config = Path.Combine(steamRoot, "userdata", accountId.ToString(System.Globalization.CultureInfo.InvariantCulture), "config");
        Directory.CreateDirectory(config);
        return Path.Combine(config, "shortcuts.vdf");
    }
}
