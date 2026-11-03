using Arcadia.App.Input;
using Arcadia.App.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// The progress dialog of a driver install as drawn (#413, artboard T413Dialog): the longest run fits the screen whole,
/// a click beside it does nothing, and after a failure [Schließen] closes it.
/// </summary>
public sealed class DriverInstallDialogViewTests
{
    private static FakeSteam EveryPartMissing()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null, IsSteamRunning = true };
        steam.Viiper.IsInstalled = false;
        return steam;
    }

    private static async Task<ControllerSettingsViewModel> StartInstallAsync(ArcadiaWindow arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Arcadia.Main.Settings!.Selected);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row is PanRowViewModel { IsInstall: true }), "„Treiber installieren“");
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Press(UiAction.Accept);
        return controller;
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    [Trait("TestCase", "T413-23")]
    [Trait("TestCase", "T413-21")]
    public async Task The_longest_run_fits_the_screen_and_a_click_beside_it_does_nothing(double scaling)
    {
        var steam = EveryPartMissing();
        steam.Pan.Running = new();
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: steam, scaling: scaling);
        var controller = await StartInstallAsync(arcadia);
        await arcadia.Arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1, "die Übergabe");
        arcadia.Settle();

        var dialog = arcadia.Showing(controller.InstallDialog!);
        var box = arcadia.Bounds(dialog.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("prompt")));
        Assert.True(new Rect(arcadia.Window.ClientSize).Contains(box), $"{box} liegt ganz im Fenster");
        foreach (var step in controller.InstallDialog!.Steps)
        {
            var title = arcadia.OneWithText(step.Title);
            Assert.True(title.IsEffectivelyVisible, step.Title);
            Assert.True(box.Contains(arcadia.Bounds(title)), $"{step.Title} liegt im Dialog");
        }

        Assert.True(arcadia.OneWithText(DriverInstallDialogViewModel.UsbHintTitle).IsEffectivelyVisible);
        Assert.DoesNotContain(arcadia.FooterHints(), hint => hint.Contains("Zurück", StringComparison.Ordinal));

        arcadia.Click(new Point(box.X / 2, box.Center.Y)).Settle();

        Assert.NotNull(controller.InstallDialog);
        steam.Pan.Running.SetResult();
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Restarts.Count == 1, "der Neustart");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T413-21")]
    [Trait("TestCase", "T413-20")]
    public async Task After_a_failure_Schliessen_closes_the_dialog_and_the_settings_stay()
    {
        var steam = EveryPartMissing();
        steam.Pan.ExitCode = 3;
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: steam);
        var controller = await StartInstallAsync(arcadia);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.InstallDialog is { IsFailed: true }, "der Fehler im Dialog");
        arcadia.Settle();

        var close = arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Command == controller.InstallDialog!.CloseCommand && button.IsEffectivelyVisible);
        // [Schließen] carries the focus ring, whole.
        RingGeometry.AssertNoRingCutOff(arcadia.Window, () => arcadia.Settle().WaitForTransitions(), "Installation fehlgeschlagen");
        arcadia.AssertRingAround(close, "Schließen");

        arcadia.Click(close).Settle();

        Assert.Null(controller.InstallDialog);
        Assert.True(arcadia.Arcadia.Main.IsSettingsOpen);
    }
}
