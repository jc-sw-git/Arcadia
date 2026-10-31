using Arcadia.ControllerSupport.Copy;
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
    [Trait("TestCase", "T304-8")]
    public async Task A_VIIPER_lost_under_the_emulation_ends_it_and_the_next_start_names_the_new_port_before_any_device()
    {
        await _emulation.TickAsync();
        await _emulation.PlugAsync(CancellationToken.None);
        Assert.Equal([50000], _pan.StartedPorts);
        _events.Clear();

        // viiper.exe ended: Pan knows only its port, so nothing may go onto a new server within this generation.
        _pads.ServerPort = null;
        await _emulation.TickAsync();

        Assert.Equal(["pad unplugged", "copy detached", "unhide 1", "stopped 1"], _events);
        Assert.Contains("Pan-Emulation 1: VIIPER verloren", _log);
        _events.Clear();

        for (var tick = 0; tick < 5 && _pan.Started.Count < 2; tick++)
        {
            _time.Advance(TimeSpan.FromSeconds(10));
            await _emulation.TickAsync();
        }

        Assert.Equal([50000, 50001], _pan.StartedPorts);
        Assert.Equal(["started 2", "hide 2", "copy attached"], _events);
        Assert.Equal(PanEmulationState.Running, State);
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

    /// <summary>#308: Windows refuses to restart a puck Arcadia's SDL holds open, so every hide failed with only the puck.</summary>
    [Fact]
    [Trait("TestCase", "T308-1")]
    public async Task Arcadia_lets_go_of_the_puck_while_Pan_restarts_it_to_hide_or_show_it()
    {
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            ReadSteamControllers = on => _events.Add(on ? "reads puck" : "lets go of puck"),
        };

        await emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, emulation.Status.State);
        Assert.Equal(["started 1", "lets go of puck", "hide 1", "reads puck", "copy attached"], _events);

        _events.Clear();
        await emulation.ShutdownAsync();
        Assert.Equal(["copy detached", "lets go of puck", "unhide 1", "reads puck", "stopped 1"], _events);
    }

    [Fact]
    [Trait("TestCase", "T308-1")]
    public async Task Arcadia_reads_the_puck_again_also_after_a_hide_that_failed()
    {
        var reads = new List<bool>();
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add) { ReadSteamControllers = reads.Add };
        _pan.HideResult = PanReason.HidingFailed;

        await emulation.TickAsync();

        Assert.Equal([false, true], reads);
    }

    /// <summary>#308, QE review: Pan's own restarts (teardown, its retry, an arrival) were vetoed by Arcadia's SDL too.</summary>
    [Fact]
    [Trait("TestCase", "T308-7")]
    public void Arcadia_lets_go_of_the_puck_while_Pan_restarts_it_on_its_own()
    {
        _ = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            ReadSteamControllers = on => _events.Add(on ? "reads puck" : "lets go of puck"),
        };

        _pan.RestartPuckItself();
        // An end without a start (a lost link after the end) and a second announcement change nothing.
        _pan.EndRestart();
        _pan.AnnounceRestart();
        _pan.AnnounceRestart();
        _pan.EndRestart();

        Assert.Equal(["lets go of puck", "pan restarts puck", "reads puck", "lets go of puck", "reads puck"], _events);
    }

    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task A_restart_of_Pans_own_that_overlaps_Arcadias_hide_keeps_the_puck_let_go_until_both_are_over()
    {
        var emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            ReadSteamControllers = on => _events.Add(on ? "reads puck" : "lets go of puck"),
        };
        // Pan restarts the puck for an arrival while Arcadia's hide runs, and ends after it.
        _pan.PuckRestart = () =>
        {
            _pan.AnnounceRestart();
            return PanReason.None;
        };

        await emulation.TickAsync();
        Assert.Equal(["started 1", "lets go of puck", "hide 1", "copy attached"], _events);

        _pan.EndRestart();
        Assert.Equal("reads puck", _events[^1]);
    }

    /// <summary>"Erneut versuchen" pressed while a tick runs: that tick's outcome predates the press and doesn't answer it.</summary>
    [Fact]
    [Trait("TestCase", "T308-3")]
    public async Task Erneut_versuchen_is_answered_only_by_a_tick_that_began_after_the_press()
    {
        var notices = new List<string>();
        _emulation.Notice += notices.Add;
        _pan.HideResult = PanReason.HidingFailed;
        _pan.PuckRestart = () =>
        {
            _emulation.Retry();
            return PanReason.HidingFailed;
        };

        await _emulation.TickAsync();
        Assert.True(_emulation.Retrying);
        Assert.Empty(notices);

        _pan.PuckRestart = null;
        await _emulation.TickAsync();
        Assert.False(_emulation.Retrying);
        Assert.Equal([PanEmulation.RetryFailedText], notices);
    }

    /// <summary>QE recheck of #310 (R3): every attempt the player started ends with a toast, also one with nothing to emulate.</summary>
    [Fact]
    [Trait("TestCase", "T308-3")]
    public async Task Erneut_versuchen_that_finds_no_Steam_Controller_says_so()
    {
        var notices = new List<string>();
        _emulation.Notice += notices.Add;
        _pan.HideResult = PanReason.HidingFailed;
        await _emulation.TickAsync();

        _emulation.Retry();
        _present = false;
        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Ready, State);
        Assert.Equal([PanEmulation.RetryNoControllerText], notices);
    }

    /// <summary>QE recheck of #310 (R2): back after a lost hiding by "Erneut versuchen", the toast came twice.</summary>
    [Fact]
    [Trait("TestCase", "T308-3")]
    public async Task Erneut_versuchen_after_a_lost_hiding_says_it_runs_again_once()
    {
        var notices = new List<string>();
        _emulation.Notice += notices.Add;
        await _emulation.TickAsync();
        _pan.HidingHolds = PanReason.HidingLost;
        await _emulation.TickAsync();
        Assert.Equal([PanEmulation.PausedText], notices);
        _pan.HidingHolds = PanReason.None;

        _emulation.Retry();
        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, State);
        Assert.Equal([PanEmulation.PausedText, PanEmulation.RunningAgainText], notices);
    }

    /// <summary>#308: the warning disappeared for each retry and came back ~6 s later, over and over.</summary>
    [Fact]
    [Trait("TestCase", "T308-3")]
    public async Task A_hide_that_keeps_failing_keeps_its_warning_through_the_retries_without_a_moment_of_setting_up()
    {
        var states = new List<PanEmulationState>();
        _emulation.Changed += () => states.Add(State);
        _pan.HideResult = PanReason.HidingFailed;

        await _emulation.TickAsync();
        for (var retry = 0; retry < 2; retry++)
        {
            _time.Advance(PanEmulation.HideRetry);
            await _emulation.TickAsync();
        }

        Assert.Equal(3, _pan.Started.Count);
        Assert.Equal([PanEmulationState.SettingUp, PanEmulationState.Paused], states);
        Assert.Equal(PanEmulationProblem.Hiding, _emulation.Status.Problem);

        // Once it works, the row says so.
        _pan.HideResult = PanReason.None;
        _time.Advance(PanEmulation.HideRetry);
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
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
    [Trait("TestCase", "T295-3")]
    public async Task Released_for_a_firmware_update_the_puck_is_shown_and_stays_so_until_Pan_is_switched_off_and_on()
    {
        await _emulation.TickAsync();
        _events.Clear();

        await _emulation.ReleaseForFirmwareAsync();
        await _emulation.TickAsync();
        _time.Advance(PanEmulation.HideRetry);
        await _emulation.TickAsync();

        Assert.Equal(["copy detached", "unhide 1", "stopped 1"], _events);
        Assert.Equal(PanEmulationProblem.Excluded, _emulation.Status.Problem);
        Assert.False(_emulation.Status.HasProblem);
        Assert.Equal([1u], _pan.Started);

        _enabled = false;
        await _emulation.TickAsync();
        _enabled = true;
        await _emulation.TickAsync();

        Assert.Equal([1u, 2u], _pan.Started);
        Assert.Equal(PanEmulationState.Running, State);
        Assert.True(_emulation.HidesPuck);
    }

    [Fact]
    [Trait("TestCase", "T295-1")]
    public async Task A_hide_Pan_refuses_while_a_firmware_update_is_flashed_waits_and_is_no_failure()
    {
        _pan.HideResult = PanReason.BootloaderPresent;
        for (var i = 0; i <= PanEmulation.MaxFailuresPerWindow + 1; i++)
        {
            await _emulation.TickAsync();
            _time.Advance(PanEmulation.HideRetry);
        }

        Assert.Equal(PanEmulationState.SettingUp, State);
        Assert.Equal(PanEmulationProblem.FirmwareUpdate, _emulation.Status.Problem);
        Assert.False(_emulation.Status.HasProblem);
        Assert.Equal(0, _pads.Plugged);

        _pan.HideResult = PanReason.None;
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task A_pad_still_being_unplugged_holds_the_puck_hidden_until_it_is_gone()
    {
        await _emulation.TickAsync();
        var pad = await _emulation.PlugAsync(CancellationToken.None);
        _pads.UnplugGate = new TaskCompletionSource();
        var unplugging = pad.DisposeAsync().AsTask();
        _events.Clear();

        _enabled = false;
        var tick = _emulation.TickAsync();
        await Task.Delay(100);
        Assert.DoesNotContain("unhide 1", _events);

        _pads.UnplugGate.SetResult();
        await tick;
        await unplugging;
        Assert.Equal(["pad unplugged", "copy detached", "unhide 1", "stopped 1"], _events);
    }

    [Fact]
    [Trait("TestCase", "T242-9")]
    public async Task A_pad_that_failed_to_go_earlier_leaves_the_puck_to_Pans_teardown()
    {
        await _emulation.TickAsync();
        var pad = await _emulation.PlugAsync(CancellationToken.None);
        _pads.FailUnplug = true;
        await Assert.ThrowsAsync<IOException>(async () => await pad.DisposeAsync());
        _events.Clear();

        _enabled = false;
        await _emulation.TickAsync();

        Assert.Empty(_pan.Unhidden);
        Assert.Equal(["copy detached", "stopped 1"], _events);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_hide_that_keeps_being_rejected_counts_as_a_failure_and_stops()
    {
        _pan.HideResult = PanReason.HidingLost;
        for (var i = 0; i <= PanEmulation.MaxFailuresPerWindow; i++)
        {
            await _emulation.TickAsync();
            _time.Advance(PanEmulation.HideRetry);
        }

        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Stopped, State);
        Assert.Equal(PanEmulation.MaxFailuresPerWindow + 1, _pan.Started.Count);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_copy_that_hangs_while_attaching_times_out_and_shows_the_puck_again()
    {
        _copy.HangAttach = true;

        var tick = _emulation.TickAsync();
        await Task.Yield();
        _time.Advance(PanEmulation.AttachTimeout);
        await tick.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(PanEmulationState.Paused, State);
        Assert.Equal(PanEmulationProblem.CopyFailed, _emulation.Status.Problem);
        Assert.Equal([1u], _pan.Unhidden);
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
    [Trait("TestCase", "T242-6")]
    public async Task A_lost_copy_that_wont_go_cleanly_ends_the_emulation_instead_of_a_new_copy_beside_it()
    {
        await _emulation.TickAsync();
        Assert.Equal(PanEmulationState.Running, State);
        _copy.FailDetach = "Ein Aufruf an den Puck endet nicht";
        _copy.Current!.Lose();

        await _emulation.TickAsync();

        Assert.Equal((PanEmulationState.Paused, PanEmulationProblem.CopyFailed), (State, _emulation.Status.Problem));
        Assert.Equal(1, _copy.Attached);
        // Not shown by Arcadia: Pan's teardown ends viiper.exe first, then shows the puck.
        Assert.Empty(_pan.Unhidden);
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
