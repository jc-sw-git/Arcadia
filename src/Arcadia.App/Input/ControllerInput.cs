using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Where Arcadia's controllers come from: SDL3 on Windows (#72), nothing elsewhere for now, as Arcadia ships SDL only
/// for Windows. Without a usable <c>SDL3.dll</c> Arcadia runs on: the error goes to <c>arcadia.log</c> and a toast,
/// and keyboard and mouse keep working.
/// </summary>
public static class ControllerInput
{
    public const string UnavailableText = "Controller werden nicht erkannt (SDL3 fehlt oder startet nicht). Tastatur und Maus funktionieren – Details stehen in arcadia.log.";

    public static IGamepadSource Create(MainViewModel viewModel) =>
        OperatingSystem.IsWindows() ? Create(new SdlGamepads(), viewModel) : new NullGamepadSource();

    public static SdlGamepadSource Create(ISdlGamepads sdl, MainViewModel viewModel) =>
        new(sdl, AppLog.Write, error =>
        {
            AppLog.Write($"Controller-Eingabe nicht verfügbar (SDL3): {error}");
            Dispatcher.UIThread.Post(() => viewModel.Toast.Show(UnavailableText));
        });
}
