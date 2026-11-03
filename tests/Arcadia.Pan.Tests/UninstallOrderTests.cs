using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The order of an uninstall (#243 part B2, overall review findings 1 and 2, #422): the drivers go only after a confirmed
/// undo, the service stays until nothing is left for it to retry, and nothing is logged into the data folder once it
/// is being deleted.
/// </summary>
public class UninstallOrderTests
{
    private readonly List<string> _steps = [];
    private readonly FakeMarker _marker = new();

    private Task<UninstallOutcome> Run(DataFolder data, bool restored = true, bool driversWanted = true, bool driversRemoved = true, int attempts = 0, Action? duringDrivers = null) =>
        UninstallOrder.RunAsync(data, new PendingUninstall(driversWanted, attempts), _marker, new UninstallSteps
        {
            StopService = () => _steps.Add("stop service"),
            Restore = () =>
            {
                _steps.Add("restore");
                return Task.FromResult(restored);
            },
            RemoveStaleDevices = () =>
            {
                _steps.Add("stale devices");
                return Task.CompletedTask;
            },
            RemoveDrivers = () =>
            {
                _steps.Add("drivers");
                duringDrivers?.Invoke();
                return Task.FromResult(driversRemoved);
            },
            RemoveService = () => _steps.Add("remove service"),
            StopFileLog = () => _steps.Add("stop file log"),
            DeleteData = () => _steps.Add("delete data"),
            Log = _ => { },
        });

    [Fact]
    [Trait("TestCase", "T243-37")]
    [Trait("TestCase", "T347-6")]
    [Trait("TestCase", "T422-1")]
    public async Task The_drivers_go_after_the_undo_and_the_service_only_after_them()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Ours));

        Assert.Equal(["stop service", "restore", "stale devices", "drivers", "remove service", "stop file log", "delete data"], _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-1")]
    public async Task The_pending_uninstall_is_recorded_before_anything_changes()
    {
        var first = string.Empty;
        await UninstallOrder.RunAsync(DataFolder.Ours, new PendingUninstall(RemoveDrivers: true, 0), _marker, new UninstallSteps
        {
            StopService = () => first = _marker.Saved.Count > 0 ? "marker" : "service",
            Restore = () => Task.FromResult(true),
            RemoveDrivers = () => Task.FromResult(true),
            RemoveService = () => { },
            StopFileLog = () => { },
            DeleteData = () => { },
            Log = _ => { },
        });

        Assert.Equal("marker", first);
        Assert.True(_marker.Saved[0].RemoveDrivers);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    [Trait("TestCase", "T347-6")]
    [Trait("TestCase", "T422-1")]
    public async Task An_incomplete_undo_keeps_the_service_and_the_marker_so_the_next_Windows_start_tries_again()
    {
        Assert.Equal(UninstallOutcome.RestoreIncomplete, await Run(DataFolder.Ours, restored: false));

        Assert.Equal(["stop service", "restore"], _steps);
        Assert.NotNull(_marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-2")]
    public async Task A_driver_that_stays_keeps_the_service_and_counts_the_attempt()
    {
        Assert.Equal(UninstallOutcome.DriversPending, await Run(DataFolder.Ours, driversRemoved: false));

        Assert.Equal(["stop service", "restore", "stale devices", "drivers"], _steps);
        Assert.Equal(1, _marker.Load()!.DriverAttempts);
    }

    [Fact]
    [Trait("TestCase", "T422-2")]
    public async Task The_attempt_is_counted_before_the_uninstallers_run_so_a_crash_in_them_counts_too()
    {
        int? countedDuring = null;
        await Run(DataFolder.Ours, attempts: 2, duringDrivers: () => countedDuring = _marker.Load()!.DriverAttempts);

        Assert.Equal(3, countedDuring);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    [Trait("TestCase", "T422-2")]
    public async Task At_the_last_attempt_a_driver_that_stays_is_given_up_and_the_data_folder_keeps_the_log()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.Ours, driversRemoved: false, attempts: UninstallOrder.MaxDriverAttempts - 1));

        Assert.Equal(["stop service", "restore", "stale devices", "drivers", "remove service"], _steps);
        Assert.Null(_marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-2")]
    public async Task Attempts_used_up_by_a_crash_go_on_without_the_drivers()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.Ours, attempts: UninstallOrder.MaxDriverAttempts));

        Assert.DoesNotContain("drivers", _steps);
        Assert.Contains("remove service", _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-3")]
    public async Task An_install_that_deletes_the_marker_meanwhile_stops_the_uninstall_before_the_drivers()
    {
        _marker.DeleteOnLoad = true;

        Assert.Equal(UninstallOutcome.Cancelled, await Run(DataFolder.Ours));

        Assert.DoesNotContain("drivers", _steps);
        Assert.DoesNotContain("remove service", _steps);
    }

    [Fact]
    [Trait("TestCase", "T422-3")]
    public async Task An_install_during_the_driver_removal_keeps_the_new_service()
    {
        Assert.Equal(UninstallOutcome.Cancelled, await Run(DataFolder.Ours, duringDrivers: () => _marker.Delete()));

        Assert.DoesNotContain("remove service", _steps);
        Assert.DoesNotContain("delete data", _steps);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task A_data_folder_that_is_not_Pans_keeps_the_drivers_as_no_undo_can_be_confirmed()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.NotOurs));

        Assert.Equal(["stop service", "remove service"], _steps);
        Assert.Empty(_marker.Saved);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task Where_Pan_never_ran_the_drivers_go_without_anything_to_undo()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Missing));

        Assert.Equal(["stop service", "stale devices", "drivers", "remove service"], _steps);
        Assert.Empty(_marker.Saved);
    }

    [Fact]
    public async Task Where_Pan_never_ran_a_driver_that_stays_is_not_retried_as_there_is_no_service_to_do_it()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.Missing, driversRemoved: false));
    }

    [Fact]
    public async Task Without_the_drivers_asked_for_the_uninstall_is_as_before()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Ours, driversWanted: false));

        Assert.Equal(["stop service", "restore", "stale devices", "remove service", "stop file log", "delete data"], _steps);
    }

    [Fact]
    public void Arcadia_waits_for_a_command_at_least_as_long_as_Pan_s_steps_may_take()
    {
        Assert.True(PanTimeouts.Install >= PanTimeouts.UsbipInstaller + HidHideSetup.Wait + PanTimeouts.PuckRestart + PanTimeouts.ServiceSteps);
        Assert.True(PanTimeouts.Uninstall >= PanTimeouts.ServiceSteps + PanTimeouts.StaleDevices + 2 * PanTimeouts.DriverUninstaller);
    }
}

/// <summary>A marker kept in memory; <see cref="DeleteOnLoad"/> plays an <c>--install</c> that deletes it after the first save.</summary>
internal sealed class FakeMarker : IPendingUninstallStore
{
    private PendingUninstall? _pending;

    public List<PendingUninstall> Saved { get; } = [];

    public bool DeleteOnLoad { get; set; }

    public FakeMarker(PendingUninstall? pending = null) => _pending = pending;

    public PendingUninstall? Load()
    {
        if (DeleteOnLoad)
        {
            _pending = null;
        }

        return _pending;
    }

    public void Save(PendingUninstall pending)
    {
        _pending = pending;
        Saved.Add(pending);
    }

    public bool Delete()
    {
        var had = _pending is not null;
        _pending = null;
        return had;
    }
}
