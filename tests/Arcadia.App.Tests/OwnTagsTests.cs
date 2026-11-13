using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Metadata;
using Arcadia.Core.UserData;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #495: own tags with the controller alone (keys only for typing): the tag window from the detail page and the context
/// menu, the row "Tags", own tags before the providers' on the detail page, the tag list in "Datenquellen" with its
/// games, rename, merge and delete, kept across a restart and a game's absence, and the unreadable file.
/// </summary>
public sealed class OwnTagsTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly DateTimeOffset InstalledAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Game Alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", InstalledAt);
    private static readonly Game Mitte = TestGames.Installed(KnownLaunchers.Gog, "Mitte", InstalledAt);
    private static readonly Game Zebra = TestGames.NotInstalled(KnownLaunchers.Gog, "Zebra");
    private static readonly Game EpicGame = TestGames.Installed(KnownLaunchers.Epic, "Eisenflut", InstalledAt);

    private static IReadOnlyList<LauncherSpec> Library(params Game[] games) => [new LauncherSpec(KnownLaunchers.Gog, 1, Library: games)];

    private static IReadOnlyList<LauncherSpec> TwoLaunchers() =>
        [new LauncherSpec(KnownLaunchers.Gog, 1, Library: [Alpha, Mitte]), new LauncherSpec(KnownLaunchers.Epic, 2, Library: [EpicGame])];

    [AvaloniaFact]
    [Trait("TestCase", "T495-1")]
    [Trait("TestCase", "T495-3")]
    [Trait("TestCase", "T495-4")]
    [Trait("TestCase", "T495-7")]
    [Trait("TestCase", "T495-8")]
    public async Task A_new_tag_from_the_detail_page_is_ticked_for_the_game_and_shows_before_the_providers_tags()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha, Mitte, Zebra));
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        // "Verwalten": "Tags" after "Artwork ändern", for every game; without tags its value says "Keine".
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        Assert.Equal([GameActionKind.Artwork, GameActionKind.Tags, GameActionKind.ChangeTitle], arcadia.Detail!.Current.OpenArea.Rows.Select(r => r.Kind).Take(3));
        Assert.Equal(("Keine", true, false), (row.ValueText, row.HasActionValue, row.IsGreyed));
        Assert.Equal(AppTexts.DetailTagsInfo, row.InfoText);

        // A: the window, "Neuer Tag" first and focused; hints A "Zuordnen", Y greyed there, B "Schließen".
        arcadia.Press(UiAction.Accept);
        var window = arcadia.Main.TagWindow!;
        Assert.Equal("Tags · Alpha", window.Heading);
        Assert.Equal(["Neuer Tag"], window.Entries.Select(e => e.Text));
        Assert.True(window.IsNewFocused);
        Assert.Equal("Zuordnen", TagWindowViewModel.AcceptHint);

        // A on "Neuer Tag": the keyboard, empty; ↵ makes the tag ticked for the game.
        arcadia.Press(UiAction.Accept);
        var editor = window.Editor!;
        Assert.Same(editor, arcadia.Main.OpenTextEditor);
        Assert.Equal(string.Empty, editor.Text);
        editor.Type("  Couch-Koop ");
        arcadia.Press(UiAction.Settings);
        Assert.Null(window.Editor);
        var tag = Assert.Single(window.Entries, e => e.IsTag);
        Assert.True(tag is { Name: "Couch-Koop", IsTicked: true, IsFocused: true });

        // The page behind follows at once: the row's value, "Übersicht"'s tags.
        Assert.Equal("1 Tag", arcadia.Detail.Current.Row(GameActionKind.Tags).ValueText);
        Assert.Equal([new DetailTag("Couch-Koop", true)], arcadia.Detail.Current.GameInfo.OverviewTags);
        Assert.Equal(["Couch-Koop"], arcadia.Detail.Current.GameInfo.OwnTags);

        // A unticks at once, A ticks again.
        arcadia.Press(UiAction.Accept);
        Assert.False(window.Entries[1].IsTicked);
        Assert.Equal("Keine", arcadia.Detail.Current.Row(GameActionKind.Tags).ValueText);
        Assert.False(arcadia.Detail.Current.GameInfo.HasOverviewTags);
        arcadia.Press(UiAction.Accept);

        // An existing name in another case makes no second tag; B on the keyboard makes nothing.
        arcadia.Press(UiAction.Up).Press(UiAction.Accept);
        window.Editor!.Type("COUCH-koop");
        arcadia.Press(UiAction.Settings);
        Assert.Equal(["Neuer Tag", "Couch-Koop"], window.Entries.Select(e => e.Text));
        arcadia.Press(UiAction.Up).Press(UiAction.Accept);
        window.Editor!.Type("Zu zweit");
        arcadia.Press(UiAction.Back);
        Assert.Equal(["Neuer Tag", "Couch-Koop"], window.Entries.Select(e => e.Text));
        arcadia.Press(UiAction.Accept);
        window.Editor!.Type("   ");
        arcadia.Press(UiAction.Settings);
        Assert.Equal(["Neuer Tag", "Couch-Koop"], window.Entries.Select(e => e.Text));

        // B closes, the focus back on the row "Tags".
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.TagWindow);
        Assert.Equal(GameActionKind.Tags, arcadia.Detail.Current.FocusedRow?.Kind);

        // A game not installed has the row too.
        while (arcadia.Detail.Current.Tile.Title != "Zebra")
        {
            arcadia.Press(UiAction.NextPage);
        }

        Assert.False(arcadia.Detail.Current.Row(GameActionKind.Tags).IsGreyed);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-2")]
    public async Task The_context_menu_opens_the_same_window_and_B_returns_to_the_focused_game()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha, Mitte, Zebra));
        arcadia.Press(UiAction.Right);
        Assert.Equal("Mitte", arcadia.FocusedGame?.Title);

        ChooseFromMenu(arcadia, AppTexts.ActionTagsMore);

        Assert.Equal("Tags · Mitte", arcadia.Main.TagWindow?.Heading);
        arcadia.Press(UiAction.Accept);
        arcadia.Main.TagWindow!.Editor!.Type("Zu zweit");
        arcadia.Press(UiAction.Settings);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.TagWindow);
        Assert.Null(arcadia.Main.GameMenu);
        Assert.Equal("Mitte", arcadia.FocusedGame?.Title);
        Assert.Equal(["Zu zweit"], arcadia.Main.Tags.Of(Mitte.Key));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-5")]
    [Trait("TestCase", "T495-6")]
    public async Task Y_deletes_a_tag_from_every_game_after_asking_and_nothing_on_Neuer_Tag()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha, Mitte, Zebra));
        arcadia.Main.Tags.Create("Couch-Koop", Alpha.Key);
        arcadia.Main.Tags.Create("Couch-Koop", Mitte.Key);
        arcadia.Main.Tags.Create("Zu zweit", Alpha.Key);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        arcadia.Press(UiAction.Accept);
        var window = arcadia.Main.TagWindow!;
        Assert.Equal(["Neuer Tag", "Couch-Koop", "Zu zweit"], window.Entries.Select(e => e.Text));

        // Y on "Neuer Tag" asks nothing; ▲ goes round to the last tag.
        arcadia.Press(UiAction.Status);
        Assert.Null(window.Question);
        arcadia.Press(UiAction.Up);
        Assert.Equal("Zu zweit", window.Entries.Single(e => e.IsFocused).Name);
        arcadia.Press(UiAction.Down);
        Assert.True(window.IsNewFocused);
        arcadia.Press(UiAction.Down);

        // Y asks, naming how many games lose it; B and "Abbrechen" leave everything.
        arcadia.Press(UiAction.Status);
        Assert.Equal(("„Couch-Koop“ löschen?", "2 Spiele verlieren den Tag."), (window.Question!.Title, window.Question.Message));
        Assert.True(window.Question.IsCancelFocused);
        arcadia.Press(UiAction.Back);
        Assert.Null(window.Question);
        arcadia.Press(UiAction.Status).Press(UiAction.Accept);
        Assert.Equal(2, arcadia.Main.Tags.Find("Couch-Koop")!.Games.Count);

        // Yes: gone from every game and from the window; the focus on the next tag.
        arcadia.Press(UiAction.Status).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Tags.Find("Couch-Koop"));
        Assert.Empty(arcadia.Main.Tags.Of(Mitte.Key));
        Assert.Equal(["Neuer Tag", "Zu zweit"], window.Entries.Select(e => e.Text));
        Assert.Equal("Zu zweit", window.Entries.Single(e => e.IsFocused).Name);
        Assert.Equal("1 Tag", arcadia.Detail!.Current.Row(GameActionKind.Tags).ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-6")]
    public async Task Two_hundred_tags_are_walked_round_and_a_held_button_stops_at_the_end()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha));
        for (var n = 1; n <= 200; n++)
        {
            arcadia.Main.Tags.Create($"Tag {n:000}");
        }

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        arcadia.Press(UiAction.Accept);
        var window = arcadia.Main.TagWindow!;
        Assert.Equal(201, window.Entries.Count);

        arcadia.Press(UiAction.Up);
        Assert.Equal("Tag 200", window.Entries.Single(e => e.IsFocused).Name);
        arcadia.Press(UiAction.Down);
        Assert.True(window.IsNewFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-9")]
    public async Task Tags_is_a_quick_selection_entry_with_its_value_and_five_entries_exist()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha));
        arcadia.Main.Tags.Create("Couch-Koop", Alpha.Key);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        var entry = Assert.Single(page.Overview.Rows, r => r.Kind == GameActionKind.Tags);
        Assert.Equal("1 Tag", entry.ValueText);
        Assert.Contains(DetailAreas.QuickIdOf(GameActionKind.Tags), DetailAreas.QuickDefaults);

        // A on the entry opens the window.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        while (page.FocusedRow?.Kind != GameActionKind.Tags)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.TagWindow);
    }

    [Fact]
    [Trait("TestCase", "T495-8")]
    [Trait("TestCase", "T495-10")]
    public void Own_tags_come_first_and_a_providers_tag_of_the_same_name_shows_once_as_the_own_one()
    {
        var data = MergedMetadata.None with { Tags = new Sourced<IReadOnlyList<string>>(["Indie", "couch-koop", "Puzzle"], "Steam") };

        var shown = new DetailMetadata(data, ["Couch-Koop", "Zu zweit"]);

        Assert.Equal(
            [new DetailTag("Couch-Koop", true), new DetailTag("Zu zweit", true), new DetailTag("Indie", false)],
            shown.OverviewTags);
        Assert.Equal("+1", shown.MoreTags);
        Assert.Equal(["Indie", "Puzzle"], shown.Tags);
        Assert.Equal(["Couch-Koop", "Zu zweit"], shown.OwnTags);
        Assert.False(new DetailMetadata(MergedMetadata.None, []).HasOverviewTags);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-10")]
    [Trait("TestCase", "T495-21")]
    public async Task Own_tags_survive_a_rescan_a_restart_and_the_game_leaving_and_coming_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha, Mitte));
        arcadia.Main.Tags.Create("Couch-Koop", Mitte.Key);
        arcadia.Main.Tags.Create("Weg", Mitte.Key);
        await arcadia.RescanAsync();
        Assert.Equal(["Couch-Koop", "Weg"], arcadia.Main.Tags.Of(Mitte.Key));
        await arcadia.UserData.WhenWrittenAsync();

        using var withoutIt = await arcadia.RestartAsync(Library(Alpha));
        // Absent, it isn't counted, but rename and delete reach it.
        Assert.Equal(0, LibraryCount(withoutIt, "Couch-Koop"));
        withoutIt.Main.Tags.Rename("Couch-Koop", "Sofa");
        withoutIt.Main.Tags.Delete("Weg");
        await withoutIt.UserData.WhenWrittenAsync();

        using var back = await withoutIt.RestartAsync(Library(Alpha, Mitte));
        Assert.Equal(["Sofa"], back.Main.Tags.Of(Mitte.Key));
        back.Hold(UiAction.Accept, PastHoldTime);
        back.Press(UiAction.NextPage);
        Assert.Equal(["Sofa"], back.Detail!.Current.GameInfo.OwnTags);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-11")]
    public async Task A_tag_is_one_across_launchers()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoLaunchers());
        arcadia.Main.Tags.Create("Couch-Koop", Alpha.Key);
        arcadia.Main.Tags.Create("couch-koop", EpicGame.Key);

        var list = OpenTagList(arcadia);

        var entry = Assert.Single(list.Entries, e => e.IsTag);
        Assert.Equal(("Couch-Koop", "2 Spiele"), (entry.Name, entry.CountText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-13")]
    [Trait("TestCase", "T495-14")]
    [Trait("TestCase", "T495-15")]
    [Trait("TestCase", "T495-19")]
    [Trait("TestCase", "T495-27")]
    public async Task The_tag_list_makes_tags_shows_their_games_and_leads_to_a_games_detail_page_and_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoLaunchers());
        arcadia.Main.Tags.Create("Zu zweit", Mitte.Key);
        arcadia.Main.Tags.Create("Zu zweit", EpicGame.Key);

        var category = OpenDataSources(arcadia);
        var row = arcadia.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        Assert.Equal(("Eigene Tags (1)", AppTexts.DataSourcesUserDataGroup), (row.Title, row.Group));

        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.TagList!;
        Assert.Equal(["Neuer Tag", "Zu zweit"], list.Entries.Select(e => e.Text));
        Assert.Equal(("1 Tag", "2 Spiele"), (list.Subtitle, list.Entries[1].CountText));
        Assert.Equal(("Anlegen", false), (list.AcceptHint, list.CanChangeFocused));

        // "Neuer Tag": a tag without games, sorted in; the row's count follows.
        arcadia.Press(UiAction.Accept);
        list.Editor!.Type("Abends");
        arcadia.Press(UiAction.Settings);
        Assert.Equal(["Neuer Tag", "Abends", "Zu zweit"], list.Entries.Select(e => e.Text));
        Assert.Equal(("Abends", "0 Spiele"), (list.Entries.Single(e => e.IsFocused).Name, list.Entries[1].CountText));
        Assert.Equal("2 Tags", list.Subtitle);
        Assert.Equal("Eigene Tags (2)", category.Rows.Single(r => r.Key == DataSourcesSettingsViewModel.OwnTagsRowKey).Title);
        Assert.Equal(("Spiele zeigen", true), (list.AcceptHint, list.CanChangeFocused));

        // A on a tag without games says so.
        arcadia.Press(UiAction.Accept);
        Assert.True(list.Games!.IsEmpty);
        arcadia.Press(UiAction.Back);
        Assert.Null(list.Games);

        // A on "Zu zweit": its games alphabetically with their launchers.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        var games = list.Games!;
        Assert.Equal("Zu zweit", list.Subtitle);
        Assert.Equal([("Eisenflut", "Epic Games"), ("Mitte", "GOG Galaxy")], games.Items.Select(g => (g.Title, g.LauncherName)));

        // A on a game: its detail page over the tag's games only, no launcher tab; LT/RT turn through them.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Settings);
        Assert.Null(arcadia.Main.TagList);
        Assert.Equal(("Mitte", "2 von 2"), (arcadia.Detail!.Current.Tile.Title, arcadia.Detail.PositionText));
        Assert.False(arcadia.Main.IsTabStripShown);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Eisenflut", arcadia.Detail.Current.Tile.Title);

        // B: the tag's games with the focus on the game shown last; B, B: the tags, then "Datenquellen" on the row.
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
        list = arcadia.Main.TagList!;
        Assert.Equal("Eisenflut", list.Games!.Items.Single(g => g.IsFocused).Title);
        Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
        arcadia.Press(UiAction.Back);
        Assert.Equal("Zu zweit", list.Entries.Single(e => e.IsFocused).Name);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.TagList);
        Assert.Equal(DataSourcesSettingsViewModel.OwnTagsRowKey, arcadia.Main.Settings!.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-14")]
    public async Task Neuer_Tag_in_the_list_with_an_existing_name_focuses_that_tag()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha));
        arcadia.Main.Tags.Create("Abends");
        arcadia.Main.Tags.Create("Zu zweit");
        var list = OpenTagList(arcadia);

        arcadia.Press(UiAction.Accept);
        list.Editor!.Type("zu ZWEIT");
        arcadia.Press(UiAction.Settings);

        Assert.Equal(["Neuer Tag", "Abends", "Zu zweit"], list.Entries.Select(e => e.Text));
        Assert.Equal("Zu zweit", list.Entries.Single(e => e.IsFocused).Name);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-16")]
    [Trait("TestCase", "T495-17")]
    [Trait("TestCase", "T495-27")]
    public async Task X_renames_and_another_tags_name_asks_to_merge()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha, Mitte, Zebra));
        arcadia.Main.Tags.Create("Couch-Koop", Alpha.Key);
        arcadia.Main.Tags.Create("Couch-Koop", Mitte.Key);
        arcadia.Main.Tags.Create("Zu Zweit", Mitte.Key);
        arcadia.Main.Tags.Create("Zu Zweit", Zebra.Key);
        var list = OpenTagList(arcadia);

        // X on "Neuer Tag" does nothing; on a tag the keyboard comes prefilled; B changes nothing.
        arcadia.Press(UiAction.Search);
        Assert.Null(list.Editor);
        arcadia.Press(UiAction.Down).Press(UiAction.Search);
        Assert.Equal("Couch-Koop", list.Editor!.Text);
        arcadia.Press(UiAction.Back);
        Assert.Equal(["Neuer Tag", "Couch-Koop", "Zu Zweit"], list.Entries.Select(e => e.Text));

        // A rename sorts it in again.
        arcadia.Press(UiAction.Search);
        arcadia.Press(UiAction.Search, times: "Couch-Koop".Length);
        list.Editor!.Type("Sofa");
        arcadia.Press(UiAction.Settings);
        Assert.Equal(["Neuer Tag", "Sofa", "Zu Zweit"], list.Entries.Select(e => e.Text));
        Assert.Equal(["Sofa"], arcadia.Main.Tags.Of(Alpha.Key));

        // Another tag's name in another case: the merge question; no leaves both.
        arcadia.Press(UiAction.Search);
        arcadia.Press(UiAction.Search, times: "Sofa".Length);
        list.Editor!.Type("zu zweit");
        arcadia.Press(UiAction.Settings);
        Assert.Equal("Mit „Zu Zweit“ zusammenführen?", list.Question!.Title);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(["Neuer Tag", "Sofa", "Zu Zweit"], list.Entries.Select(e => e.Text));

        // Yes: one tag under the existing name, each game once, the counts added up without doubles.
        arcadia.Press(UiAction.Search);
        arcadia.Press(UiAction.Search, times: "Sofa".Length);
        list.Editor!.Type("zu zweit");
        arcadia.Press(UiAction.Settings).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal(["Neuer Tag", "Zu Zweit"], list.Entries.Select(e => e.Text));
        Assert.Equal(("Zu Zweit", "3 Spiele"), (list.Entries.Single(e => e.IsFocused).Name, list.Entries[1].CountText));
        Assert.Equal(["Zu Zweit"], arcadia.Main.Tags.Of(Mitte.Key));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-18")]
    public async Task Y_in_the_list_deletes_and_the_focus_goes_to_the_next_the_one_before_or_Neuer_Tag()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha));
        arcadia.Main.Tags.Create("Abends");
        arcadia.Main.Tags.Create("Mittags");
        arcadia.Main.Tags.Create("Nachts");
        var list = OpenTagList(arcadia);

        arcadia.Press(UiAction.Down).Press(UiAction.Status);
        Assert.Equal("Kein Spiel hat diesen Tag.", list.Question!.Message);
        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal("Mittags", list.Entries.Single(e => e.IsFocused).Name);

        arcadia.Press(UiAction.Down).Press(UiAction.Status).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal("Mittags", list.Entries.Single(e => e.IsFocused).Name);

        arcadia.Press(UiAction.Status).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.True(list.IsNewFocused);
        Assert.Equal("Eigene Tags (0)", arcadia.Main.Settings!.Selected.Rows.Single(r => r.Key == DataSourcesSettingsViewModel.OwnTagsRowKey).Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T495-24")]
    [Trait("TestCase", "T495-31")]
    public async Task An_unreadable_file_greys_Tags_and_Eigene_Tags_with_the_note_and_changes_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library(Alpha));
        arcadia.Main.Tags.Create("Couch-Koop", Alpha.Key);
        await arcadia.UserData.WhenWrittenAsync();
        File.WriteAllText(arcadia.UserData.FilePath, "{ kaputt");

        using var restarted = await arcadia.RestartAsync();

        var entry = Assert.Single(restarted.Main.Status.Entries, e => e.Id == "user-data");
        Assert.Equal("Eigene Titel und Tags nicht lesbar", entry.Title);
        Assert.True(File.Exists(restarted.UserData.FilePath + UserDataStore.CopySuffix));

        restarted.Hold(UiAction.Accept, PastHoldTime);
        var row = restarted.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        Assert.True(row.IsGreyed);
        Assert.Equal(AppTexts.UserDataUnreadable, row.Note);
        Assert.StartsWith("Eigene Tags und Titel sind gerade nicht lesbar", row.Note);
        Assert.Empty(restarted.Detail!.Current.GameInfo.OwnTags);
        restarted.Press(UiAction.Accept);
        Assert.Null(restarted.Main.TagWindow);
        restarted.Press(UiAction.Back).Press(UiAction.Back);

        OpenDataSources(restarted);
        var tagsRow = restarted.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        Assert.True(tagsRow.IsDimmed);
        Assert.Equal(AppTexts.UserDataUnreadable, tagsRow.DimmedNote);
        restarted.Press(UiAction.Accept);
        Assert.Null(restarted.Main.TagList);
        Assert.Equal("{ kaputt", File.ReadAllText(restarted.UserData.FilePath));
    }

    private static int LibraryCount(ArcadiaDriver arcadia, string tag)
    {
        var keys = arcadia.Main.Session.Libraries.SelectMany(l => l.Games).Select(g => g.Key).ToHashSet();
        return arcadia.Main.Tags.Find(tag)!.Games.Count(keys.Contains);
    }

    private static DataSourcesSettingsViewModel OpenDataSources(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        return Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static TagListViewModel OpenTagList(ArcadiaDriver arcadia)
    {
        OpenDataSources(arcadia);
        arcadia.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        arcadia.Press(UiAction.Accept);
        return arcadia.Main.TagList!;
    }

    /// <summary>In the library: the focused game's menu, then the action labelled <paramref name="label"/>.</summary>
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
}
