using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// Acceptance tests for #422 written from the test cases: one uninstall and the Windows starts after it, chained the way
/// the service runs them (<see cref="UninstallOrder"/> at <c>--uninstall</c>, then <see cref="SelfRemoval"/> and
/// <see cref="UninstallOrder"/> again at each start), with the real marker file in between.
/// </summary>
public sealed class PendingUninstallJourneyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-pan-422-").FullName;
    private readonly List<string> _steps = [];
    private readonly PendingUninstallFile _marker;

    public PendingUninstallJourneyTests() => _marker = new PendingUninstallFile(Path.Combine(_root, PendingUninstallFile.FileName));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Arcadia's uninstall hook: <c>Pan.exe --uninstall</c>, the installed service stopped first.</summary>
    private Task<UninstallOutcome> Uninstall(bool removeDrivers, bool restored = true, bool driversRemoved = true) =>
        UninstallOrder.RunAsync(DataFolder.Ours, new PendingUninstall(removeDrivers, DriverAttempts: 0), _marker, Steps(restored, driversRemoved, isService: false));

    /// <summary>
    /// A Windows start: the service recovered its journal (<paramref name="undoComplete"/>), looks for Arcadia, and
    /// finishes as <c>PanUninstall.FinishInServiceAsync</c> does; null when it went on serving.
    /// </summary>
    private async Task<UninstallOutcome?> WindowsStart(bool undoComplete, bool driversRemoved = true, Action? crashAt = null, params ArcadiaPresence[] looks)
    {
        var next = new Queue<ArcadiaPresence>(looks);
        if (!await SelfRemoval.ShouldFinishAsync(undoComplete, _marker, () => next.Dequeue(), () => Task.CompletedTask, () => false, _ => { }))
        {
            return null;
        }

        var pending = _marker.Load() ?? new PendingUninstall(RemoveDrivers: false, DriverAttempts: 0);
        return await UninstallOrder.RunAsync(DataFolder.Ours, pending, _marker, Steps(restored: true, driversRemoved, isService: true, crashAt));
    }

    private UninstallSteps Steps(bool restored, bool driversRemoved, bool isService, Action? crashAt = null) => new()
    {
        StopService = () => _steps.Add(isService ? "-" : "stop service"),
        Restore = () => Task.FromResult(restored),
        RemoveDrivers = () =>
        {
            _steps.Add("drivers");
            return Task.FromResult(driversRemoved);
        },
        RemoveService = () =>
        {
            crashAt?.Invoke();
            _steps.Add("remove service");
        },
        StopFileLog = () => { },
        DeleteData = () => _steps.Add("delete data"),
        Log = _ => { },
    };

    [Fact]
    [Trait("TestCase", "T422-1")]
    public async Task An_undo_blocked_at_the_uninstall_is_finished_at_the_next_Windows_start_with_the_drivers_the_player_chose()
    {
        Assert.Equal(UninstallOutcome.RestoreIncomplete, await Uninstall(removeDrivers: true, restored: false));
        Assert.DoesNotContain("drivers", _steps);
        Assert.DoesNotContain("remove service", _steps);

        // The Configuration Client is still open at the first start: nothing more happens, the marker stays.
        Assert.Null(await WindowsStart(undoComplete: false, looks: [ArcadiaPresence.Absent]));
        Assert.Equal(new PendingUninstall(RemoveDrivers: true, DriverAttempts: 0), _marker.Load());

        Assert.Equal(UninstallOutcome.Done, await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        Assert.Equal(["stop service", "drivers", "remove service", "delete data"], _steps.Where(step => step != "-"));
    }

    [Fact]
    [Trait("TestCase", "T422-2")]
    public async Task A_driver_that_never_goes_is_tried_as_often_as_allowed_and_then_Pan_goes_without_it()
    {
        // The uninstall itself is the first attempt, each Windows start after it one more.
        var outcomes = new List<UninstallOutcome?> { await Uninstall(removeDrivers: true, driversRemoved: false) };
        while (outcomes[^1] == UninstallOutcome.DriversPending && outcomes.Count <= UninstallOrder.MaxDriverAttempts)
        {
            outcomes.Add(await WindowsStart(undoComplete: true, driversRemoved: false, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        }

        Assert.Equal(UninstallOrder.MaxDriverAttempts, outcomes.Count);
        Assert.All(outcomes.SkipLast(1), outcome => Assert.Equal(UninstallOutcome.DriversPending, outcome));
        Assert.Equal(UninstallOutcome.DriversLeft, outcomes[^1]);
        Assert.Equal(UninstallOrder.MaxDriverAttempts, _steps.Count(step => step == "drivers"));
        Assert.Equal(1, _steps.Count(step => step == "remove service"));
        Assert.DoesNotContain("delete data", _steps);
        Assert.Null(_marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-3")]
    public async Task Arcadia_installed_again_before_the_restart_keeps_Pan_and_the_drivers()
    {
        await Uninstall(removeDrivers: true, restored: false);

        Assert.Null(await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Found]));
        Assert.Null(_marker.Load());

        // Arcadia is uninstalled for good later, without the drivers asked for: the old "Ja" doesn't come back.
        Assert.Equal(UninstallOutcome.Done, await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        Assert.DoesNotContain("drivers", _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-4")]
    public async Task A_declined_UAC_prompt_leaves_Pan_to_remove_itself_at_the_next_start_without_the_drivers()
    {
        Assert.Equal(UninstallOutcome.Done, await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));

        Assert.Equal(["remove service", "delete data"], _steps.Where(step => step != "-"));
    }

    [Fact]
    [Trait("TestCase", "T422-6")]
    public async Task A_power_loss_before_the_service_went_is_finished_at_the_next_start()
    {
        await Uninstall(removeDrivers: true, restored: false);

        await Assert.ThrowsAsync<IOException>(() => WindowsStart(undoComplete: true, crashAt: () => throw new IOException("power lost"), looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        Assert.Equal(new PendingUninstall(RemoveDrivers: true, DriverAttempts: 1), _marker.Load());

        Assert.Equal(UninstallOutcome.Done, await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        Assert.Contains("remove service", _steps);
        Assert.Contains("delete data", _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-6")]
    public async Task A_marker_that_became_unreadable_finishes_the_uninstall_but_keeps_the_drivers()
    {
        await Uninstall(removeDrivers: true, restored: false);
        await File.WriteAllTextAsync(Path.Combine(_root, PendingUninstallFile.FileName), "{ broken");

        Assert.Equal(UninstallOutcome.Done, await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Absent]));
        Assert.DoesNotContain("drivers", _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-7")]
    public async Task A_place_that_can_t_be_read_at_the_second_look_keeps_Pan_and_the_marker()
    {
        await Uninstall(removeDrivers: true, restored: false);

        Assert.Null(await WindowsStart(undoComplete: true, looks: [ArcadiaPresence.Absent, ArcadiaPresence.Unknown]));
        Assert.NotNull(_marker.Load());
    }
}
