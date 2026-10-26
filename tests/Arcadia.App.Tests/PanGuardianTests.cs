using Arcadia.App.Input;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The Xbox pad under Pan's watch (#243, part A): Pan itself is faked; what Arcadia does when Pan is missing, tears the
/// emulation down or breaks off is what a player would see. The real service is the manual part (T243-1 to T243-7).
/// </summary>
public sealed class PanGuardianTests
{
    private static async Task<ArcadiaDriver> StartInBackgroundAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-2")]
    public async Task Without_Pan_no_pad_is_plugged_in_and_the_mode_cannot_be_chosen()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Pan.Missing = "Pan ist nicht eingerichtet";
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;

        await arcadia.TickSteamControllerAsync();

        Assert.Equal(0, arcadia.XboxPads.Plugged);
        Assert.False(arcadia.Main.SteamControllerDriverReady);
        Assert.Contains("ohne Treiber", arcadia.ForGames.Routing.Current.Why);

        arcadia.Pan.Return();
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(1, arcadia.XboxPads.Plugged);
        Assert.True(arcadia.Main.SteamControllerDriverReady);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-3")]
    public async Task Every_pad_is_registered_with_Pan_and_every_poll_pulses_the_heartbeat()
    {
        using var arcadia = await StartInBackgroundAsync();

        Assert.Equal([1u], arcadia.Pan.Started);
        var before = arcadia.Pan.Pulses;
        arcadia.PollControllers();
        arcadia.PollControllers();
        Assert.Equal(before + 2, arcadia.Pan.Pulses);

        arcadia.IsArcadiaInFront = true;
        arcadia.PollControllers();
        Assert.Equal(before + 3, arcadia.Pan.Pulses);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-4")]
    public async Task A_teardown_by_Pan_shows_a_toast_and_the_pad_comes_back_with_the_next_generation()
    {
        using var arcadia = await StartInBackgroundAsync();

        arcadia.Pan.TearDown(PanReason.Hang);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == SteamControllerForGames.TornDownText(PanReason.Hang), "der Toast soll erscheinen");
        await arcadia.TickSteamControllerAsync();

        Assert.Equal([1u], arcadia.Pan.Acknowledged);
        Assert.Empty(arcadia.Pan.Stopped);
        Assert.Equal(1, arcadia.XboxPads.Unplugged);
        Assert.Equal(2, arcadia.XboxPads.Plugged);
        Assert.Equal([1u, 2u], arcadia.Pan.Started);
        Assert.Contains("Verbindung zu VIIPER verloren", ArcadiaDriver.Log);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-5")]
    public async Task A_lost_Pan_removes_the_pad_with_a_toast_and_it_returns_once_Pan_is_back()
    {
        using var arcadia = await StartInBackgroundAsync();

        arcadia.Pan.Lose("Pan antwortet nicht");
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == SteamControllerForGames.PanLostText, "der Toast soll erscheinen");
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(1, arcadia.XboxPads.Unplugged);
        Assert.Equal(1, arcadia.XboxPads.Plugged);
        Assert.False(arcadia.Main.SteamControllerDriverReady);

        arcadia.Pan.Return();
        await arcadia.TickSteamControllerAsync();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(2, arcadia.XboxPads.Plugged);
        Assert.True(arcadia.Main.SteamControllerDriverReady);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-6")]
    public async Task A_normal_exit_unplugs_the_pad_tells_Pan_and_says_goodbye()
    {
        using var arcadia = await StartInBackgroundAsync();

        // What the App does on exit: the pad and its server go, then Pan hears the planned exit.
        await arcadia.XboxPad.ShutdownAsync();

        Assert.Equal([1u], arcadia.Pan.Stopped);
        Assert.True(arcadia.Pan.PlannedExit);
    }
}
