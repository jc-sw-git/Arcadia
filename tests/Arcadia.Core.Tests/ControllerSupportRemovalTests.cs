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

    [Fact]
    [Trait("TestCase", "T407-1")]
    public void The_restart_notice_says_to_restart_before_installing_again()
    {
        Assert.Contains("Windows entfernt die Treiber für die Controller-Emulation erst beim nächsten Neustart des PCs ganz", ControllerSupportRemoval.RestartNotice);
        Assert.Contains("Starte den PC neu, bevor du Arcadia wieder installierst.", ControllerSupportRemoval.RestartNotice);
    }

    [Theory]
    [Trait("TestCase", "T407-1")]
    [InlineData(10, 48)]
    [InlineData(55, 3)]
    public void The_restart_notice_stays_open_for_what_is_left_of_the_hook_limit(int elapsedSeconds, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ControllerSupportRemoval.RestartNoticeTime(TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [Trait("TestCase", "T407-1")]
    [InlineData(56)]
    [InlineData(80)]
    public void Too_close_to_the_hook_limit_no_restart_notice_is_shown(int elapsedSeconds)
    {
        Assert.Null(ControllerSupportRemoval.RestartNoticeTime(TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromSeconds(60)));
    }

    [Fact]
    [Trait("TestCase", "T407-1")]
    public void The_restart_notice_shows_when_Pan_started_removing_the_drivers()
    {
        // Right after Pan's hand-off (#270) its copy hasn't touched the drivers yet, so their state can't decide this.
        Assert.True(ControllerSupportRemoval.RestartNoticeDue(removeDrivers: true, PanUninstallResult.Started));
    }

    [Theory]
    [Trait("TestCase", "T407-1")]
    [InlineData(true, PanUninstallResult.Declined)]
    [InlineData(true, PanUninstallResult.NotRun)]
    [InlineData(false, PanUninstallResult.Started)]
    [InlineData(false, PanUninstallResult.NotRun)]
    public void Without_drivers_on_their_way_out_no_restart_notice_shows(bool removeDrivers, PanUninstallResult pan)
    {
        Assert.False(ControllerSupportRemoval.RestartNoticeDue(removeDrivers, pan));
    }
}
