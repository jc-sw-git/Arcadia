using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The fill of a button that acts only when held (#242), drawn: it fills while held and empties when let go.</summary>
public sealed class HoldButtonFillTests
{
    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    [AvaloniaFact]
    [Trait("TestCase", "T242-20")]
    public async Task Letting_go_early_empties_the_fill_at_once()
    {
        // Owner's test of #214: the fill stayed where it was when let go, until the next press.
        using var shown = await ArcadiaWindow.ShowAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam: new FakeSteam { IsSteamRunning = true });
        var arcadia = shown.Arcadia;
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.TakeoverConsent is not null, "die Frage „Steam Controller und Puck übernehmen?“");
        shown.Settle();
        var fill = FillOf(shown, "Nein, nicht übernehmen");
        Assert.Equal(0, fill.ScaleX);

        // „Nein“ is focused and holds for 1 s: half way, then let go.
        arcadia.PressDown(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        shown.Settle();
        Assert.InRange(fill.ScaleX, 0.2, 0.95);

        arcadia.LetGo(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        shown.Settle();

        Assert.NotNull(arcadia.Main.TakeoverConsent);
        Assert.Equal(0, fill.ScaleX);

        // The next press fills from empty again.
        arcadia.PressDown(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(250));
        shown.Settle();
        Assert.InRange(fill.ScaleX, 0.05, 0.6);
        arcadia.LetGo(UiAction.Accept);
    }

    private static ScaleTransform FillOf(ArcadiaWindow shown, string label)
    {
        var button = Assert.Single(shown.Window.GetVisualDescendants().OfType<Button>(),
            b => b.IsVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
        var fill = button.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("holdFill"));
        return Assert.IsType<ScaleTransform>(fill.RenderTransform);
    }
}
