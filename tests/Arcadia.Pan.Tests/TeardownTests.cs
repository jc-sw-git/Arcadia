using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public class TeardownTests
{
    private readonly List<string> _log = [];

    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

    /// <summary>The journaled server of Arcadia's VIIPER the tests' devices come from.</summary>
    private static readonly UsbipServer Ours = new(Generation: 1, FakeCleanup.ServerPort, Pid: 10);

    /// <summary>For steps that end at once: a loaded CI runner may take longer than <see cref="Short"/> to start them.</summary>
    private static readonly TimeSpan Ample = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Every_step_runs_with_its_own_timeout_and_a_stuck_or_failing_step_never_stops_the_next()
    {
        var ran = new List<string>();
        var results = await TeardownRunner.RunAsync(
        [
            new("hangs", async token => { await Task.Delay(Timeout.Infinite, token); }, Short),
            new("fails", _ => throw new InvalidOperationException("nope"), Ample),
            new("runs", _ => { ran.Add("runs"); return Task.CompletedTask; }, Ample),
        ], _log.Add);

        Assert.Equal([StepOutcome.TimedOut, StepOutcome.Failed, StepOutcome.Ok], results.Select(r => r.Outcome));
        Assert.Equal("runs", Assert.Single(ran));
        Assert.Contains("Teardown: hangs: TimedOut (after 0 s)", _log);
        Assert.Contains("Teardown: fails: Failed (nope)", _log);
    }

    [Fact]
    public async Task A_cancelled_step_ends_no_further_process()
    {
        // A step that timed out is left behind with its token cancelled: it must not go on ending processes.
        var cleanup = new FakeCleanup();
        var held = new List<HeldProcess> { new(10, 1, "viiper.exe"), new(11, 2, "viiper.exe") };
        var plan = new TeardownPlan(cleanup, held, _ => { }, [], _ => { });
        var end = plan.Steps().Single(step => step.Name == "end forwarding processes");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => end.Run(new CancellationToken(canceled: true)));

        Assert.Empty(cleanup.Ended);
    }

    [Fact]
    public void A_step_result_names_its_error_only_when_it_has_one()
    {
        Assert.Equal("detach: ok", new StepResult("detach", StepOutcome.Ok).ToString());
        Assert.Equal("detach: Failed", new StepResult("detach", StepOutcome.Failed).ToString());
        Assert.Equal("detach: Failed (busy)", new StepResult("detach", StepOutcome.Failed, "busy").ToString());
    }

    [Fact]
    public async Task A_step_that_ignores_its_token_is_left_behind_after_its_timeout()
    {
        var results = await TeardownRunner.RunAsync(
        [
            new("ignores the token", _ => Task.Delay(TimeSpan.FromSeconds(30)), Short),
        ], _log.Add);

        Assert.Equal(StepOutcome.TimedOut, results[0].Outcome);
    }

    [Fact]
    public async Task The_ports_are_detached_before_the_processes_end()
    {
        var cleanup = new FakeCleanup { Ports = [2, 5] };
        var journal = new JournalState { Emulating = true, HeldProcesses = [new(10, 1, "viiper.exe")] };
        var order = new List<string>();
        var ended = 0;
        cleanup.Children = [];
        var plan = new TeardownPlan(cleanup, journal.HeldProcesses.ToList(), process => { journal.HeldProcesses.Remove(process); ended++; }, [Ours], _ => { });

        foreach (var step in plan.Steps())
        {
            order.Add(step.Name);
            await step.Run(CancellationToken.None);
        }

        Assert.Equal(["detach usbip ports", "end forwarding processes"], order);
        Assert.Equal([2, 5], cleanup.Detached);
        Assert.Equal(10, Assert.Single(cleanup.Ended).Pid);
        Assert.Empty(journal.HeldProcesses);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task A_process_that_cannot_be_ended_stays_in_the_journal_and_the_step_fails()
    {
        var cleanup = new FakeCleanup { EndFails = true };
        var journal = new JournalState { Emulating = true, HeldProcesses = [new(10, 1, "viiper.exe"), new(11, 2, "viiper.exe")] };
        var plan = new TeardownPlan(cleanup, journal.HeldProcesses.ToList(), process => journal.HeldProcesses.Remove(process), [Ours], _ => { });

        var results = await TeardownRunner.RunAsync(plan.Steps(), _log.Add);

        Assert.Equal(StepOutcome.Failed, results[1].Outcome);
        Assert.Contains("viiper.exe (10): access denied", results[1].Error);
        Assert.Equal(2, journal.HeldProcesses.Count);
    }

    [Fact]
    public async Task A_port_that_cannot_be_detached_fails_its_step_but_the_processes_still_end()
    {
        var cleanup = new FakeCleanup { Ports = [1], DetachFails = true };
        var journal = new JournalState { Emulating = true, HeldProcesses = [new(10, 1, "viiper.exe")] };
        var plan = new TeardownPlan(cleanup, journal.HeldProcesses.ToList(), process => journal.HeldProcesses.Remove(process), [Ours], _ => { });

        var results = await TeardownRunner.RunAsync(plan.Steps(), _log.Add);

        Assert.Equal(StepOutcome.Failed, results[0].Outcome);
        Assert.Contains("port(s) 1 not detached", results[0].Error);
        Assert.Equal(StepOutcome.Ok, results[1].Outcome);
        Assert.Single(cleanup.Ended);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public async Task Only_devices_from_a_journaled_server_are_detached_and_another_programs_VIIPER_keeps_its_own()
    {
        // Another program's VIIPER on its default port, with a device of its own imported (port 7).
        var cleanup = new FakeCleanup { Ports = [2], ImportedFrom = { [UsbipPorts.ViiperPort] = [7] } };
        var detached = new List<UsbipServer>();
        var plan = new TeardownPlan(cleanup, [], _ => { }, [Ours], detached.Add);

        var results = await TeardownRunner.RunAsync(plan.Steps(), _log.Add);

        Assert.All(results, result => Assert.Equal(StepOutcome.Ok, result.Outcome));
        Assert.Equal([2], cleanup.Detached);
        Assert.Equal([Ours], detached);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public async Task A_server_whose_device_did_not_detach_is_not_reported_and_stays_journaled()
    {
        var cleanup = new FakeCleanup { Ports = [2], DetachFails = true };
        var detached = new List<UsbipServer>();
        var plan = new TeardownPlan(cleanup, [], _ => { }, [Ours], detached.Add);

        var results = await TeardownRunner.RunAsync(plan.Steps(), _log.Add);

        Assert.Equal(StepOutcome.Failed, results[0].Outcome);
        Assert.Empty(detached);
    }
}
