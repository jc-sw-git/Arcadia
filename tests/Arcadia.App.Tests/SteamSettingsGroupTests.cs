using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The group "Steam" in the category "Controller" (#169); the link to it from Steam's launcher page went with #178.</summary>
public sealed class SteamSettingsGroupTests
{
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
        using var arcadia = await ArcadiaDriver.StartAsync(steam: new FakeSteam());
        var controller = OpenController(arcadia);

        var keys = controller.Rows.Select(row => row.Key).ToList();
        // "Controller wiederherstellen" (#243) stands below the switch, indented like its problems, while Pan is installed.
        Assert.Equal(["pan", "pan-restore", "steam-desktop-layout", "mouse-mode"], keys.Take(4));
        Assert.All(controller.Rows.Take(2), row => Assert.Equal("PAN", row.Group));
        Assert.Equal("STEAM", controller.Rows[2].Group);
        Assert.DoesNotContain(controller.Rows, row => row.Group == "STEAM CONTROLLER");

        // ▼ walks the rows in this order, ▲ back; B leaves the settings.
        foreach (var key in keys.Skip(1))
        {
            arcadia.Press(UiAction.Down);
            Assert.Equal(key, arcadia.Main.Settings!.FocusedRow?.Key);
        }

        arcadia.Press(UiAction.Up, times: keys.Count - 1);
        Assert.Equal("pan", arcadia.Main.Settings!.FocusedRow?.Key);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T169-2")]
    public async Task Steams_desktop_layout_is_no_dependent_and_switches_with_the_mouse_mode_off()
    {
        var settings = Launchers() with { MouseMode = new MouseModeSettings { Activation = MouseModeActivation.Off } };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: settings, steam: new FakeSteam());
        var controller = OpenController(arcadia);
        var layout = controller.Rows.Single(row => row.Key == "steam-desktop-layout");

        Assert.Equal(0, layout.Indent);
        Assert.False(layout.IsDimmed);
        var before = arcadia.Settings.Current.IgnoreSteamDesktopLayout;
        arcadia.Press(UiAction.Down, times: 2);
        Assert.Same(layout, arcadia.Main.Settings!.FocusedRow);
        arcadia.Press(before ? UiAction.Left : UiAction.Right);
        Assert.NotEqual(before, arcadia.Settings.Current.IgnoreSteamDesktopLayout);
    }

    [AvaloniaFact]
    public async Task Coming_back_to_the_launcher_tab_shows_the_list_not_the_page_left()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(WithSteam, steam: new FakeSteam());
        var launchers = arcadia.OpenLauncherSettings();
        OpenPageOf(arcadia, launchers, "steam");

        arcadia.Press(UiAction.NextTab).Press(UiAction.PreviousTab);
        Assert.Same(launchers, arcadia.Main.Settings!.Selected);
        Assert.Null(launchers.PageLauncherId);
        Assert.Equal("steam", arcadia.Main.Settings.FocusedRow?.Key);

        // A click on the tab does the same.
        OpenPageOf(arcadia, launchers, "steam");
        arcadia.Press(UiAction.NextTab);
        launchers.SelectCommand!.Execute(null);
        Assert.Null(launchers.PageLauncherId);
    }

    private static AppSettings Launchers() => new()
    {
        ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
    };

    /// <summary>≡, then RB to "Controller".</summary>
    private static ControllerSettingsViewModel OpenController(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

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
