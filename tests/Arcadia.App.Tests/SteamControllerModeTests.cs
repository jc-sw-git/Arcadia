using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam Controller's mode per game, "Arcadia" or "Steam-Eingabe" (#213 part B): which running game decides whether
/// Arcadia's Xbox pad is plugged in, games started directly in Steam included (#119), the case without the driver, and
/// SISR no longer started. VIIPER and Steam are faked; whether Steam keeps away from the controller is #239's part.
/// </summary>
public sealed class SteamControllerModeTests
{
    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync(AppSettings? settings = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, settings ?? new AppSettings { ActiveLaunchers = ["steam", "gog"] });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    /// <summary>T213-9: A on a Steam game (Steam-Eingabe by default) unplugs the pad before Steam gets the start.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-9")]
    public async Task A_Steam_game_on_Steam_Eingabe_starts_without_Arcadias_pad_and_gets_it_back_when_it_ended()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        Assert.Equal(1, arcadia.XboxPads.Plugged);
        Assert.Equal("Steam", arcadia.SelectedTab?.Name);
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);
        var unpluggedAtStart = -1;
        arcadia.Starts.OnStart = _ => unpluggedAtStart = arcadia.XboxPads.Unplugged;

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "Steam soll das Spiel starten");
        Assert.Equal(1, unpluggedAtStart);
        Assert.False(arcadia.XboxPad.IsPlugged);

        // The next ticks while the game runs leave it out.
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(1, arcadia.XboxPads.Plugged);

        // The game is over (here: never seen running within the start's 2 minutes): the pad comes back.
        arcadia.Time.Advance(RunningGamesTracker.StartTimeout + TimeSpan.FromSeconds(1));
        await arcadia.FollowRunningGamesAsync();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(2, arcadia.XboxPads.Plugged);
    }

    /// <summary>T213-10: a game from another launcher on "Arcadia" (its default) keeps the pad; set to Steam-Eingabe, it doesn't.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-10")]
    public async Task A_game_from_another_launcher_keeps_the_pad_on_Arcadia_and_starts_without_it_on_Steam_Eingabe()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("GOG Galaxy", arcadia.SelectedTab?.Name);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);
        Assert.Equal(0, arcadia.XboxPads.Unplugged);

        // The next game of that launcher, switched to Steam-Eingabe in its preview first.
        arcadia.Press(UiAction.Right);
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 2, "GOG soll das zweite Spiel starten");
        Assert.False(arcadia.XboxPad.IsPlugged);
    }

    /// <summary>
    /// #156: the mode chosen against each launcher's default is stored as before (Steam game "Arcadia" = true, GOG game
    /// "Steam-Eingabe" = false) and keeps its meaning after a restart, now that the default comes from the launcher.
    /// </summary>
    [AvaloniaFact]
    public async Task A_mode_chosen_against_the_launchers_default_is_stored_as_before_and_kept_after_a_restart()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        var steamGame = arcadia.Main.Library.FocusedGame!.Game.Key;
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        arcadia.Press(UiAction.NextTab);
        var gogGame = arcadia.Main.Library.FocusedGame!.Game.Key;
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);

        var saved = new SettingsStore(arcadia.SettingsPath).Load().ControllerSupportOverrides;
        Assert.Equal(new Dictionary<string, bool> { [steamGame] = true, [gogGame] = false }, saved);

        using var restarted = await arcadia.RestartAsync();
        restarted.Sdl.Plug(KnownPads.SteamController);
        restarted.StartControllers();
        await restarted.TickSteamControllerAsync();
        Assert.Equal("GOG Galaxy", restarted.SelectedTab?.Name); // the last tab comes back
        Assert.Equal(gogGame, restarted.Main.Library.FocusedGame!.Game.Key);
        Assert.Equal(ControllerModes.Steam, restarted.Main.FocusedControllerSupportText);
        restarted.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Arcadia, restarted.Main.FocusedControllerSupportText); // the next GOG game keeps the default
        restarted.Press(UiAction.PreviousTab);
        Assert.Equal(steamGame, restarted.Main.Library.FocusedGame!.Game.Key);
        Assert.Equal(ControllerModes.Arcadia, restarted.Main.FocusedControllerSupportText);
        restarted.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Steam, restarted.Main.FocusedControllerSupportText);
    }

    /// <summary>
    /// T242-2 and T242-3 (#242 part C): what the App tells the copy, asked per report on the copy's own thread. Steam
    /// reads every input with Steam in front and no game, and in a game on "Steam-Eingabe"; at rest otherwise.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T242-2")]
    public async Task The_copy_passes_input_only_where_Steam_has_the_controller()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        Assert.Same(arcadia.ForGames.CopyFilter, arcadia.CopyWiring.Filter);
        arcadia.IsArcadiaInFront = true;
        Assert.False(arcadia.CopyWiring.Forwarding());

        arcadia.IsArcadiaInFront = false;
        arcadia.IsSteamInFront = true;
        Assert.True(arcadia.CopyWiring.Forwarding());
        arcadia.IsMouseModeOn = true;
        Assert.False(arcadia.CopyWiring.Forwarding());
        arcadia.IsMouseModeOn = false;

        // A game on "Arcadia" (GOG): Steam gets the copy at rest, also with Steam's window in front.
        arcadia.IsArcadiaInFront = true;
        arcadia.Press(UiAction.NextTab);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "GOG soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();
        arcadia.IsArcadiaInFront = false;
        Assert.False(arcadia.CopyWiring.Forwarding());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T242-3")]
    public async Task In_a_game_on_Steam_Eingabe_the_copy_passes_input_until_Arcadia_comes_to_the_front()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "Steam soll das Spiel starten");
        await arcadia.TickSteamControllerAsync();
        arcadia.IsArcadiaInFront = false;
        arcadia.IsSteamInFront = false;
        Assert.True(arcadia.CopyWiring.Forwarding());

        arcadia.IsArcadiaInFront = true;
        Assert.False(arcadia.CopyWiring.Forwarding());
    }

    /// <summary>T213-11 (#119): a Steam game started directly in Steam takes its mode as well, also without being in the library.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-11")]
    public async Task A_Steam_game_started_directly_in_Steam_takes_the_pad_away_unless_it_is_on_Arcadia()
    {
        var dota = TestGames.Installed(KnownLaunchers.Steam, "Dota 2", ArcadiaDriver.Now.AddDays(-3)) with { Id = "570" };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Steam, dota)], new AppSettings { ActiveLaunchers = ["steam"] });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);

        // Not in Arcadia's library, Steam-Eingabe by default.
        arcadia.RunningSteamAppId = 4_000_000;
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.XboxPad.IsPlugged);

        arcadia.RunningSteamAppId = null;
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);

        // A Steam game of the library, set to "Arcadia" in Arcadia, then started in Steam.
        Assert.Equal("Dota 2", arcadia.Main.Library.FocusedGame!.Title);
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);
        arcadia.RunningSteamAppId = 570;
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);
        Assert.Equal(1, arcadia.XboxPads.Unplugged);
    }

    /// <summary>
    /// T213-12, T242-10: without usbip or VIIPER Pan's emulation doesn't run, so the mode is greyed out at "Steam-Eingabe"
    /// with a triangle, no game gets a pad, and [A] on it leads to the controller settings on the row that says why.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-12")]
    [Trait("TestCase", "T242-10")]
    public async Task Without_the_driver_the_mode_is_greyed_out_at_Steam_Eingabe_and_leads_to_why()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        arcadia.XboxPads.Missing = "Treiber usbip nicht geladen";
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        arcadia.Press(UiAction.NextTab);

        Assert.True(arcadia.Main.ShowControllerSupport);
        Assert.True(arcadia.Main.ControllerModeBroken);
        Assert.True(arcadia.Main.Status.HasControllerStatus, "das Dreieck im Header");
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);
        Assert.Equal(ControllerModes.PanNotRunning, arcadia.Main.ControllerModeUnavailableReason);
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings?.Selected);
        Assert.Equal("pan:start-failed", controller.Rows.Single(row => row.IsFocused).Key);
        Assert.Equal("Treiber usbip nicht geladen", controller.Rows.Single(row => row.IsFocused).Subtitle);
        Assert.Empty(arcadia.Settings.Current.ControllerSupportOverrides);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);

        arcadia.Press(UiAction.Options);
        var mode = arcadia.Main.GameMenu!.Actions.Single(action => action.Kind == GameActionKind.ControllerMode);
        Assert.True(mode.IsEnabled, "fokussierbar");
        Assert.True(mode.IsBroken);
        Assert.Equal(ControllerModes.PanNotRunning, mode.Note);
        mode.Invoke();
        Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings?.Selected);
        arcadia.Press(UiAction.Back);
        Assert.Equal(0, arcadia.XboxPads.Plugged);
        Assert.Empty(arcadia.Settings.Current.ControllerSupportOverrides);

        // The driver installed meanwhile: the next tick offers the mode again.
        arcadia.XboxPads.Missing = null;
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.Main.ControllerModeBroken);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);
        Assert.Equal(1, arcadia.XboxPads.Plugged);
    }

    /// <summary>
    /// T213-13, T214-10: SISR is never started, and at the start only an
    /// SISR the older Arcadia started goes; one the user started stays.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-13")]
    [Trait("TestCase", "T214-10")]
    public async Task SISR_is_no_longer_started_and_only_the_one_the_older_Arcadia_left_running_is_stopped()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        steam.Sisr.WithSisr();
        var sisrLog = Path.Combine(steam.Sisr.LogDirectory, "sisr.log");
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe, id: 5, $"\"{steam.Sisr.SisrExe}\" {SisrInstallation.ArcadiaApiArgument} --log.file \"{sisrLog}\"");
        steam.Sisr.System.RunSisr(steam.Sisr.SisrExe, id: 6);
        using var arcadia = await ArcadiaDriver.StartAsync(steam: steam);

        await arcadia.ControllerSupport.StopSisrStartedByOldArcadiaAsync();
        await arcadia.TickControllerSupportAsync();
        arcadia.Wait(TimeSpan.FromSeconds(5));
        await arcadia.TickControllerSupportAsync();

        Assert.Equal([5], steam.Sisr.System.Stopped);
        Assert.Equal([6], steam.Sisr.System.Processes("SISR").Select(process => process.Id));
    }
}
