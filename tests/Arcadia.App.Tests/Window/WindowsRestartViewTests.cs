using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// "Windows neu starten" as drawn (#382, artboards T382F01, T382F05–F07): the restart row under the paused switch, the
/// question with its buttons and the footer that names what A does, and a refusal as a toast.
/// </summary>
public sealed class WindowsRestartViewTests
{
    private static readonly string RestartRowKey = PanRows.IssueKey(EmulationParts.WindowsRestartId);

    /// <summary>Set ARCADIA_CAPTURES to a folder to look at the drawn states (not needed for the test).</summary>
    private static void Keep(ArcadiaWindow arcadia, string name)
    {
        if (Environment.GetEnvironmentVariable("ARCADIA_CAPTURES") is { Length: > 0 } folder)
        {
            using var frame = arcadia.Capture();
            frame.Save(Path.Combine(folder, $"{name}.png"));
        }
    }

    private static IReadOnlyList<string> VisibleHints(ArcadiaWindow arcadia) =>
        arcadia.Hints(arcadia.Window.GetVisualDescendants().OfType<Arcadia.App.Controls.KeyHint>().Where(hint => hint.IsEffectivelyVisible));

    private static async Task<(ArcadiaWindow Arcadia, ControllerSettingsViewModel Controller)> OpenAsync(FakeSteam steam)
    {
        var arcadia = await ArcadiaWindow.ShowAsync(steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Arcadia.Main.Settings!.Selected);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == RestartRowKey), "die Zeile „Windows-Neustart ausstehend“");
        controller.FocusRow(RestartRowKey);
        arcadia.Settle();
        return (arcadia, controller);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-7")]
    [Trait("TestCase", "T382-18")]
    [Trait("TestCase", "T382-19")]
    public async Task The_paused_switch_and_its_restart_row_lead_to_the_question_and_B_goes_back_to_the_row()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        var (arcadia, controller) = await OpenAsync(steam);
        using var _ = arcadia;

        Keep(arcadia, "382-restart-row-paused");
        Assert.Equal("Pausiert bis zum Neustart von Windows", controller.Rows.Single(row => row.Key == PanRows.MainKey).Subtitle);
        Assert.Contains(arcadia.FooterHints(), hint => hint.EndsWith("Windows neu starten", StringComparison.Ordinal));
        Keep(arcadia, "382-restart-row-paused");

        arcadia.Press(UiAction.Accept).Settle();
        Assert.Equal(SettingsPrompt.ConfirmWindowsRestart, controller.Prompt);
        var box = arcadia.Bounds(arcadia.Window.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("prompt") && border.IsEffectivelyVisible));
        foreach (var text in new[] { ControllerSettingsViewModel.RestartTitle, ControllerSettingsViewModel.RestartText })
        {
            var shown = arcadia.WithText(text).Single(control => control.IsEffectivelyVisible);
            Assert.True(box.Contains(arcadia.Bounds(shown)), $"{text} liegt im Dialog");
        }

        Assert.Contains(VisibleHints(arcadia), hint => hint.EndsWith(" Neu starten", StringComparison.Ordinal));
        Keep(arcadia, "382-question");
        arcadia.Press(UiAction.Right).Settle();
        Assert.Contains(VisibleHints(arcadia), hint => hint == "A Abbrechen");
        Assert.DoesNotContain(VisibleHints(arcadia), hint => hint.EndsWith(" Neu starten", StringComparison.Ordinal));

        arcadia.Press(UiAction.Back).Settle();
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(RestartRowKey, controller.FocusedRow?.Key);
        Assert.Equal(0, steam.WindowsRestarts);

        arcadia.Press(UiAction.Accept).Press(UiAction.Accept).Settle();
        await arcadia.Arcadia.WaitUntilAsync(() => steam.WindowsRestarts == 1, "der Neustart von Windows");
        Assert.Equal(SettingsPrompt.None, controller.Prompt);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-22")]
    public async Task A_refused_restart_shows_its_toast_and_the_row_stays()
    {
        var steam = new FakeSteam { UsbipRebootPending = true, WindowsRestartError = new InvalidOperationException("shutdown.exe endete mit 1190") };
        var (arcadia, controller) = await OpenAsync(steam);
        using var _ = arcadia;

        arcadia.Press(UiAction.Accept).Press(UiAction.Accept).Settle();

        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Main.Toast.Text?.StartsWith(ControllerSupportService.RestartRefusedTitle, StringComparison.Ordinal) == true, "der Toast");
        arcadia.Settle();
        Keep(arcadia, "382-refused");
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == RestartRowKey));
        Assert.True(row.CanActivate);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T382-23")]
    public async Task With_keyboard_the_footer_shows_Space_and_Esc_and_a_click_on_Abbrechen_closes_the_question()
    {
        var steam = new FakeSteam { HidHideRemovalPending = true };
        var (arcadia, controller) = await OpenAsync(steam);
        using var _ = arcadia;

        arcadia.Key(Key.Space).Settle();
        Assert.Equal(SettingsPrompt.ConfirmWindowsRestart, controller.Prompt);
        Assert.Contains(VisibleHints(arcadia), hint => hint.StartsWith("Leertaste", StringComparison.Ordinal) && hint.EndsWith(" Neu starten", StringComparison.Ordinal));
        Assert.Contains(VisibleHints(arcadia), hint => hint.StartsWith("Esc", StringComparison.Ordinal) && hint.EndsWith(" Abbrechen", StringComparison.Ordinal));
        Keep(arcadia, "382-question-keyboard");

        var cancel = arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && Equals(button.Content, "Abbrechen")
            && button.Command == controller.ClosePromptCommand);
        arcadia.Click(cancel).Settle();

        Assert.Equal(SettingsPrompt.None, controller.Prompt);
        Assert.Equal(0, steam.WindowsRestarts);
    }
}
