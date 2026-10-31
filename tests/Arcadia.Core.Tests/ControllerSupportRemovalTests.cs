using Arcadia.ControllerSupport;

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
}
