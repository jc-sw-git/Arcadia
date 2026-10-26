using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>"Steam Controller Kompatibilität" in the category "Controller" (#85, #92), with Windows, Steam and SISR faked.</summary>
public sealed class ControllerSupportTests
{
    /// <summary>SISR as before #213 part B: these tests cover its switch and setup until #214 removes them.</summary>
    private static readonly StartArguments Sisr = new(Sisr: true);

    private static readonly TimeSpan PastSteamStartTimeout = TimeSpan.FromSeconds(65);

    [AvaloniaTheory]
    [Trait("TestCase", "T85-1")]
    [InlineData("SISR")]
    [InlineData("usbip-Treiber")]
    [InlineData("CEF-Debugging")]
    [InlineData("SISR Marker")]
    public async Task Without_SISR_or_what_it_needs_the_setting_offers_SISR_installieren_instead_of_the_switch(string missing)
    {
        var steam = new FakeSteam();
        using (var ready = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr))
        {
            Assert.True(OpenController(ready).Sisr.IsSwitch, "Mit allem, was SISR braucht, ist die Einstellung ein Schalter.");
        }

        steam = new FakeSteam();
        switch (missing)
        {
            case "SISR":
                steam.Sisr.IsInstalled = false;
                break;
            case "usbip-Treiber":
                steam.IsUsbipReady = false;
                break;
            case "CEF-Debugging":
                steam.IsCefDebuggingEnabled = false;
                break;
            default:
                steam.HasMarker = false;
                break;
        }

        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var sisr = OpenController(arcadia).Sisr;

        Assert.True(sisr.IsInstall);
        Assert.False(sisr.IsSwitch);
        Assert.Equal("Treiber installieren", sisr.ActivateText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-2")]
    public async Task With_Steam_running_SISR_installieren_asks_first_and_cancelling_installs_nothing()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        steam.Sisr.IsInstalled = false;
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (controller, _) = OpenController(arcadia);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.Same(controller, controller.Dialog);

        // Only the dialog's buttons take the focus; the rows underneath stay where they were.
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);
        arcadia.Press(UiAction.Down, times: 3);
        Assert.True(controller.CancelSteamStopItem.IsFocused);
        Assert.False(controller.ConfirmSteamStopItem.IsFocused);
        Assert.Equal("sisr", arcadia.Main.Settings!.FocusedRow?.Key);

        // "Abbrechen".
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Null(controller.Dialog);

        // A again asks again; B closes it like "Abbrechen", and the settings stay open.
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.ConfirmSteamStop, controller.Prompt);
        Assert.True(controller.ConfirmSteamStopItem.IsFocused);
        arcadia.Press(UiAction.Back);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.True(arcadia.Main.IsSettingsOpen);

        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Equal(0, steam.Sisr.Installs);
        Assert.False(steam.Sisr.IsInstalled);
        Assert.True(steam.IsSteamRunning);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T85-9")]
    [InlineData(false, true, "Momentan nur unter Windows verfügbar.")]
    [InlineData(true, false, "Benötigt Steam.")]
    public async Task Without_Windows_or_Steam_the_setting_is_greyed_out_says_why_and_A_does_nothing(bool windows, bool steamInstalled, string why)
    {
        var steam = new FakeSteam { IsWindows = windows, IsSteamInstalled = steamInstalled };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (controller, sisr) = OpenController(arcadia);

        Assert.Equal(SisrRowMode.Unavailable, sisr.Mode);
        Assert.True(sisr.IsDimmed);
        Assert.Equal(why, sisr.Subtitle);
        Assert.False(sisr.CanActivate);
        Assert.False(sisr.CanChange);

        arcadia.Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Left);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
        Assert.Equal(0, steam.StartRequests);
        Assert.Equal(0, steam.Sisr.Installs);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-11")]
    public async Task Steam_not_starting_within_a_minute_shows_Steam_startet_nicht_with_cancel_and_retry()
    {
        var steam = new FakeSteam { StartsWhenAsked = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (controller, sisr) = OpenController(arcadia);

        var switchedOnAt = arcadia.Time.GetUtcNow();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => controller.Prompt == SettingsPrompt.SteamStartFailed, "„Steam startet nicht“", PastSteamStartTimeout);
        Assert.True(arcadia.Time.GetUtcNow() - switchedOnAt >= TimeSpan.FromSeconds(60), "Arcadia wartet eine Minute auf Steam.");
        Assert.Equal("Steam startet nicht", controller.SteamStartFailedTitle);
        Assert.Equal("Erneut versuchen", controller.SteamStartFailedAction);
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
        Assert.False(sisr.IsOn);

        // "Abbrechen" leaves the option off and tries nothing more.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        arcadia.Wait(TimeSpan.FromSeconds(5));
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
        Assert.Equal(1, steam.StartRequests);

        // Switched on again it fails again; "Erneut versuchen" starts Steam once more, which comes up this time.
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => controller.Prompt == SettingsPrompt.SteamStartFailed, "„Steam startet nicht“ zum zweiten Mal", PastSteamStartTimeout);
        steam.StartsWhenAsked = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => steam.Sisr.IsRunning, "SISR startet nach „Erneut versuchen“", TimeSpan.FromSeconds(10));
        Assert.Equal(3, steam.StartRequests);
        Assert.True(arcadia.Settings.Current.SteamControllerSupport);
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-11")]
    public async Task Steam_hanging_at_its_login_when_switching_on_shows_the_prompt_and_SISR_does_not_start()
    {
        var steam = new FakeSteam { IsSteamRunning = true, IsLoggedIn = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (controller, _) = OpenController(arcadia);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => controller.Prompt == SettingsPrompt.SteamStartFailed, "„Steam meldet sich nicht an“", PastSteamStartTimeout);

        Assert.Equal("Steam meldet sich nicht an", controller.SteamStartFailedTitle);
        Assert.Equal("Steam neu starten", controller.SteamStartFailedAction);
        Assert.False(arcadia.Settings.Current.SteamControllerSupport);
        Assert.Equal(0, steam.Sisr.Starts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-14")]
    public async Task While_Steam_or_SISR_start_the_switch_is_locked_and_other_buttons_do_nothing()
    {
        var steam = new FakeSteam { StartsWhenAsked = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (controller, sisr) = OpenController(arcadia);

        // Steam starting.
        arcadia.Press(UiAction.Accept).Wait(TimeSpan.FromSeconds(5));
        Assert.True(sisr.IsLocked);
        Assert.False(sisr.CanChange);
        Assert.False(sisr.CanActivate);
        arcadia.Press(UiAction.Accept).Press(UiAction.Left).Press(UiAction.Right);
        Assert.Equal(1, steam.StartRequests);
        Assert.True(arcadia.Settings.Current.SteamControllerSupport, "Die Taste hat den Start nicht abgebrochen.");
        Assert.Equal(SettingsPrompt.None, controller.Prompt);

        // SISR starting: its status shows the spinner.
        steam.IsSteamRunning = true;
        await arcadia.WaitOnClockUntilAsync(() => arcadia.ControllerSupport.State == SisrState.Starting, "SISR startet", TimeSpan.FromSeconds(5));
        Assert.True(sisr.IsLocked);
        Assert.Contains(ShownText.Blocks(sisr.Info).OfType<InfoStatus>(), status => status.IsInTransition);
        arcadia.Press(UiAction.Left);
        Assert.True(arcadia.Settings.Current.SteamControllerSupport);

        // Started, the switch works again.
        await arcadia.WaitOnClockUntilAsync(() => !sisr.IsLocked, "SISR ist gestartet", TimeSpan.FromSeconds(10));
        Assert.True(sisr.CanChange);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T85-14")]
    [InlineData("Steam startet")]
    [InlineData("SISR startet")]
    public async Task While_Steam_or_SISR_start_B_leaves_the_settings_at_once(string starting)
    {
        var steam = new FakeSteam { StartsWhenAsked = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, arguments: Sisr);
        var (_, sisr) = OpenController(arcadia);
        arcadia.Press(UiAction.Accept).Wait(TimeSpan.FromSeconds(5));
        if (starting == "SISR startet")
        {
            steam.IsSteamRunning = true;
            await arcadia.WaitOnClockUntilAsync(() => arcadia.ControllerSupport.State == SisrState.Starting, "SISR startet", TimeSpan.FromSeconds(5));
        }

        Assert.True(sisr.IsLocked);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-14")]
    public async Task With_Steam_hanging_at_its_login_the_switch_stays_usable()
    {
        var steam = new FakeSteam { IsSteamRunning = true, IsLoggedIn = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, settings: SwitchedOn(), arguments: Sisr);
        await arcadia.TickControllerSupportAsync();
        var (_, sisr) = OpenController(arcadia);
        Assert.True(sisr.IsOn);

        Assert.False(sisr.IsLocked);
        Assert.True(sisr.CanChange);
        arcadia.Press(UiAction.Left);
        await arcadia.WaitOnClockUntilAsync(() => !arcadia.Settings.Current.SteamControllerSupport, "die Option ist aus", TimeSpan.FromSeconds(5));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T85-16")]
    public async Task Steam_hanging_at_its_login_for_over_30_seconds_shows_a_message_with_Steam_neu_starten_and_SISR_keeps_running()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, settings: SwitchedOn(), arguments: Sisr);
        await StartSisrAsync(arcadia);
        var (controller, _) = OpenController(arcadia);

        steam.IsLoggedIn = false;
        await arcadia.TickControllerSupportAsync();
        arcadia.Wait(TimeSpan.FromSeconds(25));
        await arcadia.TickControllerSupportAsync();
        Assert.DoesNotContain(controller.Rows, row => row.Title == "Steam meldet sich noch an");

        arcadia.Wait(TimeSpan.FromSeconds(10));
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Title == "Steam meldet sich noch an"), "die Meldung „Steam meldet sich noch an“");
        var message = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Title == "Steam meldet sich noch an"));
        Assert.Equal("Steam neu starten", message.ActionText);
        Assert.True(steam.Sisr.IsRunning);
        Assert.Equal(1, steam.Sisr.Starts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-10")]
    public async Task The_header_symbols_open_their_category_Controller_for_SISR_and_Ueber_Arcadia_for_Arcadias_update()
    {
        var steam = new FakeSteam { IsSteamRunning = true, HasMarker = false };
        steam.Sisr.LatestRelease = new Release("v1.1.0", new Version(1, 1, 0), []);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, settings: SwitchedOn(), releases: new FakeReleases("0.1.0", ("0.2.0", ["Neu"])), arguments: Sisr);
        await arcadia.TickControllerSupportAsync();
        await arcadia.Updates.CheckIfDueAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.Status is { HasControllerStatus: true, HasSisrUpdate: true, HasArcadiaUpdate: true }, "alle drei Symbole im Header");

        // The warning (marker missing) and SISR's update both open "Controller", wherever the settings were.
        arcadia.Click(arcadia.Main.OpenControllerSettingsCommand);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);
        arcadia.Press(UiAction.Back);

        arcadia.Click(arcadia.Main.OpenAboutSettingsCommand);
        Assert.Equal("Über Arcadia", arcadia.Main.Settings!.Selected.Name);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T92-1")]
    public async Task The_setting_is_called_Steam_Controller_Kompatibilitaet_with_both_modes_and_no_text_says_Steam_Controller_Support()
    {
        var steam = new FakeSteam { IsSteamRunning = true, HasMarker = false };
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam, settings: SwitchedOn(), arguments: Sisr);
        await arcadia.TickControllerSupportAsync();
        var (controller, sisr) = OpenController(arcadia);

        Assert.Equal("Steam Controller Kompatibilität", sisr.Title);
        Assert.StartsWith("Mit dieser Einstellung wird der Steam Controller als Xbox Controller erkannt.", Assert.IsType<InfoText>(sisr.Info[0]).Text);
        Assert.Contains(sisr.Info.OfType<InfoText>(), text => text.Text.Contains("SISR", StringComparison.Ordinal));
        var modes = ShownText.Blocks(sisr.Info).OfType<InfoCard>().ToList();
        Assert.Equal([ControllerModes.Arcadia, ControllerModes.Steam], modes.Select(card => card.Title));

        var texts = controller.Rows.SelectMany(ShownText.Of).Append(arcadia.Main.Status.ControllerStatusText).ToList();
        Assert.True(arcadia.Main.Status.HasControllerStatus);
        Assert.DoesNotContain(texts, text => text.Contains("Controller Support", StringComparison.OrdinalIgnoreCase));
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

    /// <summary>Only these launchers; the old switch stays off, as it means nothing since #213.</summary>
    private static AppSettings Launchers(params LauncherInfo[] launchers) => new()
    {
        ActiveLaunchers = [.. launchers.Select(launcher => launcher.Id)],
    };

    /// <summary>"Steam Controller Kompatibilität" on, as after a start with the option on.</summary>
    private static AppSettings SwitchedOn(params LauncherInfo[] launchers) => new()
    {
        ActiveLaunchers = launchers.Length > 0 ? [.. launchers.Select(launcher => launcher.Id)] : [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
        SteamControllerSupport = true,
    };

    /// <summary>The controller support's regular check starts SISR, which comes up with the Steam Controller.</summary>
    private static async Task StartSisrAsync(ArcadiaDriver arcadia)
    {
        await arcadia.TickControllerSupportAsync();
        await arcadia.WaitOnClockUntilAsync(() => arcadia.ControllerSupport.State == SisrState.Running, "SISR läuft mit dem Steam Controller", TimeSpan.FromSeconds(10));
        await arcadia.TickControllerSupportAsync();
    }

    /// <summary>≡, then RB to "Controller"; the focus is on "Steam Controller Kompatibilität".</summary>
    private static (ControllerSettingsViewModel Controller, SisrRowViewModel Sisr) OpenController(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        var sisr = Assert.IsType<SisrRowViewModel>(arcadia.Main.Settings.FocusedRow);
        return (controller, sisr);
    }
}
