using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Arcadia.App.Tests.TestApplication))]

namespace Arcadia.App.Tests;

/// <summary>
/// The view models post to Avalonia's UI thread; the headless platform gives the tests one. With Skia and Arcadia's
/// styles, <see cref="ArcadiaWindow"/> shows the real window and can look at its pixels (#191).
/// </summary>
public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<StyledApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            // Inter on every machine, as Program does: the same text widths on Windows, macOS and Linux.
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });

    /// <summary>
    /// Arcadia's styles as <c>App.axaml</c> loads them. Not Arcadia's <c>App</c>: it starts the real services and
    /// swallows UI errors, which a test must see; the driver wires the parts itself.
    /// </summary>
    public sealed class StyledApplication : Application
    {
        public override void Initialize()
        {
            // avares:// finds compiled XAML only in loaded assemblies.
            _ = typeof(Arcadia.App.App).Assembly;
            RequestedThemeVariant = ThemeVariant.Dark;
            Styles.Add(new FluentTheme());
            Styles.Add(new StyleInclude(new Uri("avares://Arcadia/")) { Source = new Uri("avares://Arcadia/Theme/Theme.axaml") });
        }
    }
}
