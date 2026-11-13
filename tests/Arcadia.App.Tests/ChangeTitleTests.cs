using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.UserData;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #496: "Titel ändern", "Titel zurücksetzen" and "Original-Titel" with the controller alone (keys only for typing):
/// the title in library, search, sorting and the carousel at once, kept across a restart and a game's absence, and the
/// unreadable file of own titles.
/// </summary>
public sealed class ChangeTitleTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly DateTimeOffset InstalledAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Game[] Games =
    [
        TestGames.Installed(KnownLaunchers.Gog, "Alpha", InstalledAt),
        TestGames.Installed(KnownLaunchers.Gog, "Mitte", InstalledAt),
        TestGames.Installed(KnownLaunchers.Gog, "Zebra", InstalledAt),
    ];

    private static IReadOnlyList<LauncherSpec> Library(params Game[] games) => [new LauncherSpec(KnownLaunchers.Gog, 1, Library: games)];

    [AvaloniaFact]
    [Trait("TestCase", "T496-1")]
    [Trait("TestCase", "T496-2")]
    [Trait("TestCase", "T496-7")]
    [Trait("TestCase", "T496-8")]
    [Trait("TestCase", "T496-11")]
    public async Task A_new_title_shows_at_once_everywhere_and_moves_the_game_in_the_sort()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal(("Zebra", "3 von 3"), (arcadia.Detail!.Current.Tile.Title, arcadia.Detail.PositionText));
        Assert.False(arcadia.Detail.Current.HasOriginalTitle);

        // "Verwalten": both rows beside "Artwork ändern"; without an own title the reset is greyed but takes the focus.
        var change = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);
        var manage = arcadia.Detail.Current.OpenArea.Rows.Select(row => row.Kind).ToList();
        Assert.Equal([GameActionKind.Artwork, GameActionKind.Tags, GameActionKind.ChangeTitle, GameActionKind.ResetTitle], manage.Take(4));
        Assert.False(change.IsGreyed);
        arcadia.Press(UiAction.Down);
        var reset = arcadia.Detail.Current.FocusedRow!;
        Assert.True(reset is { Kind: GameActionKind.ResetTitle, IsGreyed: true, Note: "Das Spiel hat keinen eigenen Titel." });
        arcadia.Press(UiAction.Up);

        // A: the keyboard opens prefilled; X erases, the real keyboard types, ≡ takes it.
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.ChangeTitle!.Editor;
        Assert.Same(editor, arcadia.Main.OpenTextEditor);
        Assert.Equal("Zebra", editor.Text);
        arcadia.Press(UiAction.Search, times: "Zebra".Length);
        editor.Type("Aal");
        arcadia.Press(UiAction.Settings);

        // At once: the page's title, the focus back on "Titel ändern", the reset available, the carousel's new order.
        Assert.Null(arcadia.Main.ChangeTitle);
        var page = arcadia.Detail.Current;
        Assert.Equal(("Aal", "1 von 3"), (page.Tile.Title, arcadia.Detail.PositionText));
        Assert.Equal(GameActionKind.ChangeTitle, page.FocusedRow?.Kind);
        Assert.False(page.Row(GameActionKind.ResetTitle).IsGreyed);
        Assert.Equal(["Aal", "Alpha", "Mitte"], arcadia.Detail.Games.Select(game => game.Title));

        // R3 shows and hides "Original-Titel: Zebra", from the rows too.
        Assert.True(page.HasOriginalTitle);
        arcadia.Press(UiAction.Options);
        Assert.True(page.ShowsOriginalTitle);
        Assert.Equal("Original-Titel: Zebra", page.OriginalTitleLine);
        arcadia.Press(UiAction.Options);
        Assert.False(page.ShowsOriginalTitle);

        // "Artwork ändern" names it so.
        arcadia.Press(UiAction.Up, times: 2);
        Assert.Equal(GameActionKind.Artwork, page.FocusedRow?.Kind);
        arcadia.Press(UiAction.Accept);
        Assert.StartsWith("Aal", arcadia.Main.ChangeArtwork!.GameLine);
        arcadia.Press(UiAction.Back);

        // B, B: the library page with the game first and focused.
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
        Assert.Equal(["Aal", "Alpha", "Mitte"], arcadia.Main.Library.PageGames.Select(tile => tile.Title));
        Assert.Equal("Aal", arcadia.Main.Library.FocusedGame?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-10")]
    public async Task The_search_finds_a_renamed_game_by_both_titles_and_shows_the_own_one()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        Rename(arcadia, "Zebra", "Aal");

        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;
        search.Type("zebra");
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal(["Aal"], search.Hits.Select(game => game.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-3")]
    public async Task B_on_the_keyboard_keeps_the_title_and_Enter_on_the_real_keyboard_takes_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);

        arcadia.Press(UiAction.Accept);
        arcadia.Main.ChangeTitle!.Editor.Type(" Zwei");
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.ChangeTitle);
        Assert.Equal("Alpha", arcadia.Detail!.Current.Tile.Title);

        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.ChangeTitle!.Editor;
        editor.Type(" Zwei");
        Assert.True(editor.HandleKey(Avalonia.Input.Key.Enter));
        Assert.Null(arcadia.Main.ChangeTitle);
        Assert.Equal("Alpha Zwei", arcadia.Detail.Current.Tile.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-5")]
    [Trait("TestCase", "T496-12")]
    public async Task A_reset_brings_back_the_automatic_title_and_the_line_is_off_on_every_new_page()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        Rename(arcadia, "Zebra", "Aal");

        // The line is off after a turn and back, after the page was left, and after another tab.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("Aal", arcadia.Detail!.Current.Tile.Title);
        arcadia.Press(UiAction.Options);
        Assert.True(arcadia.Detail.Current.ShowsOriginalTitle);
        arcadia.Press(UiAction.NextPage).Press(UiAction.PreviousPage);
        Assert.False(arcadia.Detail.Current.ShowsOriginalTitle);
        arcadia.Press(UiAction.Down).Press(UiAction.Options);
        Assert.True(arcadia.Detail.Current.ShowsOriginalTitle);
        arcadia.Press(UiAction.Back);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.False(arcadia.Detail!.Current.ShowsOriginalTitle);

        // "Titel zurücksetzen": the automatic title at once on the page and in the library, the row greyed again.
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ResetTitle);
        arcadia.Press(UiAction.Accept);
        var page = arcadia.Detail.Current;
        Assert.Equal("Zebra", page.Tile.Title);
        Assert.True(page.Row(GameActionKind.ResetTitle).IsGreyed);
        Assert.Equal(GameActionKind.ResetTitle, page.FocusedRow?.Kind);
        Assert.False(page.HasOriginalTitle);
        arcadia.Press(UiAction.Options);
        Assert.False(page.ShowsOriginalTitle);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Alpha", "Mitte", "Zebra"], arcadia.Main.Library.PageGames.Select(tile => tile.Title));

        await arcadia.UserData.WhenWrittenAsync();
        using var restarted = await arcadia.RestartAsync();
        Assert.Equal(["Alpha", "Mitte", "Zebra"], restarted.Main.Library.PageGames.Select(tile => tile.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-6")]
    [Trait("TestCase", "T496-14")]
    public async Task An_own_title_survives_a_restart_a_rescan_and_the_game_leaving_and_coming_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        Rename(arcadia, "Zebra", "Aal");
        await arcadia.RescanAsync();
        Assert.Equal("Aal", arcadia.Main.Library.PageGames[0].Title);
        await arcadia.UserData.WhenWrittenAsync();

        using var withoutIt = await arcadia.RestartAsync(Library(Games[0], Games[1]));
        Assert.Equal(["Alpha", "Mitte"], withoutIt.Main.Library.PageGames.Select(tile => tile.Title));

        using var back = await withoutIt.RestartAsync(Library(Games));
        Assert.Equal(["Aal", "Alpha", "Mitte"], back.Main.Library.PageGames.Select(tile => tile.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-15")]
    public async Task An_unreadable_file_is_a_status_message_and_greys_both_rows_with_the_reason()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        Rename(arcadia, "Zebra", "Aal");
        await arcadia.UserData.WhenWrittenAsync();
        File.WriteAllText(arcadia.UserData.FilePath, "{ kaputt");

        using var restarted = await arcadia.RestartAsync();

        Assert.Equal(["Alpha", "Mitte", "Zebra"], restarted.Main.Library.PageGames.Select(tile => tile.Title));
        var entry = Assert.Single(restarted.Main.Status.Entries, e => e.Id == "user-data");
        Assert.Equal("Eigene Titel und Tags nicht lesbar", entry.Title);
        Assert.True(File.Exists(restarted.UserData.FilePath + UserDataStore.CopySuffix));

        // "Zu den Einstellungen" opens the row that says why and what to do.
        restarted.Press(UiAction.Status).Press(UiAction.Accept);
        Assert.Equal(DataSourcesSettingsViewModel.UserDataRowKey, restarted.Main.Settings!.FocusedRow?.Key);
        Assert.Equal(AppTexts.DataSourcesUserDataUnreadable, restarted.Main.Settings.FocusedRow!.Subtitle);
        Assert.Equal(HealthSeverity.Warning, restarted.Main.Settings.Selected.Marker);
        restarted.Press(UiAction.Back);

        restarted.Hold(UiAction.Accept, PastHoldTime);
        var change = restarted.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);
        Assert.True(change.IsGreyed);
        Assert.Equal(AppTexts.UserDataUnreadable, change.Note);
        Assert.True(restarted.Detail!.Current.Row(GameActionKind.ResetTitle).IsGreyed);
        restarted.Press(UiAction.Accept);
        Assert.Null(restarted.Main.ChangeTitle);
        Assert.Equal("{ kaputt", File.ReadAllText(restarted.UserData.FilePath));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T496-8")]
    public async Task From_the_game_menu_the_focus_follows_the_renamed_and_the_reset_game_across_pages()
    {
        // "Spiel 01" … "Spiel 30": with 24 per page, renaming the first to "Zz" moves it to page 2.
        Game[] games = [.. Enumerable.Range(1, 30).Select(n => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {n:00}", InstalledAt))];
        using var arcadia = await ArcadiaDriver.StartAsync(Library(games));
        Assert.Equal("Spiel 01", arcadia.FocusedGame?.Title);

        ChooseFromMenu(arcadia, AppTexts.ActionChangeTitleMore);
        arcadia.Press(UiAction.Search, times: "Spiel 01".Length);
        arcadia.Main.ChangeTitle!.Editor.Type("Zz");
        arcadia.Press(UiAction.Settings);

        Assert.Equal("2", arcadia.CurrentPage);
        Assert.Equal("Zz", arcadia.FocusedGame?.Title);

        ChooseFromMenu(arcadia, AppTexts.ActionResetTitle);

        Assert.Equal("1", arcadia.CurrentPage);
        Assert.Equal("Spiel 01", arcadia.FocusedGame?.Title);
    }

    [AvaloniaFact]
    public async Task A_providers_batch_that_changes_no_shown_title_leaves_the_library_as_it_is()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Games));
        var rebuilt = 0;
        arcadia.Main.Session.LibrariesChanged += (_, _) => rebuilt++;

        arcadia.Main.Session.Titles.AutomaticTitlesChanged([Games[0].Key, Games[2].Key, "gog:not-in-the-library"]);

        Assert.Equal(0, rebuilt);
    }

    /// <summary>In the library: the focused game's menu (≡ held, here Options), then the action labelled <paramref name="label"/>.</summary>
    private static void ChooseFromMenu(ArcadiaDriver arcadia, string label)
    {
        arcadia.Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!;
        for (var turns = 0; !menu.Actions.Single(action => action.Label == label).IsFocused; turns++)
        {
            Assert.True(turns < 10, $"{label} can't be focused in the menu.");
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
    }

    /// <summary>From the library: the game's detail page, "Titel ändern", the new title, ≡, and back to the library.</summary>
    private static void Rename(ArcadiaDriver arcadia, string from, string to)
    {
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        while (arcadia.Detail!.Current.Tile.Title != from)
        {
            arcadia.Press(UiAction.NextPage);
        }

        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Search, times: from.Length);
        arcadia.Main.ChangeTitle!.Editor.Type(to);
        arcadia.Press(UiAction.Settings);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
    }
}
