using Arcadia.App.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The overlay over a game (#64, #243 part B2): a Pan notice must not take away a hold the player is watching.</summary>
public sealed class MouseModeOverlayTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T243-36")]
    public void A_notice_during_the_View_hold_shows_below_Zurueck_zu_Arcadia_and_the_hold_goes_on()
    {
        var overlay = new MouseModeOverlay();
        try
        {
            overlay.ShowReturnHold(TimeSpan.FromSeconds(1));

            overlay.ShowNotice("Pan ist nicht erreichbar");

            Assert.True(overlay.FindControl<Panel>("ReturnBox")!.IsVisible, "„Zurück zu Arcadia“ bleibt, solange View gehalten wird.");
            var label = overlay.FindControl<Border>("Label")!;
            Assert.True(label.IsVisible);
            Assert.Contains("belowReturnBox", label.Classes);
            Assert.Equal("Pan ist nicht erreichbar", overlay.FindControl<TextBlock>("LabelText")!.Text);
        }
        finally
        {
            overlay.Close();
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T243-36")]
    public void A_notice_during_the_mouse_mode_hold_leaves_its_edge_filling()
    {
        var overlay = new MouseModeOverlay();
        try
        {
            overlay.ShowHold(TimeSpan.FromSeconds(1));

            overlay.ShowNotice("Pan ist nicht erreichbar");

            Assert.True(overlay.FindControl<Arcadia.App.Controls.ScreenEdgeFill>("Edge")!.IsVisible, "Der Rand füllt sich weiter.");
            var label = overlay.FindControl<Border>("Label")!;
            Assert.True(label.IsVisible);
            Assert.DoesNotContain("belowReturnBox", label.Classes);
        }
        finally
        {
            overlay.Close();
        }
    }
}
