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
        { false, true, false, false, true, false, true },
        { false, true, true, false, false, false, false },
        { false, true, false, true, false, false, false },
        { false, false, false, false, true, true, false },
        { false, false, false, false, false, false, true },
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
        var now = new ForegroundNow(arcadiaActive, mouseMode, steamInFront);

        Assert.Equal(copyPasses, SteamControllerCopyRules.CopyPasses(route, now));
        Assert.Equal(padPasses, SteamControllerCopyRules.PadPasses(route, now));
    }
}
