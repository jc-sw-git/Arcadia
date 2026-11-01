using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public sealed class SteamControllerRoutingTests
{
    private readonly List<string> _active = [];
    private readonly HashSet<string> _onSteamInput = new(StringComparer.Ordinal);
    private readonly List<string> _log = [];
    private int? _steamAppId;
    private bool _driverReady = true;
    private Exception? _failure;

    private SteamControllerRouting Create() => new(
        () => _failure is { } failure ? throw failure : _active,
        key => !_onSteamInput.Contains(key),
        () => _steamAppId,
        () => _driverReady,
        _log.Add);

    [Theory]
    [InlineData(new string[0], true, true, false)]
    [InlineData(new[] { "epic:a" }, true, true, false)]
    [InlineData(new[] { "epic:a", "steam:570" }, true, false, true)]
    [InlineData(new string[0], false, false, false)]
    [InlineData(new[] { "epic:a" }, false, false, true)]
    [InlineData(new[] { "steam:570" }, false, false, true)]
    public void One_game_on_Steam_Eingabe_or_any_game_without_the_driver_hands_the_controller_to_Steam(string[] games, bool driverReady, bool padWanted, bool steamUses)
    {
        var route = SteamControllerRouting.Decide(games, key => key != "steam:570", driverReady);

        Assert.Equal((padWanted, steamUses), (route.PadWanted, route.SteamUsesController));
    }

    [Fact]
    public void A_Steam_game_running_in_Steam_counts_by_its_app_id_and_its_end_brings_the_pad_back()
    {
        var routing = Create();
        _onSteamInput.Add("steam:570");
        Assert.True(routing.Update().PadWanted);

        _steamAppId = 570;
        var running = routing.Update();
        Assert.False(running.PadWanted);
        Assert.True(running.SteamUsesController);
        Assert.True(running.GameRunning);

        _steamAppId = null;
        var ended = routing.Update();
        Assert.True(ended.PadWanted);
        Assert.False(ended.SteamUsesController);
        Assert.False(ended.GameRunning);
    }

    [Fact]
    public void Only_changes_are_logged()
    {
        var routing = Create();
        routing.Update();
        routing.Update();
        _active.Add("epic:a");
        routing.Update();

        Assert.Single(_log);
        Assert.Contains("Xbox-Pad von Arcadia", _log[0]);
    }

    [Fact]
    public void The_driver_coming_and_going_is_reported_but_not_the_first_look()
    {
        var routing = Create();
        var changes = 0;
        routing.DriverChanged += () => changes++;
        _driverReady = false;
        routing.Update();
        Assert.False(routing.DriverReady);
        Assert.Equal(0, changes);

        _driverReady = true;
        routing.Update();
        Assert.True(routing.DriverReady);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_failing_look_keeps_the_last_route_and_is_logged()
    {
        var routing = Create();
        var before = routing.Update();

        _failure = new InvalidOperationException("kaputt");
        Assert.Same(before, routing.Update());
        Assert.Contains(_log, line => line.Contains("kaputt", StringComparison.Ordinal));
    }

    public static TheoryData<bool, bool, bool, bool, bool, bool, bool> CopyCases => new()
    {
        // game on Steam-Eingabe, game on Arcadia, Arcadia in front, mouse mode, Steam in front → copy passes, pad passes
        { true, false, false, false, false, true, false },
        { true, false, true, false, false, false, false },
        { true, false, false, true, false, false, false },
        { false, true, false, false, false, false, true },
        { false, true, false, false, true, true, false },
        { false, true, true, false, false, false, false },
        { false, true, false, true, false, false, false },
        { false, false, false, false, true, true, false },
        { false, false, false, false, false, false, false },
        { false, false, true, false, true, false, false },
        { false, false, false, true, true, false, false },
    };

    [Theory]
    [MemberData(nameof(CopyCases))]
    [Trait("TestCase", "T242-2")]
    public void The_copy_passes_input_only_to_a_game_on_Steam_Eingabe_or_to_Steam_in_front_and_never_while_Arcadia_or_the_mouse_mode_has_the_controller(
        bool steamInputGame, bool arcadiaGame, bool arcadiaActive, bool mouseMode, bool steamInFront, bool copyPasses, bool padPasses)
    {
        var games = new List<string>();
        if (steamInputGame)
        {
            games.Add("steam:570");
        }

        if (arcadiaGame)
        {
            games.Add("epic:a");
        }

        var route = SteamControllerRouting.Decide(games, key => key != "steam:570", driverReady: true);
        // The game on "Arcadia" is in front whenever neither Arcadia nor Steam is.
        var now = new ForegroundNow(arcadiaActive, mouseMode, steamInFront, GameOnArcadiaInFront: arcadiaGame && !arcadiaActive && !steamInFront);

        Assert.Equal(copyPasses, SteamControllerCopyRules.CopyPasses(route, now, desktopLayoutIgnored: true));
        Assert.Equal(padPasses, SteamControllerCopyRules.PadPasses(route, now));
    }

    public static TheoryData<bool, bool, bool, bool, bool, bool, bool> CopyCasesWithSteamsDesktopLayout => new()
    {
        // game on Steam-Eingabe, game on Arcadia, Arcadia in front, mouse mode, Steam in front → copy passes, pad passes
        { true, false, false, false, false, true, false },
        { true, false, true, false, false, true, false },
        { true, false, false, true, false, true, false },
        { false, true, false, false, false, false, true },
        { false, true, false, false, true, true, false },
        { false, true, true, false, false, true, false },
        { false, true, false, true, false, true, false },
        { false, false, false, false, true, true, false },
        { false, false, false, false, false, true, false },
        { false, false, true, false, true, true, false },
        { false, false, true, false, false, true, false },
        { false, false, false, true, false, true, false },
    };

    [Theory]
    [MemberData(nameof(CopyCasesWithSteamsDesktopLayout))]
    [Trait("TestCase", "T315-2")]
    public void With_Steams_desktop_layout_not_ignored_the_copy_passes_everywhere_but_to_a_game_on_Arcadia_and_never_together_with_the_pad(
        bool steamInputGame, bool arcadiaGame, bool arcadiaActive, bool mouseMode, bool steamInFront, bool copyPasses, bool padPasses)
    {
        var games = new List<string>();
        if (steamInputGame)
        {
            games.Add("steam:570");
        }

        if (arcadiaGame)
        {
            games.Add("epic:a");
        }

        var route = SteamControllerRouting.Decide(games, key => key != "steam:570", driverReady: true);
        // The game on "Arcadia" is in front whenever neither Arcadia nor Steam is.
        var now = new ForegroundNow(arcadiaActive, mouseMode, steamInFront, GameOnArcadiaInFront: arcadiaGame && !arcadiaActive && !steamInFront);

        Assert.Equal(copyPasses, SteamControllerCopyRules.CopyPasses(route, now, desktopLayoutIgnored: false));
        Assert.Equal(padPasses, SteamControllerCopyRules.PadPasses(route, now));
    }

    [Fact]
    [Trait("TestCase", "T315-2")]
    public void Without_the_driver_the_copy_passes_on_the_desktop_only_while_Steams_desktop_layout_is_not_ignored()
    {
        var route = SteamControllerRouting.Decide([], _ => true, driverReady: false);
        var desktop = new ForegroundNow(ArcadiaActive: false, MouseModeOn: false, SteamInFront: false);

        Assert.False(SteamControllerCopyRules.CopyPasses(route, desktop, desktopLayoutIgnored: true));
        Assert.True(SteamControllerCopyRules.CopyPasses(route, desktop, desktopLayoutIgnored: false));
        Assert.False(SteamControllerCopyRules.PadPasses(route, desktop));
    }

    public static TheoryData<string, bool, bool, bool, bool, bool, bool> PadCases => new()
    {
        // where, a game on Arcadia runs, Arcadia in front, mouse mode, Steam in front, game on Arcadia in front → pad passes
        { "game on Arcadia in front", true, false, false, false, true, true },
        { "Steam in front, game running", true, false, false, true, false, false },
        { "desktop or browser, game running", true, false, false, false, false, false },
        { "desktop or browser, no game", false, false, false, false, false, false },
        { "Steam in front, no game", false, false, false, true, false, false },
        { "Arcadia in front, game running", true, true, false, false, false, false },
        { "mouse mode over the game", true, false, true, false, true, false },
        { "no foreground window, game running", true, false, false, false, false, false },
        { "game in front before the route knows it runs", false, false, false, false, true, false },
        { "Steam reported in front together with the game", true, false, false, true, true, false },
    };

    [Theory]
    [MemberData(nameof(PadCases))]
    [Trait("TestCase", "T357-1")]
    public void The_pad_passes_input_only_while_a_game_on_Arcadia_is_in_front(
        string where, bool gameOnArcadiaRuns, bool arcadiaActive, bool mouseMode, bool steamInFront, bool gameInFront, bool padPasses)
    {
        var route = SteamControllerRouting.Decide(gameOnArcadiaRuns ? ["epic:a"] : [], _ => true, driverReady: true);
        var now = new ForegroundNow(arcadiaActive, mouseMode, steamInFront, gameInFront);

        Assert.True(padPasses == SteamControllerCopyRules.PadPasses(route, now), where);
        foreach (var desktopLayoutIgnored in new[] { true, false })
        {
            Assert.False(padPasses && SteamControllerCopyRules.CopyPasses(route, now, desktopLayoutIgnored), $"{where}: pad and copy pass at once");
        }
    }

    [Fact]
    [Trait("TestCase", "T357-1")]
    public void A_game_on_Steam_Eingabe_in_front_never_gets_the_pad()
    {
        var route = SteamControllerRouting.Decide(["steam:570"], _ => false, driverReady: true);

        Assert.False(SteamControllerCopyRules.PadPasses(route, new ForegroundNow(ArcadiaActive: false, MouseModeOn: false, SteamInFront: false, GameOnArcadiaInFront: true)));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [Trait("TestCase", "T315-3")]
    public void The_copy_keeps_the_combo_from_Steam_only_while_the_mouse_mode_can_switch_and_Steams_desktop_layout_is_ignored(
        bool mouseModeCanSwitch, bool desktopLayoutIgnored, bool kept)
    {
        Assert.Equal(kept, SteamControllerCopyRules.CombosKeptFromSteam(mouseModeCanSwitch, desktopLayoutIgnored).BackAndStart);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [Trait("TestCase", "T238-4")]
    public void Another_controllers_pad_passes_in_front_of_Arcadia_and_in_the_mouse_mode_only_with_Steams_desktop_control(
        bool arcadiaActive, bool mouseModeOn, bool desktopLayoutIgnored, bool passes)
    {
        var now = new ForegroundNow(arcadiaActive, mouseModeOn, SteamInFront: false);
        Assert.Equal(passes, SteamControllerCopyRules.OtherControllerPasses(now, desktopLayoutIgnored));
        Assert.Equal(arcadiaActive || mouseModeOn, SteamControllerCopyRules.OtherControllerOnlyForSteam(now));
    }

    [Fact]
    [Trait("TestCase", "T315-4")]
    public void The_copys_gate_logs_each_change_of_its_answer_once_with_the_reason()
    {
        var log = new List<string>();
        var ignored = true;
        var arcadiaActive = true;
        var route = SteamControllerRouting.Decide([], _ => true, driverReady: true);
        var gate = new SteamControllerCopyGate(() => route, () => new ForegroundNow(arcadiaActive, false, false), () => ignored, log.Add);

        Assert.False(gate.Passes());
        Assert.False(gate.Passes());
        ignored = false;
        Assert.True(gate.Passes());
        Assert.True(gate.Passes());
        arcadiaActive = false;
        Assert.True(gate.Passes());

        Assert.Equal(
        [
            "Kopie für Steam: meldet Ruhe (Arcadia vorne, Steams Desktop-Belegung ignoriert)",
            "Kopie für Steam: gibt Eingaben weiter (Arcadia vorne, Steams Desktop-Belegung aktiv)",
        ], log);
    }

    [Fact]
    [Trait("TestCase", "T315-4")]
    public void The_copys_gate_lets_nothing_pass_when_asking_fails_or_before_the_route_is_decided()
    {
        var log = new List<string>();
        var undecided = new SteamControllerCopyGate(() => null, () => default, () => false, log.Add);
        var failing = new SteamControllerCopyGate(
            () => SteamControllerRouting.Decide([], _ => true, driverReady: true),
            () => throw new InvalidOperationException("Fenster weg"),
            () => false,
            _ => throw new IOException("Log voll"));

        Assert.False(undecided.Passes());
        Assert.False(failing.Passes());
        Assert.Equal(["Kopie für Steam: meldet Ruhe (noch nicht entschieden)"], log);
    }
}
