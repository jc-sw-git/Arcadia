using Arcadia.ControllerSupport;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class ControllerSupportRemovalTests
{
    [Fact]
    [Trait("TestCase", "T243-37")]
    public void The_uninstall_question_lists_what_is_there_and_asks_once_for_all_of_it()
    {
        var text = ControllerSupportRemoval.Question(new InstalledParts(Sisr: true, Viiper: true, Usbip: true, HidHide: true), steamRunning: true, timeoutSeconds: 20);

        Assert.Contains("Treiber für die Controller-Emulation auch entfernen?", text);
        Assert.Contains("•  usbip-win2 (Treiber)\n•  HidHide (Treiber)\n•  SISR und VIIPER\n", text);
        Assert.Contains("einmal nach Administratorrechten", text);
        Assert.Contains("Steam wird dafür kurz beendet", text);
        Assert.Contains("Ohne Antwort innerhalb von 20 Sekunden bleibt alles installiert", text);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public void Without_drivers_the_question_names_no_administrator_prompt()
    {
        var text = ControllerSupportRemoval.Question(new InstalledParts(Sisr: false, Viiper: true, Usbip: false, HidHide: false), steamRunning: true, timeoutSeconds: 20);

        Assert.Contains("•  VIIPER\n", text);
        Assert.DoesNotContain("HidHide (Treiber)", text);
        Assert.DoesNotContain("Administratorrechten", text);
        Assert.DoesNotContain("Steam wird", text);
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
