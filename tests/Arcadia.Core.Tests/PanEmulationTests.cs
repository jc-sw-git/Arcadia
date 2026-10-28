using Arcadia.ControllerSupport.Pan;
using Arcadia.Input;
using Arcadia.Pan;
using Arcadia.Tests.Pads;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// Pan's emulation of the Steam Controller (#242): hide first, then the copy for Steam, then Arcadia's Xbox pad within
/// the same generation; undone in reverse; no hiding, no emulation; hiding lost ends everything at once and comes back.
/// </summary>
public sealed class PanEmulationTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private readonly List<string> _log = [];
    private readonly List<string> _notices = [];
    private readonly FakeVirtualXboxPads _pads = new();
    private readonly FakeEmulationGuardian _pan = new();
    private readonly FakeSteamControllerHold _copy = new();
    private readonly PanEmulation _emulation;
    private bool _enabled = true;
    private bool _present = true;

    public PanEmulationTests()
    {
        _pads.Events = _events;
        _pan.Events = _events;
        _copy.Events = _events;
        _emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            Enabled = () => _enabled,
            SteamControllerPresent = () => _present,
        };
        _emulation.Notice += _notices.Add;
    }

    private PanEmulationState State => _emulation.Status.State;

    [Fact]
    [Trait("TestCase", "T242-4")]
    public async Task It_hides_first_then_attaches_the_copy_and_only_then_lets_the_pad_in_within_the_same_generation()
    {
        Assert.NotNull(_emulation.Unavailable());

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal(["started 1", "hide 1", "copy attached"], _events);
        Assert.Equal(1, _pads.Prepared);
        Assert.Null(_emulation.Unavailable());

        var pad = await _emulation.PlugAsync(CancellationToken.None);
        await pad.DisposeAsync();
        await _emulation.PlugAsync(CancellationToken.None);

        // The pad comes and goes within the emulation: no new generation, no hide or show.
        Assert.Equal([1u], _pan.Started);
        Assert.Equal([1u], _pan.Hidden);
        Assert.Empty(_pan.Stopped);
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task At_exit_pad_and_copy_go_before_the_puck_is_shown_then_the_emulation_ends_and_Pan_hears_the_planned_exit()
    {
        await _emulation.TickAsync();
        await _emulation.PlugAsync(CancellationToken.None);
        _events.Clear();

        await _emulation.ShutdownAsync();

        Assert.Equal(["pad unplugged", "copy detached", "unhide 1", "stopped 1"], _events);
        Assert.True(_pads.ShutDown);
        Assert.True(_pan.PlannedExit);
    }

    [Fact]
    [Trait("TestCase", "T242-5")]
    public async Task Switched_off_it_ends_in_Pans_order_and_nothing_is_emulated()
    {
        await _emulation.TickAsync();
        _events.Clear();
        _enabled = false;

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Off, State);
        Assert.Equal(["copy detached", "unhide 1", "stopped 1"], _events);
        Assert.NotNull(_emulation.Unavailable());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _emulation.PlugAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(PanReason.HidHideMissing)]
    [InlineData(PanReason.HidHideUnsupported)]
    [InlineData(PanReason.HidHideInverseMode)]
    [InlineData(PanReason.HidHideBusy)]
    [InlineData(PanReason.HidHideCloakingOffWithForeignEntries)]
    [Trait("TestCase", "T242-6")]
    public async Task Without_working_hiding_nothing_is_emulated_neither_pad_nor_copy(PanReason reason)
    {
        _pan.Hiding = reason;

        await _emulation.TickAsync();

        Assert.Equal(new PanEmulationStatus(PanEmulationState.Paused, PanEmulationProblem.Hiding, reason, $"HidHide: {reason}"), _emulation.Status);
        Assert.True(_emulation.Status.HasProblem);
        Assert.Empty(_pan.Started);
        Assert.Equal(0, _copy.Attached);
        Assert.NotNull(_emulation.Unavailable());

        // Hiding works again: it starts by itself, with no notice (it never ran).
        _pan.Hiding = PanReason.None;
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        Assert.Empty(_notices);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_hide_that_fails_ends_the_emulation_and_is_tried_again_after_ten_seconds()
    {
        _pan.HideResult = PanReason.HidingFailed;

        await _emulation.TickAsync();

        Assert.Equal((PanEmulationState.Paused, PanEmulationProblem.Hiding, PanReason.HidingFailed), (State, _emulation.Status.Problem, _emulation.Status.Hiding));
        Assert.Equal(["started 1", "hide 1", "stopped 1"], _events);

        _pan.HideResult = PanReason.None;
        _time.Advance(TimeSpan.FromSeconds(4));
        await _emulation.TickAsync();
        Assert.Single(_pan.Started);

        _time.Advance(PanEmulation.HideRetry);
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal([1u, 2u], _pan.Started);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_copy_that_fails_to_come_up_shows_the_puck_again_and_counts_as_a_failure()
    {
        _copy.FailAttach = "VIIPER antwortet nicht";

        await _emulation.TickAsync();

        Assert.Equal(new PanEmulationStatus(PanEmulationState.Paused, PanEmulationProblem.CopyFailed, Reason: "VIIPER antwortet nicht"), _emulation.Status);
        Assert.Equal(["started 1", "hide 1", "unhide 1", "stopped 1"], _events);
    }

    [Fact]
    [Trait("TestCase", "T242-7")]
    public async Task Hiding_lost_while_running_ends_pad_and_copy_at_once_with_a_notice_and_it_comes_back_by_itself()
    {
        await _emulation.TickAsync();
        var pad = await _emulation.PlugAsync(CancellationToken.None);
        _events.Clear();

        // Someone unticks the puck in HidHide's configuration client mid-game.
        _pan.HidingHolds = PanReason.HidingLost;
        await _emulation.TickAsync();

        Assert.Equal(["pad unplugged", "copy detached", "unhide 1", "stopped 1"], _events);
        Assert.False(pad.IsConnected);
        Assert.True(pad.TakenAway);
        Assert.Equal((PanEmulationState.Paused, PanReason.HidingLost), (State, _emulation.Status.Hiding));
        Assert.Equal([PanEmulation.PausedText], _notices);

        _pan.HidingHolds = PanReason.None;
        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal([PanEmulation.PausedText, PanEmulation.RunningAgainText], _notices);
        Assert.Equal([1u, 2u], _pan.Hidden);
    }

    [Fact]
    [Trait("TestCase", "T242-7")]
    public async Task HidHides_configuration_opened_while_running_pauses_until_it_is_closed()
    {
        await _emulation.TickAsync();

        _pan.Hiding = PanReason.HidHideBusy;
        await _emulation.TickAsync();
        Assert.Equal((PanEmulationState.Paused, PanReason.HidHideBusy), (State, _emulation.Status.Hiding));
        await _emulation.TickAsync();
        Assert.Single(_pan.Started);

        _pan.Hiding = PanReason.None;
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal([PanEmulation.PausedText, PanEmulation.RunningAgainText], _notices);
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task A_teardown_by_Pan_drops_pad_and_copy_acknowledges_it_and_starts_anew()
    {
        await _emulation.TickAsync();
        var pad = await _emulation.PlugAsync(CancellationToken.None);
        _pan.TearDown();

        Assert.False(pad.IsConnected);
        Assert.True(pad.TakenAway);
        await _emulation.TickAsync();
        Assert.Equal([1u], _pan.Acknowledged);
        Assert.Empty(_pan.Unhidden);
        Assert.Equal(1, _copy.Detached);

        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal([1u, 2u], _pan.Started);
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task A_copy_that_fails_to_go_leaves_the_puck_hidden_for_Pans_teardown_which_ends_the_forwarding_first()
    {
        await _emulation.TickAsync();
        _copy.FailDetach = "VIIPER antwortet nicht";
        _events.Clear();

        await _emulation.ShutdownAsync();

        Assert.Empty(_pan.Unhidden);
        Assert.Equal(["stopped 1"], _events);
        Assert.Contains(_log, line => line.Contains("Kopie entfernen fehlgeschlagen"));
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task A_Pan_that_ends_every_new_emulation_at_once_restarts_the_puck_at_most_once_per_tick()
    {
        _pan.TearDownAfterHides = 3;

        await _emulation.TickAsync();

        Assert.Equal([1u], _pan.Started);
        Assert.Equal([1u], _pan.Hidden);

        await _emulation.TickAsync();
        Assert.Equal([1u, 2u], _pan.Started);
    }

    [Fact]
    [Trait("TestCase", "T242-20")]
    public async Task While_Steam_is_in_front_or_was_within_three_minutes_with_the_real_puck_nothing_is_hidden()
    {
        var steamInFront = true;
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            SteamControllerPresent = () => _present,
            SteamInFront = () => steamInFront,
        };

        await emulation.TickAsync();
        Assert.Equal(PanEmulationState.SettingUp, emulation.Status.State);
        Assert.Equal(PanEmulationProblem.WaitingForSteam, emulation.Status.Problem);

        steamInFront = false;
        _time.Advance(PanEmulation.FirmwareGrace - TimeSpan.FromSeconds(1));
        await emulation.TickAsync();
        Assert.Empty(_pan.Hidden);
        Assert.Empty(_pan.Started);

        _time.Advance(TimeSpan.FromSeconds(1));
        await emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, emulation.Status.State);
        Assert.Equal([1u], _pan.Hidden);

        // Hidden, Steam reads only the copy: Steam in front changes nothing.
        steamInFront = true;
        await emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, emulation.Status.State);
    }

    [Fact]
    [Trait("TestCase", "T242-20")]
    public async Task After_a_pause_with_Steam_in_front_the_hide_waits_three_minutes_after_Steam_left()
    {
        var steamInFront = false;
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            SteamControllerPresent = () => _present,
            SteamInFront = () => steamInFront,
        };
        await emulation.TickAsync();
        _pan.HidingHolds = PanReason.HidingLost;
        await emulation.TickAsync();
        Assert.Equal(PanEmulationState.Paused, emulation.Status.State);

        // Paused, Steam reads the real puck and may start an update on it.
        steamInFront = true;
        _pan.HidingHolds = PanReason.None;
        await emulation.TickAsync();
        steamInFront = false;
        _time.Advance(TimeSpan.FromMinutes(1));
        await emulation.TickAsync();

        Assert.Equal([1u], _pan.Hidden);
        Assert.Equal(PanEmulationProblem.WaitingForSteam, emulation.Status.Problem);

        _time.Advance(PanEmulation.FirmwareGrace);
        await emulation.TickAsync();
        Assert.Equal([1u, 2u], _pan.Hidden);
    }

    [Fact]
    public async Task Without_Pan_or_a_part_nothing_starts_and_the_reason_is_given()
    {
        _pan.Missing = "Pan ist nicht eingerichtet";
        await _emulation.TickAsync();
        Assert.Equal(new PanEmulationStatus(PanEmulationState.Unavailable, Reason: "Pan ist nicht eingerichtet"), _emulation.Status);

        _pan.Missing = PanClient.CleaningUp;
        await _emulation.TickAsync();
        Assert.Equal((PanEmulationState.SettingUp, PanEmulationProblem.CleaningUp), (State, _emulation.Status.Problem));
        Assert.False(_emulation.Status.HasProblem);

        _pan.Missing = null;
        _pads.Missing = "Treiber usbip nicht geladen";
        await _emulation.TickAsync();
        Assert.Equal("Treiber usbip nicht geladen", _emulation.Status.Reason);
        Assert.Empty(_pan.Started);
    }

    [Fact]
    public async Task Pan_gone_while_running_drops_everything_without_asking_Pan()
    {
        await _emulation.TickAsync();
        _pan.Lose("Pan antwortet nicht");

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Unavailable, State);
        Assert.Equal(1, _copy.Detached);
    }

    [Fact]
    public async Task Without_a_Steam_Controller_it_is_ready_and_hides_nothing_until_one_comes()
    {
        _present = false;
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Ready, State);
        Assert.Empty(_pan.Started);

        _present = true;
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);

        // Once hidden it stays hidden when the controller is switched off: a replug needs nothing (decision 1).
        _present = false;
        _copy.PuckPresent = false;
        _copy.Current!.Lose();
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        Assert.Empty(_pan.Unhidden);
        Assert.Equal(1, _copy.Detached);

        _copy.PuckPresent = true;
        await _emulation.TickAsync();
        Assert.Equal(2, _copy.Attached);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task Failing_more_than_three_times_in_five_minutes_stops_until_Erneut_versuchen()
    {
        _copy.FailAttach = "VIIPER antwortet nicht";
        for (var i = 0; i < 4; i++)
        {
            await _emulation.TickAsync();
            _time.Advance(TimeSpan.FromSeconds(30));
        }

        Assert.Equal([PanEmulation.StoppedText], _notices);
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Stopped, State);
        Assert.Equal(4, _pan.Started.Count);

        _copy.FailAttach = null;
        _emulation.ForgetFailures();
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
    }

    [Fact]
    public async Task The_pad_giving_up_stops_the_whole_emulation_and_shows_the_puck()
    {
        await _emulation.TickAsync();
        _emulation.PadGaveUp();

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Stopped, State);
        Assert.Equal([1u], _pan.Unhidden);
    }

    [Fact]
    public async Task A_build_without_the_copy_never_hides_and_still_brings_the_pad()
    {
        _copy.CanCopy = false;

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, State);
        Assert.Empty(_pan.Hidden);
        Assert.Equal(0, _copy.Attached);
        await _emulation.PlugAsync(CancellationToken.None);
        Assert.Equal(1, _pads.Plugged);
        await _emulation.ShutdownAsync();
        Assert.Empty(_pan.Unhidden);
        Assert.Equal([1u], _pan.Stopped);
    }

    [Fact]
    public async Task A_VIIPER_that_does_not_start_counts_as_a_failure_and_registers_nothing()
    {
        _pads.FailPrepare = true;

        await _emulation.TickAsync();

        Assert.Equal((PanEmulationState.Paused, PanEmulationProblem.CopyFailed), (State, _emulation.Status.Problem));
        Assert.Empty(_pan.Started);
    }

    [Fact]
    public async Task A_pad_sends_nothing_once_the_emulation_took_it_out()
    {
        await _emulation.TickAsync();
        var pad = await _emulation.PlugAsync(CancellationToken.None);
        _enabled = false;
        await _emulation.TickAsync();

        await pad.SendAsync(XboxPadState.Neutral with { Buttons = 1 }, CancellationToken.None);
        await pad.DisposeAsync();

        Assert.Empty(_pads.Current!.Sent);
        Assert.Equal(1, _pads.Unplugged);
    }
}
