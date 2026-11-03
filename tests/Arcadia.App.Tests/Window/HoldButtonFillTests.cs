using System.Diagnostics;
using Arcadia.App.Input;
using Arcadia.ControllerSupport.Copy;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia;
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
        using var shown = await FirmwareNoticeAsync();
        var arcadia = shown.Arcadia;
        arcadia.Press(UiAction.Left, 2);
        shown.Settle();
        var fill = FillOf(shown, "Controller freigeben");
        Assert.Equal(0, fill.ScaleX);

        // „Controller freigeben“ holds for 1 s: let go before. The fill is an Avalonia animation on the real clock, which a
        // test can't set; it is bounded by the time measured instead of a fixed range, so a slow runner can't break it.
        var held = Stopwatch.StartNew();
        arcadia.PressDown(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(300));
        shown.Settle();
        Assert.InRange(fill.ScaleX, double.Epsilon, AtMost(held.Elapsed));

        arcadia.LetGo(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        shown.Settle();

        Assert.False(arcadia.Main.FirmwareAttempt!.IsReleased);
        Assert.Equal(0, fill.ScaleX);

        // The next press fills from empty again: no further than the time since this press allows.
        held.Restart();
        arcadia.PressDown(UiAction.Accept);
        shown.Settle();
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        shown.Settle();
        Assert.InRange(fill.ScaleX, double.Epsilon, AtMost(held.Elapsed));
        arcadia.LetGo(UiAction.Accept);
    }

    /// <summary>The fill a 1 s hold reaches at most after <paramref name="held"/>; it starts on the frame after the press.</summary>
    private static double AtMost(TimeSpan held) => Math.Min(1, held.TotalSeconds);

    /// <summary>The notice of a firmware update Steam tried through the copy (#295): its buttons act only when held.</summary>
    private static async Task<ArcadiaWindow> FirmwareNoticeAsync()
    {
        var shown = await ArcadiaWindow.ShowAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam: new FakeSteam { IsSteamRunning = true });
        var arcadia = shown.Arcadia;
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        arcadia.ForGames.CopyFilter.FromSteam(SteamReportKind.SetFeature, [1, 0x90, 0x00], forwarding: true);
        await arcadia.WaitUntilAsync(() => arcadia.Main.FirmwareAttempt is not null, "der Hinweis „Steam will die Firmware aktualisieren“");
        shown.Settle();
        return shown;
    }

    private static ScaleTransform FillOf(ArcadiaWindow shown, string label)
    {
        var button = Assert.Single(shown.Window.GetVisualDescendants().OfType<Button>(),
            b => b.IsVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
        var fill = button.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("holdFill"));
        return Assert.IsType<ScaleTransform>(fill.RenderTransform);
    }
}
