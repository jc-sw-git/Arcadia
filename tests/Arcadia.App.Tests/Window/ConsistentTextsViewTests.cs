using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// The UI texts as drawn (#339): the copy games see is a "virtueller Controller" everywhere the player reads about it,
/// and every row's subtitle fits on one line at 1920×1080. Walked with the gamepad only, row by row, so each row's info
/// panel is drawn too.
/// </summary>
public sealed class ConsistentTextsViewTests
{
    private static readonly IReadOnlyList<LauncherSpec> Launchers = LauncherSettingsDriving.WithoutFolders;

    /// <summary>Every part missing: the controller page shows all its part rows and "Treiber installieren".</summary>
    private static FakeSteam EveryPartMissing()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        return steam;
    }

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
        var steam = EveryPartMissing();
        steam.Pan.Running = new();
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: steam);
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Arcadia.Main.Settings!.Selected);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row is PanRowViewModel { IsInstall: true }), "„Treiber installieren“");

        EveryRow(arcadia, row => AssertNoEmulier(VisibleTexts(arcadia), $"Zeile {row.Key}"));

        // The driver installation: its question and the progress dialog.
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Settle();
        AssertNoEmulier(VisibleTexts(arcadia), "Frage vor der Installation");
        arcadia.Press(UiAction.Accept);
        await arcadia.Arcadia.WaitUntilAsync(() => steam.Pan.HandOvers == 1, "die Übergabe");
        arcadia.Settle();
        Assert.NotNull(controller.InstallDialog);
        AssertNoEmulier(VisibleTexts(arcadia), "Installationsdialog");
        Assert.Contains(VisibleTexts(arcadia), text => text.Contains("Controller-Wächter (Pan)", StringComparison.Ordinal) || text.Contains("Pan", StringComparison.Ordinal));
        steam.Pan.Running.SetResult();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T339-14")]
    public async Task The_first_start_says_nothing_about_emulated_controllers()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Amazon, 6, IsInstalled: false)], new AppSettings());
        Assert.True(arcadia.Arcadia.Main.IsFirstRunShown);

        var texts = VisibleTexts(arcadia);

        Assert.Contains(AppTexts.FirstRunExplanation, texts);
        AssertNoEmulier(texts, "Erster Start");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T339-15")]
    public async Task Every_subtitle_in_every_settings_category_and_on_the_launcher_page_takes_one_line()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers, steam: EveryPartMissing());
        arcadia.Press(UiAction.Settings);
        var checkedRows = 0;
        void Check(SettingsRowViewModel row, string where)
        {
            AssertSubtitleOnOneLine(arcadia, row, where);
            checkedRows++;
        }

        // Launcher: the list, then the page of its first launcher.
        EveryRow(arcadia, row => Check(row, $"Launcher › {row.Key}"));
        var launchers = arcadia.LauncherCategory();
        for (var press = 0; press < launchers.Rows.Count && launchers.FocusedRow is not LauncherRowViewModel; press++)
        {
            arcadia.Press(UiAction.Up);
        }

        arcadia.Press(UiAction.Accept).Settle();
        Assert.True(launchers.PageLauncherId is not null, $"focused {launchers.FocusedRow?.GetType().Name} {launchers.FocusedRow?.Key}; rows {string.Join(",", launchers.Rows.Select(r => r.GetType().Name + ":" + r.Key))}");
        EveryRow(arcadia, row => Check(row, $"Launcherseite › {row.Key}"));
        arcadia.Press(UiAction.Back).Settle();

        foreach (var category in new[] { "Controller", "Über Arcadia" })
        {
            arcadia.Press(UiAction.NextTab).Settle();
            await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Main.Settings!.Selected.Rows.Count > 0, category);
            EveryRow(arcadia, row => Check(row, $"{category} › {row.Key}"));
        }

        Assert.True(checkedRows > 20, $"nur {checkedRows} Zeilen geprüft");
    }
}
