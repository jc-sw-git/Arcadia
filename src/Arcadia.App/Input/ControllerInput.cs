using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Arcadia.ControllerSupport;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Where Arcadia's controllers come from: SDL3 on Windows (#72), nothing elsewhere for now, as Arcadia ships SDL only
/// for Windows. Without a usable <c>SDL3.dll</c> Arcadia runs on: the error goes to <c>arcadia.log</c> and a toast,
/// and keyboard and mouse keep working.
/// </summary>
public static class ControllerInput
{
    public static string UnavailableText => AppTexts.ControllersUnavailable;

    public static IGamepadSource Create(MainViewModel viewModel, OwnPadModels ownPads, TimeProvider time)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NullGamepadSource();
        }

        // Out of scope of #212: Steam's hooks hide the physical Steam Controller from a process Steam started.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamGameId")))
        {
            AppLog.Write("Controller: Arcadia wurde über Steam gestartet; Steam verbirgt den Steam Controller vor Arcadia");
        }

        return Create(new SdlGamepads(), viewModel, time, new DeviceTreeEmulatedPads(XusbDevices.List, AppLog.Write, DeviceTree.InterfaceOnSoftwareHost, ownPads));
    }

    /// <param name="time">The clock of the quiet period of a throwing handler's errors (#399).</param>
    public static SdlGamepadSource Create(ISdlGamepads sdl, MainViewModel viewModel, TimeProvider time, IEmulatedPads? emulated = null) =>
        new(sdl, AppLog.Write, error =>
        {
            AppLog.Write($"Controller-Eingabe nicht verfügbar (SDL3): {error}");
            Dispatcher.UIThread.Post(() => viewModel.Toast.Show(HealthSeverity.Error, UnavailableText));
        }, emulated, AppErrors.ReportRepeating, time);
}
