using Arcadia.App.Input;
using Arcadia.App.Settings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// The UI texts as drawn (#339): the copy games see is a "virtueller Controller" everywhere the player reads about it,
/// and every row's subtitle fits on one line at 1920×1080. Walked with the gamepad only, row by row, so each row's info
/// panel is drawn too.
/// </summary>
public sealed class ConsistentTextsViewTests
{
    private static string Shown(TextBlock block) =>
        block.Text is { Length: > 0 } text ? text : string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? []);

    private static IReadOnlyList<string> VisibleTexts(ArcadiaWindow arcadia) =>
        [.. arcadia.Window.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(Shown).Where(text => text.Length > 0)];

    private static void AssertNoEmulier(IEnumerable<string> texts, string where)
    {
        var offending = texts.Where(text => text.Contains("emulier", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(offending.Count == 0, $"{where}: „virtuell“ statt „emuliert“:\n{string.Join("\n", offending)}");
    }

    /// <summary>Each row of the open category focused once with ▼, from the top; what <paramref name="check"/> sees per row.</summary>
    private static void EveryRow(ArcadiaWindow arcadia, Action<SettingsRowViewModel> check)
    {
        var category = arcadia.Arcadia.Main.Settings!.Selected;
        for (var index = 0; index < category.Rows.Count; index++)
        {
            arcadia.Settle().WaitForTransitions();
            check(category.FocusedRow ?? throw new InvalidOperationException("Keine Zeile fokussiert"));
            arcadia.Press(UiAction.Down);
        }
    }

    private static void AssertSubtitleOnOneLine(ArcadiaWindow arcadia, SettingsRowViewModel row, string where)
    {
        if (string.IsNullOrEmpty(row.Subtitle))
        {
            return;
        }

        var subtitle = arcadia.RowButton(row).GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.IsEffectivelyVisible && (Controls.ButtonTextBlock.GetText(block) ?? block.Text) == row.Subtitle);
        Assert.True(subtitle.TextLayout.TextLines.Count == 1, $"{where}: „{row.Subtitle}“ bricht um ({subtitle.TextLayout.TextLines.Count} Zeilen)");
        Assert.True(subtitle.TextLayout.WidthIncludingTrailingWhitespace <= subtitle.Bounds.Width + 0.5, $"{where}: „{row.Subtitle}“ ist abgeschnitten");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T339-14")]
    public async Task No_text_on_the_controller_page_its_info_panels_or_the_install_dialog_says_emuliert()
    {
        var steam = GuardPages.EveryPartMissing();
        steam.Pan.Running = new();
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Arcadia.Main.Settings!.Selected);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row is PanRowViewModel { IsInstall: true }), "„Treiber installieren“");

        var sawVirtual = false;
        EveryRow(arcadia, row =>
        {
            var texts = VisibleTexts(arcadia);
            AssertNoEmulier(texts, $"Zeile {row.Key}");
            sawVirtual |= texts.Any(text => text.Contains("virtuelle", StringComparison.Ordinal));
        });
        Assert.True(sawVirtual, "Die Controller-Seite nennt die Kopie für Spiele nirgends „virtuelle Controller“");

        // The driver installation: its question and the progress dialog.
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Settle();
        AssertNoEmulier(VisibleTexts(arcadia), "Frage vor der Installation");
        arcadia.Press(UiAction.Accept);
        await arcadia.Arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1, "die Übergabe");
        arcadia.Settle();
        Assert.NotNull(controller.InstallDialog);
        AssertNoEmulier(VisibleTexts(arcadia), "Installationsdialog");
        Assert.Contains(VisibleTexts(arcadia), text => text.Contains("Controller-Wächter (Pan)", StringComparison.Ordinal));
        steam.Pan.Running.SetResult();
    }

    /// <summary>No page, dialog or settings category of <see cref="GuardPages"/> says "emuliert"; the settings row by row, with each row's info panel.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T339-14")]
    public Task No_page_dialog_or_settings_category_says_emuliert() =>
        GuardWalk.EveryAsync(_ => true, (arcadia, _, state) =>
        {
            var texts = VisibleTexts(arcadia.WaitForTransitions());
            Assert.True(texts.Count > 0, $"{state}: kein Text gezeichnet");
            AssertNoEmulier(texts, state);
        }, walks: page => page.IsSettingsPage);

    [AvaloniaFact]
    [Trait("TestCase", "T339-15")]
    public async Task Every_subtitle_in_every_settings_category_and_on_the_launcher_page_takes_one_line()
    {
        var checkedRows = 0;
        await GuardWalk.EveryAsync(page => page.IsSettingsPage, (arcadia, _, state) =>
        {
            arcadia.WaitForTransitions();
            AssertSubtitleOnOneLine(arcadia, arcadia.Arcadia.Main.Settings!.Selected.FocusedRow ?? throw new InvalidOperationException("Keine Zeile fokussiert"), state);
            checkedRows++;
        });

        Assert.True(checkedRows > 20, $"nur {checkedRows} Zeilen geprüft");
    }
}
