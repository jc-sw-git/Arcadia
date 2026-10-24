using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Arcadia.App.Tests.TestApplication))]

namespace Arcadia.App.Tests;

/// <summary>
/// The view models post to Avalonia's UI thread; the headless platform gives the tests one without a window.
/// A plain <see cref="Application"/>, not Arcadia's <c>App</c>: the driver wires the parts itself, as <c>App</c> does.
/// </summary>
public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
