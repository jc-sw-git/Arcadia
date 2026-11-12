using Arcadia.App.Filter;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Search;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.App.Status;
using Arcadia.App.ViewModels;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia.Controls;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// Every page, dialog and settings category of the window, the one list the cross-page guards walk (#518): a new one is
/// one entry here, and <c>GuardPageListTests</c> fails while a view model a player sees is missing from it.
/// </summary>
internal static class GuardPages
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    public static IReadOnlyList<GuardPage> All { get; } =
    [
        new("Bibliothek", [typeof(LibraryViewModel)], Walk: Library,
            Focused: arcadia => arcadia.Tile(arcadia.Arcadia.FocusedGame!), RingsAtLeast: 4),
        new("Leerer Tab", [typeof(EmptyTabViewModel)], Walk: arcadia => Presses(arcadia, UiAction.Right),
            Start: () => ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Gog, 0)])),
        new("Ansicht", [typeof(ViewOptionsViewModel)], Pressing(UiAction.ViewOptions),
            arcadia => Presses(arcadia, UiAction.Right, UiAction.Right, UiAction.Down, UiAction.Right, UiAction.Down, UiAction.Down, UiAction.Left)),
        new("Spielmenü", [typeof(GameMenuViewModel)], Pressing(UiAction.Options),
            arcadia => Down(arcadia, arcadia.Arcadia.Main.GameMenu!.Actions.Count)),
        new("Bilder ändern", [typeof(ChangeArtworkViewModel)], OpenChangeArtwork),
        new("Filter", [typeof(FilterModalViewModel)], Pressing(UiAction.KeyboardLayout),
            arcadia => Down(arcadia, arcadia.Arcadia.Main.FilterModal!.Categories.Count)),
        new("Statusmeldungen", [typeof(StatusModalViewModel)], OpenStatusModalAsync, Start: StartWithEveryControllerPartMissingAsync),
        new("Detailseite", [typeof(GameDetailViewModel), typeof(GameDetailPageViewModel)], OpenDetail,
            arcadia => Down(arcadia, arcadia.Arcadia.Detail!.Current.Areas.Count)),
        new("Bestätigung", [typeof(ConfirmationViewModel)], OpenConfirmation, arcadia => Presses(arcadia, UiAction.Left)),
        new("Übersicht anpassen", [typeof(CustomizeOverviewViewModel)], OpenCustomizeOverview,
            arcadia => Presses(arcadia, UiAction.Down, UiAction.Down, UiAction.Down)),
        new("Suche", [typeof(SearchViewModel)], Pressing(UiAction.Search), Search, RingsAtLeast: 30,
            RingFinding: ("mit Treffern", "Der Rahmen des fokussierten Treffers in der ersten Reihe ist oben nur halb so hell (#518).")),
        new("Tastaturlayout der Suche", [typeof(LayoutMenuViewModel)], Pressing(UiAction.Search, UiAction.KeyboardLayout),
            arcadia => Down(arcadia, arcadia.Arcadia.Search!.Keyboard.LayoutMenu.Items.Count),
            Focused: arcadia => GuardPage.FocusedButton(arcadia, "layoutItem")),
        new("Einstellungen › Launcher", [typeof(SettingsPageViewModel), typeof(LauncherSettingsViewModel)], OpenCategoryAsync<LauncherSettingsViewModel>, Rows)
            { IsSettingsPage = true },
        new("Launcherseite", [], OpenLauncherPageAsync, Rows) { IsSettingsPage = true },
        new("Einstellungen › Controller", [typeof(ControllerSettingsViewModel)], OpenCategoryAsync<ControllerSettingsViewModel>, Rows,
            Start: StartWithEveryControllerPartMissingAsync) { IsSettingsPage = true },
        // Nothing on it takes the focus: it only shows the progress.
        new("Treiberinstallation", [typeof(DriverInstallDialogViewModel)], OpenDriverInstallAsync, Start: StartWithEveryControllerPartMissingAsync, Focused: _ => null),
        GuardPage.NotWalkedYet("Controller zuordnen", [typeof(ControllerLayoutsViewModel)], "„Controller zuordnen“ gibt es nur mit dem Steam Controller unter Windows."),
        new("Einstellungen › Datenquellen", [typeof(DataSourcesSettingsViewModel)], OpenCategoryAsync<DataSourcesSettingsViewModel>, Rows) { IsSettingsPage = true },
        new("Einstellungen › Über Arcadia", [typeof(AboutSettingsViewModel)], OpenCategoryAsync<AboutSettingsViewModel>, Rows) { IsSettingsPage = true },
        GuardPage.NotWalkedYet("Frage in den Einstellungen", [typeof(SettingsQuestionViewModel)], "Fragt nur nach Änderungen an Bildern oder Controller-Modi, die der Test erst anlegen müsste."),
        GuardPage.NotWalkedYet("Bearbeitungsfeld", [typeof(TextEditorViewModel)], "Ein Textfeld in einer Zeile; die Guards sehen es mit seiner Zeile."),
        GuardPage.NotWalkedYet("Warteliste", [typeof(WaitingListViewModel), typeof(WaitingHitsViewModel)], "Braucht Spiele, die auf die Bildersuche warten."),
        new("Erster Start", [typeof(FirstRunViewModel)],
            Walk: arcadia => Down(arcadia, arcadia.Arcadia.Main.FirstRun!.Found.Count + arcadia.Arcadia.Main.FirstRun!.Missing.Count),
            Start: () => ArcadiaWindow.ShowAsync(settings: new AppSettings())),
        new("Erster Start › Datenquellen", [typeof(FirstRunDataSourcesViewModel)], Pressing(UiAction.Settings), Rows,
            Start: () => ArcadiaWindow.ShowAsync(settings: new AppSettings())) { IsSettingsPage = true },
        GuardPage.NotWalkedYet("Anmeldung im Browser", [typeof(WebPageViewModel)], "Zeigt den Browser des Systems, den das Fenster ohne Bildschirm nicht zeichnet."),
        GuardPage.NotWalkedYet("Firmware-Hinweis", [typeof(FirmwareAttemptViewModel)], "Erscheint nur, wenn Steam die Firmware des Steam Controllers schreiben will."),
        GuardPage.NotWalkedYet("Pan-Update", [typeof(PanUpdatePageViewModel)], "Erscheint nur beim Start mit einem fremden Pan."),
    ];

    /// <summary>Every part missing: the controller page shows all its part rows and "Treiber installieren".</summary>
    public static FakeSteam EveryPartMissing()
    {
        var steam = new FakeSteam { IsUsbipReady = false, IsPanReady = false, HidHideVersion = null };
        steam.Viiper.IsInstalled = false;
        return steam;
    }

    private static Func<ArcadiaWindow, Task> Pressing(params UiAction[] actions) => arcadia =>
    {
        foreach (var action in actions)
        {
            arcadia.Press(action);
        }

        return Task.CompletedTask;
    };

    private static IEnumerable<string> Presses(ArcadiaWindow arcadia, params UiAction[] moves)
    {
        yield return "offen";
        foreach (var move in moves)
        {
            arcadia.Press(move);
            yield return $"nach {move}";
        }
    }

    private static IEnumerable<string> Down(ArcadiaWindow arcadia, int count) => Presses(arcadia, [.. Enumerable.Repeat(UiAction.Down, Math.Max(0, count - 1))]);

    private static IEnumerable<string> Library(ArcadiaWindow arcadia)
    {
        yield return "erste Seite";
        arcadia.Press(UiAction.NextPage);
        yield return "zweite Seite";
        arcadia.Press(UiAction.PreviousPage);
    }

    private static IEnumerable<string> Search(ArcadiaWindow arcadia)
    {
        yield return "leer";
        arcadia.Type("a");
        yield return "mit Treffern";
    }

    /// <summary>Each row of the category or launcher page shown, focused once with ▼ from the top.</summary>
    private static IEnumerable<string> Rows(ArcadiaWindow arcadia)
    {
        var category = arcadia.Arcadia.Main.Settings!.Selected;
        for (var index = 0; index < category.Rows.Count; index++)
        {
            yield return $"Zeile {category.FocusedRow?.Key}";
            arcadia.Press(UiAction.Down);
        }
    }

    private static Task<ArcadiaWindow> StartWithEveryControllerPartMissingAsync() => ArcadiaWindow.ShowAsync(
        [new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)],
        new AppSettings { PanEnabled = true, ActiveLaunchers = ["steam", "gog"] }, steam: EveryPartMissing());

    private static async Task OpenCategoryAsync<T>(ArcadiaWindow arcadia)
        where T : SettingsCategoryViewModel
    {
        arcadia.Press(UiAction.Settings);
        for (var tab = 0; tab < 4 && arcadia.Arcadia.Main.Settings!.Selected is not T; tab++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Main.Settings!.Selected is T { Rows.Count: > 0 }, typeof(T).Name);
        arcadia.Settle();
    }

    private static async Task OpenLauncherPageAsync(ArcadiaWindow arcadia)
    {
        await OpenCategoryAsync<LauncherSettingsViewModel>(arcadia);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.LauncherCategory().PageLauncherId);
    }

    private static Task OpenDetail(ArcadiaWindow arcadia)
    {
        arcadia.Arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen, "Detailseite offen");
        arcadia.Settle();
        return Task.CompletedTask;
    }

    private static async Task OpenConfirmation(ArcadiaWindow arcadia)
    {
        await OpenDetail(arcadia);
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Arcadia.Main.Confirmation);
    }

    /// <summary>From the detail page's quick selection: into "Übersicht", up to "Übersicht anpassen", A.</summary>
    private static async Task OpenCustomizeOverview(ArcadiaWindow arcadia)
    {
        await OpenDetail(arcadia);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Accept);
        Assert.NotNull(arcadia.Arcadia.Main.CustomizeOverview);
    }

    private static Task OpenChangeArtwork(ArcadiaWindow arcadia)
    {
        arcadia.Press(UiAction.Options);
        var menu = arcadia.Arcadia.Main.GameMenu!;
        for (var item = 0; item < menu.Actions.Count && menu.Actions.Single(action => action.IsFocused).Kind != GameActionKind.Artwork; item++)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Arcadia.Main.ChangeArtwork);
        return Task.CompletedTask;
    }

    private static async Task OpenStatusModalAsync(ArcadiaWindow arcadia)
    {
        await arcadia.Arcadia.TickControllerSupportAsync();
        arcadia.Press(UiAction.Status);
        Assert.True(arcadia.Arcadia.Main.IsStatusModalOpen, "Statusmeldungen offen");
    }

    /// <summary>The progress dialog of the driver installation.</summary>
    private static async Task OpenDriverInstallAsync(ArcadiaWindow arcadia)
    {
        await OpenCategoryAsync<ControllerSettingsViewModel>(arcadia);
        var controller = (ControllerSettingsViewModel)arcadia.Arcadia.Main.Settings!.Selected;
        await arcadia.Arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row is PanRowViewModel { IsInstall: true }), "„Treiber installieren“");
        Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey)).Activate();
        arcadia.Settle().Press(UiAction.Accept);
        await arcadia.Arcadia.WaitUntilAsync(() => controller.InstallDialog is not null, "der Installationsdialog");
        arcadia.Settle();
    }
}
