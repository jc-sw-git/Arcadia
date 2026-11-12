using Arcadia.App.Input;
using Arcadia.App.Setup;

namespace Arcadia.App.Tests;

/// <summary>Steps through the first start (#139, #497) as a player does with the controller.</summary>
internal static class FirstRunSteps
{
    /// <summary>The step "Datenquellen" now shown, or a failure.</summary>
    public static FirstRunDataSourcesViewModel DataSourcesStep(this ArcadiaDriver arcadia) =>
        Assert.IsType<FirstRunDataSourcesViewModel>(arcadia.Main.Settings?.Selected);

    /// <summary>≡ on the launcher page ("Fertig"), then ≡ on "Datenquellen" ("Überspringen"): the library.</summary>
    public static async Task FinishFirstRunAsync(this ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        Assert.True(arcadia.Main.Settings?.IsFirstRun, "Nach „Fertig“ soll der Schritt „Datenquellen“ kommen.");
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsInFirstRun && !arcadia.Main.IsSettingsOpen, "die Bibliothek soll nach dem Überspringen erscheinen");
    }
}
