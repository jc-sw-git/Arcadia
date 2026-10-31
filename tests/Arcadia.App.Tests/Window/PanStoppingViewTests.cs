using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport.Pan;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>"Pan wird beendet" as drawn (#340, artboard T326F04): "noch höchstens" above the number, "Sekunden" below.</summary>
public sealed class PanStoppingViewTests
{
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    [Trait("TestCase", "T340-1")]
    public async Task The_caption_number_and_unit_stand_centred_in_the_ring_and_the_number_does_not_move_when_it_gets_one_digit(double scaling)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: OldPanStillRunning(), scaling: scaling);
        await arcadia.Arcadia.PanAnsweredAtStartAsync();
        arcadia.Press(UiAction.Back).Settle();

        var caption = Assert.IsType<TextBlock>(arcadia.OneWithText(PanUpdatePageViewModel.CountdownCaption));
        var number = Assert.IsType<TextBlock>(arcadia.OneWithText("45"));
        var ring = caption.FindAncestorOfType<StackPanel>()!.FindAncestorOfType<Panel>()!;
        var unit = ring.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Sekunden");
        Assert.Same(unit.Foreground, caption.Foreground);
        Assert.Equal(unit.FontSize, caption.FontSize);

        var inRing = arcadia.Bounds(ring);
        Rect[] lines = [arcadia.Bounds(caption), arcadia.Bounds(number), arcadia.Bounds(unit)];
        Assert.True(lines[0].Bottom <= lines[1].Top && lines[1].Bottom <= lines[2].Top, "von oben: noch höchstens, Zahl, Sekunden");
        foreach (var line in lines)
        {
            // The spinner's arc runs along the edge of the square: the lines keep clear of its inner fifth.
            Assert.True(inRing.Deflate(inRing.Width / 5).Contains(line), $"{line} liegt im Ring {inRing}");
            Assert.InRange(line.Center.X - inRing.Center.X, -1, 1);
        }

        var twoDigits = arcadia.Bounds(number).Center;
        for (var second = 0; second < 40 && number.Text != "9"; second++)
        {
            arcadia.Arcadia.Wait(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("9", number.Text);
        arcadia.Settle();
        var oneDigit = arcadia.Bounds(number).Center;
        Assert.InRange(oneDigit.Y - twoDigits.Y, -0.5, 0.5);
        Assert.InRange(oneDigit.X - twoDigits.X, -1, 1);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T340-2")]
    public async Task B_before_Pan_answered_shows_the_new_text_without_the_ring_and_its_caption()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: OldPanStillRunning());
        arcadia.Press(UiAction.Back).Settle();

        Assert.True(arcadia.OneWithText(PanUpdatePageViewModel.StoppingText).IsEffectivelyVisible);
        Assert.All(arcadia.WithText(PanUpdatePageViewModel.CountdownCaption), caption => Assert.False(caption.IsEffectivelyVisible));
    }

    private static FakeSteam OldPanStillRunning() => new() { PanCompatibility = PanCompatibility.UpdateRequired, IsPanRunning = true };
}
