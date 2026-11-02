using Arcadia.ControllerSupport;
using Arcadia.Input;
using Arcadia.Tests.Pads;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>"Spiele sehen" lists the Steam Controller like every other connected controller, also while Pan hides.</summary>
public class SteamControllerSightingTests
{
    private static readonly GamepadReading SteamController = new(7, PadButtons.None, default, default) { Type = PadType.Steam };
    private static readonly GamepadReading XboxPad = new(8, PadButtons.None, default, default);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly SteamControllerXboxPad _pad;
    private int _changes;

    public SteamControllerSightingTests()
    {
        _pad = new SteamControllerXboxPad(new FakeVirtualXboxPads(), _time, _log.Add);
        _pad.Sighting.Changed += () => _changes++;
    }

    private SteamControllerSighting Sighting => _pad.Sighting;

    [Fact]
    public void Connecting_and_disconnecting_tell_the_list_once_each()
    {
        _pad.Feed([XboxPad]);
        Assert.False(Sighting.Connected);

        _pad.Feed([SteamController, XboxPad]);
        _pad.Feed([SteamController, XboxPad]);
        Assert.True(Sighting.Connected);
        Assert.Equal(1, _changes);

        _pad.Feed([XboxPad]);
        Assert.False(Sighting.Connected);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void It_stays_listed_while_Arcadia_lets_go_of_it_and_until_it_is_read_again()
    {
        _pad.Feed([SteamController]);
        Sighting.LetGo();
        _time.Advance(TimeSpan.FromSeconds(30));
        _pad.Feed([]);
        Assert.True(Sighting.Connected);

        Sighting.ReadAgain();
        _time.Advance(SteamControllerSighting.RereadGrace / 2);
        _pad.Feed([]);
        _pad.Feed([SteamController]);
        Assert.True(Sighting.Connected);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void Two_let_gos_at_once_keep_it_listed_until_both_ended()
    {
        _pad.Feed([SteamController]);
        Sighting.LetGo();
        Sighting.LetGo();
        Sighting.ReadAgain();
        _time.Advance(SteamControllerSighting.RereadGrace * 2);
        _pad.Feed([]);
        Assert.True(Sighting.Connected);

        Sighting.ReadAgain();
        _time.Advance(SteamControllerSighting.RereadGrace);
        _pad.Feed([]);
        Assert.False(Sighting.Connected);
    }

    [Fact]
    public void Unplugged_during_a_let_go_it_goes_once_the_grace_after_reading_again_is_over()
    {
        _pad.Feed([SteamController]);
        Sighting.LetGo();
        Sighting.ReadAgain();
        _pad.Feed([]);
        Assert.True(Sighting.Connected);

        _time.Advance(SteamControllerSighting.RereadGrace);
        _pad.Feed([]);
        Assert.False(Sighting.Connected);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void A_lost_gamepad_source_drops_it_at_once_even_during_a_let_go()
    {
        _pad.Feed([SteamController]);
        Sighting.LetGo();

        _pad.Drop();

        Assert.False(Sighting.Connected);
        _pad.Feed([]);
        Assert.False(Sighting.Connected);
    }

    [Fact]
    public void A_throwing_subscriber_is_logged_and_does_not_cost_the_poll_its_read()
    {
        _pad.Sighting.Changed += () => throw new InvalidOperationException("kaputt");

        _pad.Feed([SteamController]);

        Assert.True(Sighting.Connected);
        Assert.True(_pad.SteamControllerConnected);
        Assert.Contains(_log, line => line.Contains("kaputt", StringComparison.Ordinal));
    }
}
