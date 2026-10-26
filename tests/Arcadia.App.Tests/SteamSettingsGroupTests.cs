using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The group "Steam" in the category "Controller" and the link to it from Steam's launcher page (#169).</summary>
public sealed class SteamSettingsGroupTests
{
    /// <summary>SISR as before #213 part B: these tests cover its switch and setup until #214 removes them.</summary>
    private static readonly StartArguments WithSisr = new(Sisr: true);

    private static readonly LauncherSpec[] WithSteam =
    [
        new(KnownLaunchers.Steam, 3),
        new(KnownLaunchers.Gog, 31),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T169-1")]
    [Trait("TestCase", "T169-3")]
    public async Task The_group_Steam_comes_first_and_the_controller_alone_reaches_every_row_in_its_order()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(steam: new FakeSteam(), arguments: WithSisr);
        var controller = OpenController(arcadia);

        var keys = controller.Rows.Select(row => row.Key).ToList();
        Assert.Equal(["sisr", "steam-desktop-layout", "mouse-mode"], keys.Take(3));
        Assert.All(controller.Rows.Take(2), row => Assert.Equal("STEAM", row.Group));
        Assert.DoesNotContain(controller.Rows, row => row.Group == "STEAM CONTROLLER");

        // ▼ walks the rows in this order, ▲ back; B leaves the settings.
        foreach (var key in keys.Skip(1))
        {
            arcadia.Press(UiAction.Down);
            Assert.Equal(key, arcadia.Main.Settings!.FocusedRow?.Key);
        }

        arcadia.Press(UiAction.Up, times: keys.Count - 1);
        Assert.Equal("sisr", arcadia.Main.Settings!.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-2")]
    public async Task Steams_desktop_layout_is_no_dependent_and_switches_with_SISR_off_and_the_mouse_mode_off()
    {
        var settings = SwitchedOn() with { SteamControllerSupport = false, MouseMode = new MouseModeSettings { Activation = MouseModeActivation.Off } };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: settings, steam: new FakeSteam(), arguments: WithSisr);
        var controller = OpenController(arcadia);
        var layout = controller.Rows.Single(row => row.Key == "steam-desktop-layout");

        Assert.Equal(0, layout.Indent);
        Assert.False(layout.IsDimmed);
        var before = arcadia.Settings.Current.IgnoreSteamDesktopLayout;
        arcadia.Press(UiAction.Down);
        Assert.Same(layout, arcadia.Main.Settings!.FocusedRow);
        arcadia.Press(before ? UiAction.Left : UiAction.Right);
        Assert.NotEqual(before, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-4")]
    [Trait("TestCase", "T169-5")]
    public async Task Steams_page_links_to_the_group_and_B_comes_back_to_the_link()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(WithSteam, steam: new FakeSteam(), arguments: WithSisr);
        var launchers = arcadia.OpenLauncherSettings();
        OpenPageOf(arcadia, launchers, "steam");

        var link = Assert.IsType<ActionRowViewModel>(launchers.Rows[^1]);
        Assert.Equal(("CONTROLLER", "Controller-Einstellungen für Steam", "Öffnen"), (link.Group, link.Title, link.ActionText));
        Assert.Contains(link.Info, block => block is InfoText { Text: "[A] springt dorthin, [B] kommt hierher zurück." });
        arcadia.Press(UiAction.Down, times: launchers.Rows.Count);
        Assert.Same(link, arcadia.Main.Settings!.FocusedRow);

        // A: the category "Controller" on "Steam Controller Kompatibilität".
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Controller", arcadia.Main.Settings.Selected.Name);
        Assert.Equal("sisr", arcadia.Main.Settings.FocusedRow?.Key);

        // B: back on Steam's page, on the link; B again: the list.
        arcadia.Press(UiAction.Back);
        Assert.Same(launchers, arcadia.Main.Settings.Selected);
        Assert.Equal("steam", launchers.PageLauncherId);
        Assert.Same(link, arcadia.Main.Settings.FocusedRow);
        arcadia.Press(UiAction.Back);
        Assert.Null(launchers.PageLauncherId);
        Assert.True(arcadia.Main.IsSettingsOpen);

        // The other launchers' pages have no link.
        OpenPageOf(arcadia, launchers, "gog");
        Assert.DoesNotContain(launchers.Rows, row => row.Key.StartsWith("link:", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-5")]
    public async Task After_another_category_was_chosen_B_on_the_link_target_closes_the_settings_as_usual()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(WithSteam, steam: new FakeSteam(), arguments: WithSisr);
        var launchers = arcadia.OpenLauncherSettings();
        OpenPageOf(arcadia, launchers, "steam");
        arcadia.Press(UiAction.Down, times: launchers.Rows.Count).Press(UiAction.Accept);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);

        arcadia.Press(UiAction.NextTab).Press(UiAction.PreviousTab);
        Assert.Equal("Controller", arcadia.Main.Settings.Selected.Name);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-6")]
    [Trait("TestCase", "T169-7")]
    public async Task Switched_on_the_row_shows_its_status_and_its_lamp_green_while_SISR_runs()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using (var off = await ArcadiaDriver.StartAsync(steam: steam, arguments: WithSisr))
        {
            var sisr = Sisr(OpenController(off));
            Assert.Equal("Erlaubt es, den Steam Controller auch in Spielen außerhalb von Steam zu verwenden", sisr.Subtitle);
            Assert.False(sisr.HasLamp);
        }

        steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: SwitchedOn(), steam: steam, arguments: WithSisr);
        var on = Sisr(OpenController(arcadia));
        Assert.Equal(RowLamp.Resting, on.Lamp);

        await StartSisrAsync(arcadia);
        await arcadia.WaitUntilAsync(() => on.Lamp == RowLamp.Working, "die Lampe wird grün");
        Assert.Equal("Aktiv – der Steam Controller läuft als Xbox Controller", on.Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-7")]
    [Trait("TestCase", "T169-8")]
    public async Task A_warning_turns_the_lamp_yellow_and_its_row_stands_indented_right_below()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: SwitchedOn(), steam: steam, arguments: WithSisr);
        await StartSisrAsync(arcadia);
        var controller = OpenController(arcadia);

        // Steam hanging at its login for over 30 s is a warning.
        steam.IsLoggedIn = false;
        await arcadia.TickControllerSupportAsync();
        arcadia.Wait(TimeSpan.FromSeconds(35));
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "issue:steam-login"), "die Meldung „Steam meldet sich noch an“");

        Assert.Equal(RowLamp.Problem, Sisr(controller).Lamp);
        Assert.Equal(["sisr", "issue:steam-login", "steam-desktop-layout"], controller.Rows.Take(3).Select(row => row.Key));
        Assert.Equal(1, controller.Rows[1].Indent);
        Assert.Equal(0, controller.Rows[2].Indent);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-7")]
    public async Task SISR_crashing_again_and_again_turns_the_lamp_red()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: SwitchedOn(), steam: steam, arguments: WithSisr);
        await StartSisrAsync(arcadia);
        var controller = OpenController(arcadia);

        for (var crash = 0; crash <= ControllerSupportService.MaxRestartsPerWindow && arcadia.ControllerSupport.State != SisrState.Failed; crash++)
        {
            steam.Sisr.Kill();
            await arcadia.TickControllerSupportAsync();
        }

        await arcadia.WaitUntilAsync(() => Sisr(controller).Lamp == RowLamp.Failure, "die Lampe wird rot");
        Assert.Contains(controller.Rows, row => row.Key == "issue:sisr-crashing" && row.Indent == 1);
    }

    private static AppSettings SwitchedOn() => new()
    {
        ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
        SteamControllerSupport = true,
    };

    private static async Task StartSisrAsync(ArcadiaDriver arcadia)
    {
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitOnClockUntilAsync(() => arcadia.ControllerSupport.State == SisrState.Running, "SISR läuft mit dem Steam Controller", TimeSpan.FromSeconds(10));
        await arcadia.TickControllerSupportAsync();
    }

    /// <summary>≡, then RB to "Controller".</summary>
    private static ControllerSettingsViewModel OpenController(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static SisrRowViewModel Sisr(ControllerSettingsViewModel controller) =>
        Assert.IsType<SisrRowViewModel>(controller.Rows.Single(row => row.Key == "sisr"));

    /// <summary>From the launcher list: ▼ to the launcher, A opens its page.</summary>
    private static void OpenPageOf(ArcadiaDriver arcadia, LauncherSettingsViewModel launchers, string launcherId)
    {
        arcadia.Press(UiAction.Up, times: launchers.Rows.Count);
        for (var row = 0; row < launchers.Rows.Count && arcadia.Main.Settings!.FocusedRow?.Key != launcherId; row++)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.Equal(launcherId, launchers.PageLauncherId);
    }
}
