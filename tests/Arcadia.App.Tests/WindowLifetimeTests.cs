using System.Reflection;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>A closed window lets go of what outlives it: the static error event and the services keep no disposed window alive.</summary>
public sealed class WindowLifetimeTests
{
    [AvaloniaFact]
    public async Task A_disposed_window_no_longer_listens_to_unexpected_errors()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;
        Assert.Contains(ErrorListeners(), listener => listener.Target == main);

        arcadia.Dispose();

        Assert.DoesNotContain(ErrorListeners(), listener => listener.Target == main);
    }

    [AvaloniaFact]
    public async Task A_disposed_window_no_longer_listens_to_the_controller_support_or_Pans_firmware_attempts()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;
        object[] parts = [main, main.Status, main.ControllerMode, main.PanDialogs];
        Assert.Contains(Listeners(arcadia.ControllerSupport, nameof(ControllerSupportService.Changed)), listener => parts.Contains(listener.Target));
        Assert.Contains(Listeners(arcadia.ForGames, nameof(SteamControllerForGames.FirmwareUpdateTried)), listener => parts.Contains(listener.Target));

        arcadia.Dispose();

        Assert.DoesNotContain(Listeners(arcadia.ControllerSupport, nameof(ControllerSupportService.Changed)), listener => parts.Contains(listener.Target));
        Assert.DoesNotContain(Listeners(arcadia.ForGames, nameof(SteamControllerForGames.FirmwareUpdateTried)), listener => parts.Contains(listener.Target));
    }

    [AvaloniaFact]
    public async Task The_header_clock_stops_when_the_window_is_disposed()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        var status = arcadia.Main.Status;
        Assert.Equal("20:00", status.Clock);

        arcadia.Dispose();
        arcadia.Wait(TimeSpan.FromSeconds(61));

        Assert.Equal("20:00", status.Clock);
    }

    private static Delegate[] ErrorListeners() =>
        (typeof(AppErrors).GetField(nameof(AppErrors.Occurred), BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Delegate)?.GetInvocationList() ?? [];

    // Field-like events keep their delegate in a private field of the same name.
    private static Delegate[] Listeners(object source, string eventName) =>
        (source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(source) as Delegate)?.GetInvocationList() ?? [];
}
