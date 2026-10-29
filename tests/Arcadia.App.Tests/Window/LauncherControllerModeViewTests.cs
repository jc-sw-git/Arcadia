using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The Steam Controller default on a launcher's page (#178) as drawn, with the mouse alone.</summary>
public sealed class LauncherControllerModeViewTests
{
    /// <summary>T178-10: the arrows switch the default, the reset's question takes clicks, ✕ and a click beside it.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T178-10")]
    public async Task With_the_mouse_alone_the_arrows_switch_the_default_and_the_question_takes_clicks()
    {
        var settings = new AppSettings { ActiveLaunchers = ["steam", "gog"] }.WithControllerModeChoice("steam:demo-0", true);
        using var arcadia = await ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)], settings);
        arcadia.Press(UiAction.Settings);
        arcadia.Click(arcadia.LauncherName(arcadia.LauncherRow("steam")));
        var mode = arcadia.Row("launcher-controller-mode");
        var reset = arcadia.Row("launcher-controller-reset");

        // ▶ and ◀ beside the value switch it.
        Assert.Equal(ControllerModes.Steam, arcadia.ValueShown(mode));
        arcadia.Click(arcadia.ValueArrows(mode)[1]);
        Assert.Equal(ControllerModes.Arcadia, arcadia.ValueShown(mode));
        arcadia.Click(arcadia.ValueArrows(mode)[0]);
        Assert.Equal(ControllerModes.Steam, arcadia.ValueShown(mode));

        // A click on the row asks; "Abbrechen", ✕ and a click beside the question keep every choice.
        var launchers = arcadia.LauncherCategory();
        foreach (var cancel in new[] { "Abbrechen", "✕", "daneben" })
        {
            arcadia.Click(arcadia.RowButton(reset));
            var question = Assert.IsType<SettingsQuestionViewModel>(launchers.Dialog);
            Assert.All(Glyphs(arcadia, question), glyph => Assert.False(glyph.IsEffectivelyVisible, "mit der Maus keine [A]/[B]"));
            switch (cancel)
            {
                case "Abbrechen":
                    arcadia.Click(DialogButton(arcadia, question.CancelCommand));
                    break;
                case "✕":
                    arcadia.MouseMove(new Point(300, 300)).MouseMove(new Point(340, 330));
                    arcadia.Click(arcadia.CloseButton(question.CancelCommand));
                    break;
                default:
                    arcadia.Click(new Point(60, 540));
                    break;
            }

            Assert.Null(launchers.Dialog);
            Assert.Single(arcadia.Arcadia.Settings.Current.ControllerSupportOverrides);
        }

        arcadia.Click(arcadia.RowButton(reset));
        arcadia.Click(DialogButton(arcadia, ((SettingsQuestionViewModel)launchers.Dialog!).ConfirmCommand));
        Assert.Null(launchers.Dialog);
        Assert.Empty(arcadia.Arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.DoesNotContain(launchers.Rows, row => row.Key == "launcher-controller-reset");
    }

    private static Button DialogButton(ArcadiaWindow arcadia, System.Windows.Input.ICommand command) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("action") && button.IsEffectivelyVisible && button.Command == command);

    private static IEnumerable<PadGlyph> Glyphs(ArcadiaWindow arcadia, SettingsQuestionViewModel question) =>
        new[] { question.ConfirmCommand, question.CancelCommand }.SelectMany(command => DialogButton(arcadia, command).GetVisualDescendants().OfType<PadGlyph>());
}
