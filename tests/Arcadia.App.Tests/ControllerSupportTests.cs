using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The Steam Controller's mode per game in the preview and on the detail page (#92, #127), and the header's symbols.</summary>
public sealed class ControllerSupportTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T15-10")]
    public async Task The_header_symbols_open_their_category_Controller_for_a_missing_part_and_Ueber_Arcadia_for_Arcadias_update()
    {
        var steam = new FakeSteam { IsSteamRunning = true, IsPanReady = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, releases: new FakeReleases("0.1.0", ("0.2.0", ["Neu"])));
        await arcadia.TickControllerSupportAsync();
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status is { HasControllerStatus: true, HasArcadiaUpdate: true }, "beide Symbole im Header");

        // The warning (Pan missing) opens "Controller", wherever the settings were.
        arcadia.Click(arcadia.Main.OpenControllerSettingsCommand);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);
        arcadia.Press(UiAction.Back);

        arcadia.Click(arcadia.Main.OpenAboutSettingsCommand);
        Assert.Equal("Über Arcadia", arcadia.Main.Settings!.Selected.Name);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T92-2")]
    public async Task The_preview_shows_Steam_Eingabe_for_Steam_games_and_Arcadia_for_all_others()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(
            [new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)], Launchers(KnownLaunchers.Steam, KnownLaunchers.Gog));
        arcadia.StartControllers();
        Assert.False(arcadia.Main.ShowControllerSupport, "Ohne Steam Controller zeigt die Vorschau keinen Modus.");

        // No switch any more (#213): the Steam Controller alone brings the mode.
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        Assert.True(arcadia.Main.ShowControllerSupport);
        Assert.Equal("Steam", arcadia.SelectedTab?.Name);
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("GOG Galaxy", arcadia.SelectedTab?.Name);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-2")]
    public async Task On_the_detail_page_the_game_settings_show_no_note_below_the_buttons_and_the_context_menu_says_what_they_switch_to()
    {
        var settings = Launchers(KnownLaunchers.Gog) with
        {
            MouseMode = new MouseModeSettings { Activation = MouseModeActivation.Manual, InGames = MouseModeInGames.PerGame },
        };
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 3)], settings);
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        Assert.True(arcadia.Main.ShowControllerSupport, "der Steam Controller ist erkannt");

        // The context menu names what each choice switches to.
        arcadia.Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!.Actions;
        Assert.Equal($"Wechselt zu „{ControllerModes.Steam}“", menu.Single(action => action.Kind == GameActionKind.ControllerMode).Note);
        Assert.Equal("Wechselt zu „Erlaubt“", menu.Single(action => action.Kind == GameActionKind.MouseMode).Note);
        arcadia.Press(UiAction.Back);

        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        var page = arcadia.Detail!.Current;
        Assert.False(page.HasNotes);

        // Down to the row of the game's settings: "Steam Controller", then "Individueller Mausmodus".
        arcadia.Press(UiAction.Down);
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedKind);
        Assert.False(page.HasNotes);
        arcadia.Press(UiAction.Right);
        Assert.Equal(GameActionKind.MouseMode, page.FocusedKind);
        Assert.False(page.HasNotes);
    }

    private static AppSettings Launchers(params LauncherInfo[] launchers) => new()
    {
        ActiveLaunchers = [.. launchers.Select(launcher => launcher.Id)],
    };
}
