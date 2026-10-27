using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// <c>--uninstall [--remove-drivers]</c> after the service is gone (#243 part B2, overall review findings 1 and 2): the
/// drivers go only after a confirmed undo, and nothing is logged into the data folder once it is being deleted.
/// </summary>
public class UninstallOrderTests
{
    private readonly List<string> _steps = [];

    private Task<UninstallOutcome> Run(DataFolder data, bool restored = true, bool? driversRemoved = true) =>
        UninstallOrder.RunAsync(
            data,
            () =>
            {
                _steps.Add("restore");
                return Task.FromResult(restored);
            },
            driversRemoved is { } removed
                ? () =>
                {
                    _steps.Add("drivers");
                    return Task.FromResult(removed);
                }
                : null,
            () => _steps.Add("stop file log"),
            () => _steps.Add("delete data"),
            _ => { });

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task The_drivers_go_after_the_undo_and_the_file_log_stops_before_the_data_folder_goes()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Ours));

        Assert.Equal(["restore", "drivers", "stop file log", "delete data"], _steps);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task An_incomplete_undo_keeps_the_drivers_so_HidHide_still_hides_a_puck_that_may_be_forwarded()
    {
        Assert.Equal(UninstallOutcome.RestoreIncomplete, await Run(DataFolder.Ours, restored: false));

        Assert.Equal(["restore"], _steps);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task A_driver_that_stays_keeps_the_data_folder_and_its_log()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.Ours, driversRemoved: false));

        Assert.Equal(["restore", "drivers"], _steps);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task A_data_folder_that_is_not_Pans_keeps_the_drivers_as_no_undo_can_be_confirmed()
    {
        Assert.Equal(UninstallOutcome.DriversLeft, await Run(DataFolder.NotOurs));

        Assert.Empty(_steps);
    }

    [Fact]
    [Trait("TestCase", "T243-37")]
    public async Task Where_Pan_never_ran_the_drivers_go_without_anything_to_undo()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Missing));

        Assert.Equal(["drivers"], _steps);
    }

    [Fact]
    public async Task Without_the_drivers_asked_for_the_uninstall_is_as_before()
    {
        Assert.Equal(UninstallOutcome.Done, await Run(DataFolder.Ours, driversRemoved: null));

        Assert.Equal(["restore", "stop file log", "delete data"], _steps);
    }

    [Fact]
    public void Arcadia_waits_for_a_command_at_least_as_long_as_Pan_s_steps_may_take()
    {
        Assert.True(PanTimeouts.Install >= PanTimeouts.UsbipInstaller + HidHideSetup.Wait + PanTimeouts.PuckRestart + PanTimeouts.ServiceSteps);
        Assert.True(PanTimeouts.Uninstall >= PanTimeouts.ServiceSteps + 2 * PanTimeouts.DriverUninstaller);
    }
}
