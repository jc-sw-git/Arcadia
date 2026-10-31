using Arcadia.ControllerSupport;
using Arcadia.Input;
using Arcadia.Tests.Pads;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public class SteamControllerXboxPadTests
{
    private static readonly GamepadReading SteamController = new(7, PadButtons.None, default, default) { Type = PadType.Steam };
    private static readonly GamepadReading XboxPad = new(8, PadButtons.None, default, default);

    private readonly FakeTimeProvider _time = new();
    private readonly FakeVirtualXboxPads _pads = new();
    private readonly List<string> _log = [];
    private readonly List<string> _rumbles = [];
    private bool _wanted = true;
    private bool _forwarding = true;
    private readonly List<bool> _fast = [];
    private readonly SteamControllerXboxPad _pad;

    public SteamControllerXboxPadTests()
    {
        _pad = new SteamControllerXboxPad(_pads, _time, line => { lock (_log) _log.Add(line); })
        {
            Wanted = () => _wanted,
            ForwardingAllowed = () => _forwarding,
            Combos = () => new ComboWithholdingOptions(BackAndStart: true),
            Rumble = (padId, motors) => { lock (_rumbles) _rumbles.Add($"{padId}: {motors.Large}/{motors.Small}"); },
            Forwarding = _fast.Add,
        };
    }

    private string Log() { lock (_log) return string.Join("\n", _log); }

    /// <summary>#238 D9, QE finding of #353: 1 ms reads only while the pad gets the Steam Controller's states.</summary>
    [Fact]
    [Trait("TestCase", "T238-11")]
    public async Task Fast_reads_are_wanted_only_while_the_plugged_pad_gets_the_Steam_Controllers_states()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        _pad.Feed([SteamController]);
        Assert.Equal([true], _fast);

        _forwarding = false;
        _pad.Feed([SteamController]);
        _forwarding = true;
        _pad.Feed([SteamController]);
        _pad.Feed([XboxPad]);

        Assert.Equal([true, false, true, false], _fast);
    }

    [Fact]
    public async Task The_pad_is_plugged_in_only_while_a_Steam_Controller_is_connected_and_wanted()
    {
        _pad.Feed([XboxPad]);
        await _pad.TickAsync();
        Assert.Equal(0, _pads.Plugged);

        _pad.Feed([XboxPad, SteamController]);
        await _pad.TickAsync();
        Assert.Equal(1, _pads.Plugged);
        Assert.True(_pad.IsPlugged);

        _wanted = false;
        await _pad.TickAsync();
        Assert.False(_pad.IsPlugged);
        Assert.Equal(1, _pads.Unplugged);

        _wanted = true;
        await _pad.TickAsync();
        _pad.Feed([XboxPad]);
        await _pad.TickAsync();
        Assert.Equal(2, _pads.Unplugged);
        Assert.Contains("Xbox-Pad: abgesteckt", Log());
    }

    /// <summary>#238 D5: a game's rumble on Arcadia's pad reaches the Steam Controller, and stops when the pad goes.</summary>
    [Fact]
    public async Task A_games_rumble_reaches_the_Steam_Controller_and_stops_when_the_pad_goes()
    {
        _pad.Feed([XboxPad, SteamController]);
        await _pad.TickAsync();

        _pads.Current!.Rumble(200, 50);
        _wanted = false;
        await _pad.TickAsync();

        Assert.Equal(["7: 200/50", "7: 0/0"], _rumbles);
    }

    /// <summary>The Steam button is Steam's (#212): it reaches Steam through the copy, never as the pad's Guide.</summary>
    [Fact]
    public async Task The_Steam_button_never_becomes_the_pads_Guide()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();

        _pad.Feed([SteamController with { Guide = true, Buttons = PadButtons.A }]);

        var sent = await _pads.Current!.WaitForAsync(state => state.Buttons != 0);
        Assert.Equal(0x1000, sent.Buttons);
    }

    [Fact]
    public async Task What_the_Steam_Controller_does_reaches_the_pad_but_not_another_controller()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();

        _pad.Feed([XboxPad with { Buttons = PadButtons.B }, SteamController with
        {
            Buttons = PadButtons.A | PadButtons.RightTrigger,
            LeftStick = new StickPosition(1, -1),
            Triggers = new TriggerPosition(0, 1),
        }]);

        var sent = await _pads.Current!.WaitForAsync(state => state.Buttons != 0);
        Assert.Equal(new XboxPadState(0x1000, 0, 255, 32767, -32767, 0, 0), sent);
    }

    [Fact]
    public async Task While_forwarding_is_not_allowed_the_pad_is_neutral()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        _pad.Feed([SteamController with { Buttons = PadButtons.A }]);
        await _pads.Current!.WaitForAsync(state => state.Buttons == 0x1000);

        _forwarding = false;
        _pad.Feed([SteamController with { Buttons = PadButtons.A }]);
        await _pads.Current.WaitForAsync(state => state == XboxPadState.Neutral);
    }

    [Fact]
    public async Task A_lost_pad_is_plugged_in_again_and_after_too_many_losses_Arcadia_gives_up_once()
    {
        var gaveUp = new List<string>();
        _pad.GaveUp += gaveUp.Add;
        _pad.Feed([SteamController]);
        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow + 1; i++)
        {
            await _pad.TickAsync();
            _pads.Current!.Lose();
        }

        await _pad.TickAsync();
        await _pad.TickAsync();
        Assert.Equal(SteamControllerXboxPad.MaxReplugsPerWindow + 1, _pads.Plugged);
        Assert.Single(gaveUp);
        Assert.Contains("Verbindung zu VIIPER verloren", Log());
    }

    [Fact]
    [Trait("TestCase", "T243-39")]
    public async Task A_pad_Pan_took_away_comes_back_and_never_counts_towards_giving_up()
    {
        var gaveUp = new List<string>();
        _pad.GaveUp += gaveUp.Add;
        _pad.Feed([SteamController]);
        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow + 1; i++)
        {
            await _pad.TickAsync();
            _pads.Current!.TakeAway();
        }

        await _pad.TickAsync();
        Assert.True(_pad.IsPlugged);
        Assert.Empty(gaveUp);
        Assert.Contains("Xbox-Pad: von Pan entfernt", Log());
        Assert.DoesNotContain("Verbindung zu VIIPER verloren", Log());
    }

    [Fact]
    [Trait("TestCase", "T243-40")]
    public async Task Restoring_the_controllers_lets_Arcadia_try_again_after_it_gave_up()
    {
        var gaveUp = new List<string>();
        _pad.GaveUp += gaveUp.Add;
        _pad.Feed([SteamController]);
        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow; i++)
        {
            await _pad.TickAsync();
            _pads.Current!.Lose();
        }

        await _pad.TickAsync();
        Assert.False(_pad.IsPlugged);
        Assert.Contains("Controller wiederherstellen", Assert.Single(gaveUp));

        _pad.ForgetLosses();
        await _pad.TickAsync();
        Assert.True(_pad.IsPlugged);

        // The count starts afresh: the losses before the restore don't bring the next give-up closer.
        for (var i = 0; i < SteamControllerXboxPad.MaxReplugsPerWindow; i++)
        {
            _pads.Current!.Lose();
            await _pad.TickAsync();
        }

        Assert.True(_pad.IsPlugged);
        Assert.Single(gaveUp);
    }

    [Fact]
    public async Task Losses_further_apart_than_the_window_never_give_up()
    {
        _pad.Feed([SteamController]);
        for (var i = 0; i < 10; i++)
        {
            await _pad.TickAsync();
            _pads.Current!.Lose();
            _time.Advance(SteamControllerXboxPad.CrashWindow / 2);
        }

        await _pad.TickAsync();
        Assert.True(_pad.IsPlugged);
    }

    [Fact]
    public async Task A_failing_plug_in_is_logged_and_counts_as_a_loss_and_never_throws()
    {
        _pads.FailPlug = true;
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        Assert.False(_pad.IsPlugged);
        Assert.Contains("Anstecken fehlgeschlagen: kaputt", Log());

        _pads.FailPlug = false;
        await _pad.TickAsync();
        Assert.True(_pad.IsPlugged);
    }

    [Fact]
    public async Task A_plug_in_that_fails_because_the_pads_went_unavailable_is_no_loss()
    {
        var gaveUp = new List<string>();
        _pad.GaveUp += gaveUp.Add;
        _pads.FailPlug = true;
        _pads.MissingAfterFailedPlug = "Pan räumt noch auf";
        _pad.Feed([SteamController]);
        // Several teardowns in a row, each one failing the plug and leaving Pan "still cleaning up": none is a loss.
        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow + 1; i++)
        {
            _pads.Missing = null;
            await _pad.TickAsync();
            Assert.Equal("Pan räumt noch auf", _pads.Missing);
        }

        Assert.False(_pad.IsPlugged);
        Assert.Empty(gaveUp);
        Assert.Contains("Anstecken fehlgeschlagen: Pan räumt noch auf", Log());

        // Pan is done: the pad comes back without a word about losses.
        _pads.FailPlug = false;
        _pads.Missing = null;
        await _pad.TickAsync();
        Assert.True(_pad.IsPlugged);
        Assert.Empty(gaveUp);
    }

    [Fact]
    public async Task Without_VIIPER_or_the_driver_it_says_why_once()
    {
        _pads.Missing = "Treiber usbip nicht geladen";
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        await _pad.TickAsync();
        Assert.Equal(0, _pads.Plugged);
        Assert.Single(Log().Split('\n'), line => line.Contains("nicht möglich (Treiber usbip nicht geladen)"));
    }

    [Fact]
    public async Task Shutdown_unplugs_the_pad_and_ends_the_server()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        await _pad.ShutdownAsync();
        Assert.Equal(1, _pads.Unplugged);
        Assert.True(_pads.ShutDown);
    }

    [Fact]
    public async Task A_pad_plugged_in_while_a_button_is_held_gets_that_button_at_once()
    {
        _pad.Feed([SteamController with { Buttons = PadButtons.A }]);
        await _pad.TickAsync();
        await _pads.Current!.WaitForAsync(state => state.Buttons == 0x1000);
    }

    /// <summary>Codex review of #231: a failed unplug must not leave VIIPER's server running.</summary>
    [Fact]
    public async Task Shutdown_ends_the_server_even_when_unplugging_fails()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        _pads.FailUnplug = true;

        await _pad.ShutdownAsync();

        Assert.True(_pads.ShutDown);
        Assert.Contains("Abstecken beim Beenden fehlgeschlagen", Log());
    }

    /// <summary>Codex review of #231: a send that hangs counts as a lost pad, and the next tick plugs in a new one.</summary>
    [Fact]
    public async Task A_send_that_hangs_counts_as_a_lost_pad()
    {
        _pad.Feed([SteamController]);
        await _pad.TickAsync();
        _pads.StallSends = true;
        _pad.Feed([SteamController with { Buttons = PadButtons.A }]);

        for (var i = 0; i < 300 && _pads.Current!.IsConnected; i++)
        {
            await Task.Delay(10);
        }

        Assert.False(_pads.Current!.IsConnected);
        _pads.StallSends = false;
        await _pad.TickAsync();
        Assert.Equal(2, _pads.Plugged);
    }

    /// <summary>Codex review of #231: SDL gone for good lets go of everything the game got, without another poll.</summary>
    [Fact]
    public async Task Losing_the_gamepad_source_lets_go_of_what_was_held_and_unplugs_the_pad()
    {
        _pad.Feed([SteamController with { Buttons = PadButtons.A }]);
        await _pad.TickAsync();
        await _pads.Current!.WaitForAsync(state => state.Buttons == 0x1000);

        _pad.Drop();

        await _pads.Current.WaitForAsync(state => state == XboxPadState.Neutral);
        await _pad.TickAsync();
        Assert.False(_pad.IsPlugged);
    }
}
