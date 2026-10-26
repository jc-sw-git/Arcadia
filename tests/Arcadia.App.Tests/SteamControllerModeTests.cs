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

    /// <summary>T213-12: without usbip or VIIPER the mode is greyed out at "Steam-Eingabe", says why, and no pad comes.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-12")]
    public async Task Without_the_driver_the_mode_is_greyed_out_at_Steam_Eingabe_and_no_game_gets_a_pad()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] });
        arcadia.XboxPads.Missing = "Treiber usbip nicht geladen";
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        arcadia.Press(UiAction.NextTab);

        Assert.False(arcadia.Main.SteamControllerDriverReady);
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);
        Assert.Equal(ControllerModes.NeedsDriver, arcadia.Main.ControllerModeUnavailableReason);
        arcadia.Click(arcadia.Main.CycleControllerSupportCommand);
        Assert.Equal(ControllerModes.Steam, arcadia.Main.FocusedControllerSupportText);
        Assert.Empty(arcadia.Settings.Current.ControllerSupportOverrides);

        arcadia.Press(UiAction.Options);
        var mode = arcadia.Main.GameMenu!.Actions.Single(action => action.Kind == GameActionKind.ControllerMode);
        Assert.False(mode.IsEnabled);
        Assert.Equal(ControllerModes.NeedsDriver, mode.Note);
        arcadia.Press(UiAction.Back);
        Assert.Equal(0, arcadia.XboxPads.Plugged);

        // The driver installed meanwhile: the next tick offers the mode again.
        arcadia.XboxPads.Missing = null;
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.Main.SteamControllerDriverReady);
        Assert.Equal(ControllerModes.Arcadia, arcadia.Main.FocusedControllerSupportText);
        Assert.Equal(1, arcadia.XboxPads.Plugged);
    }

    /// <summary>T213-13: SISR is no longer started, also with the old switch on, and a SISR left running goes at the start.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-13")]
    public async Task SISR_is_no_longer_started_and_one_left_running_is_stopped()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings
        {
            ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
            SteamControllerSupport = true,
        }, steam: steam);

        steam.Sisr.Start();
        await arcadia.ControllerSupport.StopReplacedSisrAsync();
        Assert.False(steam.Sisr.IsRunning);

        await arcadia.TickControllerSupportAsync();
        arcadia.Wait(TimeSpan.FromSeconds(5));
        await arcadia.TickControllerSupportAsync();
        Assert.Equal(1, steam.Sisr.Starts);
        Assert.False(steam.Sisr.IsRunning);
    }
}
