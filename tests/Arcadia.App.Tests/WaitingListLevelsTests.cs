using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #506 C through the view models: the waiting list's two levels, the hit grid and the field "Eigener Suchbegriff", hits
/// checked when shown, an own search with "Zurücksetzen", and nothing asked or loaded for games the focus passes.
/// </summary>
public sealed class WaitingListLevelsTests
{
    private static async Task<(ArcadiaDriver Arcadia, WaitingListViewModel List)> OpenAsync(ArcadiaDriver? started = null)
    {
        var arcadia = started ?? await ArcadiaDriver.StartAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        if (started is null)
        {
            await WaitingListTests.FindNamesakesAsync(arcadia);
        }

        var list = WaitingListTests.OpenList(arcadia);
        await WaitingListTests.ShownAsync(list);
        return (arcadia, list);
    }

    private static int FocusedHit(WaitingListViewModel list) => list.Hits!.Hits.Select((hit, i) => (hit, i)).Single(e => e.hit.IsFocused).i;

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>One game, "Echo Drift", with eight namesakes that each have a cover: five a row, three in the second.</summary>
    private static async Task<(ArcadiaDriver Arcadia, WaitingListViewModel List)> EightHitsAsync()
    {
        var game = WaitingListTests.GameOf("echo", "Echo Drift");
        var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(WaitingListTests.Other, [game])], WaitingListTests.Settings);
        arcadia.SteamGridDb.Finds("Echo Drift", [.. Enumerable.Range(801, 8).Select(id => (id, "Echo Drift", (int?)2000))]);
        foreach (var id in Enumerable.Range(801, 8))
        {
            arcadia.SteamGridDb.Cover("game", Id(id), $"https://cdn.example/grid/{id}.png", 600, 900);
        }

        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        arcadia.Artwork.SearchAgain(byPlayer: false);
        await WaitingListTests.SettledAsync(arcadia);
        return await OpenAsync(arcadia);
    }

    /// <summary>The waiting list as stored before #506: "Echo Drift" with hits that were never checked.</summary>
    private static async Task<(ArcadiaDriver Arcadia, WaitingListViewModel List)> OldListAsync(params int[] hits)
    {
        var game = WaitingListTests.GameOf("echo", "Echo Drift");
        using var first = await ArcadiaDriver.StartAsync([LauncherSpec.With(WaitingListTests.Other, [game])], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        foreach (var id in new[] { 501, 502 })
        {
            first.SteamGridDb.Cover("game", Id(id), $"https://cdn.example/grid/{id}.png", 600, 900);
        }

        var list = string.Join(", ", hits.Select(id => $$"""{ "id": {{id}}, "name": "Echo Drift" }"""));
        Directory.CreateDirectory(Path.Combine(first.Folder, "artwork"));
        await File.WriteAllTextAsync(
            Path.Combine(first.Folder, "artwork", NameMatchStore.FileName),
            $$"""{ "other:echo": { "state": "Waiting", "position": 1, "hits": [ {{list}} ] } }""");
        var arcadia = await first.RestartAsync();
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        await WaitingListTests.SettledAsync(arcadia);
        arcadia.SteamGridDb.Requests.Clear();
        return (arcadia, WaitingListTests.OpenList(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-80")]
    [Trait("TestCase", "T506-81")]
    public async Task The_focus_starts_in_the_games_list_and_up_down_go_round_held_they_stop_at_the_end()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;

        Assert.Equal((false, "Echo Drift"), (list.InHits, list.Hits!.Title));
        Assert.DoesNotContain(list.Hits.Hits, hit => hit.IsFocused);
        Assert.Equal((AppTexts.WaitingChooseHint, AppTexts.WaitingOpenHitsHint, AppTexts.Close), (list.ChooseHint, list.AcceptHint, list.BackHint));

        arcadia.Press(UiAction.Right).Press(UiAction.Left);
        Assert.Equal("Echo Drift", list.Hits!.Title);
        arcadia.Press(UiAction.Up);
        Assert.Equal("Iron Ember", list.Hits!.Title);
        arcadia.Press(UiAction.Down);
        Assert.Equal("Echo Drift", list.Hits!.Title);

        // Held from the first: past the second to the last, and no further.
        for (var i = 0; i < 4; i++)
        {
            HeldPress.Run(repeat: i > 0, () => list.Handle(UiAction.Down));
        }

        Assert.Equal("Iron Ember", list.Hits!.Title);
        arcadia.Press(UiAction.Down);
        Assert.Equal("Echo Drift", list.Hits!.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-82")]
    [Trait("TestCase", "T506-85")]
    public async Task A_goes_into_the_hits_B_comes_back_to_the_same_game_and_A_again_starts_at_the_first_hit()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;

        arcadia.Press(UiAction.Accept);
        Assert.Equal((true, 0), (list.InHits, FocusedHit(list)));
        Assert.Equal((AppTexts.WaitingChooseHitHint, AppTexts.WaitingTakeCoverHint, AppTexts.Back), (list.ChooseHint, list.AcceptHint, list.BackHint));
        Assert.True(list.Games[0].IsCurrent);
        arcadia.Press(UiAction.Right);
        Assert.Equal(1, FocusedHit(list));

        arcadia.Press(UiAction.Back);
        Assert.Equal((false, "Echo Drift"), (list.InHits, list.Hits!.Title));
        Assert.DoesNotContain(list.Hits.Hits, hit => hit.IsFocused);
        Assert.Equal(3, arcadia.Artwork.Waiting.Count);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(0, FocusedHit(list));

        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-83")]
    public async Task The_grid_goes_round_in_rows_and_columns_and_up_from_the_top_row_reaches_the_field()
    {
        var (arcadia, list) = await EightHitsAsync();
        using var _ = arcadia;
        var hits = list.Hits!;
        Assert.Equal((8, WaitingHitsViewModel.CoverColumns), (hits.Hits.Count, hits.Columns));
        arcadia.Press(UiAction.Accept);

        // Five covers a row: rows 0–4 and 5–7.
        arcadia.Press(UiAction.Left);
        Assert.Equal(4, FocusedHit(list));
        arcadia.Press(UiAction.Right);
        Assert.Equal(0, FocusedHit(list));
        arcadia.Press(UiAction.Down);
        Assert.Equal(5, FocusedHit(list));
        arcadia.Press(UiAction.Right).Press(UiAction.Right).Press(UiAction.Right);
        Assert.Equal(5, FocusedHit(list));

        // ▼ from a hit with none below goes round to the column's top, the field; ▲ there to the column's last hit.
        arcadia.Press(UiAction.Down);
        Assert.Equal((WaitingHitsFocus.Term, true), (hits.Zone, hits.IsTermFocused));
        Assert.DoesNotContain(hits.Hits, hit => hit.IsFocused);
        Assert.Equal(AppTexts.WaitingSearchHint, list.AcceptHint);
        arcadia.Press(UiAction.Up);
        Assert.Equal(5, FocusedHit(list));
        arcadia.Press(UiAction.Up).Press(UiAction.Right).Press(UiAction.Right).Press(UiAction.Right);
        Assert.Equal(3, FocusedHit(list));
        arcadia.Press(UiAction.Down);
        Assert.Equal(WaitingHitsFocus.Term, hits.Zone);
        arcadia.Press(UiAction.Down);
        Assert.Equal(3, FocusedHit(list));

        // Held ▼ stops at the column's last hit; "Zurücksetzen" is passed over while greyed out.
        arcadia.Press(UiAction.Up);
        HeldPress.Run(repeat: true, () => list.Handle(UiAction.Up));
        Assert.Equal(WaitingHitsFocus.Term, hits.Zone);
        arcadia.Press(UiAction.Right);
        Assert.Equal(WaitingHitsFocus.Term, hits.Zone);
        arcadia.Press(UiAction.Down).Press(UiAction.Left).Press(UiAction.Left).Press(UiAction.Left).Press(UiAction.Down);
        Assert.Equal(5, FocusedHit(list));
        HeldPress.Run(repeat: true, () => list.Handle(UiAction.Down));
        Assert.Equal(5, FocusedHit(list));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-96")]
    [Trait("TestCase", "T506-95")]
    public async Task Hits_from_before_are_checked_when_the_game_is_shown_and_those_without_an_image_drop_out()
    {
        var (arcadia, list) = await OldListAsync(501, 504, 502);
        using var _ = arcadia;
        Assert.Empty(list.Hits!.Hits);
        Assert.Empty(arcadia.SteamGridDb.Asked());

        await WaitingListTests.ShownAsync(list);

        Assert.Equal([501, 502], list.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        Assert.Equal((false, false), (list.Hits.IsChecking, list.Hits.HasMessage));
        Assert.Equal(["grids/game/501", "grids/game/502", "grids/game/504"], arcadia.SteamGridDb.Asked().Where(a => a.StartsWith("grids/", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal([501, 502], list.Games[0].Artwork!.Hits.Select(h => h.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-95")]
    public async Task While_hits_are_checked_the_grid_says_so_and_with_none_left_it_says_keine_treffer()
    {
        var (arcadia, list) = await OldListAsync(504, 505);
        using var _ = arcadia;
        arcadia.SteamGridDb.Delay = TimeSpan.FromMilliseconds(300);

        await arcadia.WaitUntilAsync(() => list.Hits!.IsChecking, "Treffer werden geprüft");
        Assert.False(list.Hits!.HasMessage);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((false, AppTexts.WaitingNoHits, true), (list.Hits!.IsChecking, list.Hits.Message, list.Hits.HasMessage));
        Assert.Single(list.Games);

        // A goes to the field then, Y still skips.
        arcadia.Press(UiAction.Accept);
        Assert.True(list.Hits.IsTermFocused);
        arcadia.Press(UiAction.Status);
        Assert.True(list.IsDone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-97")]
    public async Task Without_network_the_check_stops_after_one_request_and_tries_again_next_time()
    {
        var (arcadia, list) = await OldListAsync(501, 502, 503, 504);
        using var _ = arcadia;
        arcadia.SteamGridDb.Offline = true;
        await WaitingListTests.ShownAsync(list);

        Assert.Equal(AppTexts.WaitingUnreachable, list.Hits!.Message);
        Assert.True(arcadia.SteamGridDb.Count <= 4, $"{arcadia.SteamGridDb.Count} Anfragen");
        arcadia.SteamGridDb.Offline = false;
        arcadia.Press(UiAction.Back).Press(UiAction.Accept);
        var again = arcadia.Main.WaitingList!;
        await WaitingListTests.ShownAsync(again);
        Assert.Equal([501, 502], again.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T532-8")]
    public async Task After_the_key_is_deleted_a_waiting_entry_says_there_is_no_key_not_that_it_is_invalid()
    {
        var (arcadia, list) = await OldListAsync(501, 502, 503, 504);
        using var _ = arcadia;
        arcadia.Artwork.SteamGridDb!.Clear();
        await WaitingListTests.ShownAsync(list);

        Assert.Equal(AppTexts.DataSourcesKeyMissing, list.Hits!.Message);
        Assert.Empty(arcadia.SteamGridDb.Requests);
    }

    private static TextEditorEditing EditTerm(ArcadiaDriver arcadia, WaitingListViewModel list)
    {
        arcadia.Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Accept);
        return new TextEditorEditing(list.Hits!.Editor ?? throw new InvalidOperationException("Die Tastatur ist nicht offen."));
    }

    private sealed record TextEditorEditing(Settings.TextEditorViewModel Editor)
    {
        public void Write(string text)
        {
            while (Editor.Text.Length > 0)
            {
                Editor.HandleKey(Avalonia.Input.Key.Back);
            }

            Editor.Type(text);
        }
    }

    /// <summary>"Echo Saga" finds 40 games; every fourth has no cover, so 30 have one.</summary>
    private static List<int> FortySagas(ArcadiaDriver arcadia)
    {
        var ids = Enumerable.Range(1001, 40).ToList();
        arcadia.SteamGridDb.Finds("Echo Saga", [.. ids.Select(id => (id, $"Echo Saga {id}", (int?)null))]);
        var withCover = ids.Where((_, i) => i % 4 != 3).ToList();
        foreach (var id in withCover)
        {
            arcadia.SteamGridDb.Cover("game", Id(id), $"https://cdn.example/grid/{id}.png", 600, 900);
        }

        // One background, for the second step of the choice (#539).
        arcadia.SteamGridDb.Hero("game", "1002", "https://cdn.example/hero/1002.png", 1920, 620);
        return withCover;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-99")]
    [Trait("TestCase", "T506-100")]
    [Trait("TestCase", "T506-102")]
    public async Task An_own_term_shows_the_first_25_results_with_images_and_zuruecksetzen_brings_back_the_hits()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var withCover = FortySagas(arcadia);
        var editing = EditTerm(arcadia, list);
        Assert.Equal("Echo Drift", editing.Editor.Text);
        Assert.True(list.Hits!.IsEditing);

        editing.Write("Echo Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);

        var hits = list.Hits!;
        Assert.Equal((false, "Echo Saga", true), (hits.IsEditing, hits.Term, hits.CanReset));
        Assert.Equal(withCover.Take(SteamGridDbClient.MaxOwnTermHits), hits.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        var lastNeeded = Enumerable.Range(1001, 40).ToList().IndexOf(withCover[SteamGridDbClient.MaxOwnTermHits - 1]);
        Assert.DoesNotContain(arcadia.SteamGridDb.Asked(), a => Enumerable.Range(1001 + lastNeeded + 1, 40 - lastNeeded - 1).Any(id => a == $"grids/game/{id}"));
        Assert.Equal(AppTexts.WaitingIntroCoverStepOwnTerm(25), hits.Intro);

        // ◀ ▶ reach "Zurücksetzen" now; A on it: the search name and the hits again, without a search.
        var searches = arcadia.SteamGridDb.Asked().Count(a => a.StartsWith("search/", StringComparison.Ordinal));
        arcadia.Press(UiAction.Up);
        Assert.Equal(WaitingHitsFocus.Term, hits.Zone);
        arcadia.Press(UiAction.Right);
        Assert.Equal((WaitingHitsFocus.Reset, AppTexts.WaitingReset), (hits.Zone, list.AcceptHint));
        arcadia.Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal(("Echo Drift", false), (hits.Term, hits.CanReset));
        Assert.Equal([501, 502], hits.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        Assert.Equal((WaitingHitsFocus.Grid, 0), (hits.Zone, FocusedHit(list)));
        Assert.Equal(searches, arcadia.SteamGridDb.Asked().Count(a => a.StartsWith("search/", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-99")]
    public async Task B_on_the_keyboard_keeps_the_term_and_searches_nothing()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var editing = EditTerm(arcadia, list);
        editing.Write("Etwas anderes");
        var asked = arcadia.SteamGridDb.Count;

        arcadia.Press(UiAction.Back);

        Assert.Equal((false, "Echo Drift", true), (list.Hits!.IsEditing, list.Hits.Term, list.InHits));
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-101")]
    public async Task A_result_of_the_own_term_is_taken_and_the_term_isnt_kept()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        FortySagas(arcadia);
        var editing = EditTerm(arcadia, list);
        editing.Write("Echo Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);

        // The first result took the focus; the term searches the backgrounds on the second step too (#539).
        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);
        Assert.Equal("Echo Saga", list.Hits!.Term);
        Assert.Equal([1002], list.Hits.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        arcadia.Press(UiAction.Accept);
        await list.Loading;
        await WaitingListTests.SettledAsync(arcadia);

        Assert.Equal(("Golden Crown", "Golden Crown", false), (list.Hits!.Title, list.Hits.Term, list.InHits));
        Assert.DoesNotContain(arcadia.Artwork.Waiting, w => w.Game.Key == "other:echo");
        var stored = await File.ReadAllTextAsync(Path.Combine(arcadia.Folder, "artwork", NameMatchStore.FileName));
        Assert.Contains("1002", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Echo Saga", stored, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-104")]
    public async Task An_own_term_without_network_or_without_results_says_so_and_zuruecksetzen_still_works()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;

        var editing = EditTerm(arcadia, list);
        editing.Write("Nichts dergleichen");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);
        Assert.Equal((AppTexts.WaitingNoHits, 0), (list.Hits!.Message, list.Hits.Hits.Count));

        arcadia.SteamGridDb.Offline = true;
        editing = new TextEditorEditing(OpenEditor(arcadia, list));
        editing.Write("Echo Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);
        Assert.Equal(AppTexts.WaitingUnreachable, list.Hits!.Message);

        // Nothing came back: no "findet 0 Spiele" above the reason.
        Assert.False(list.Hits.HasIntro);

        arcadia.SteamGridDb.Offline = false;
        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);
        Assert.Equal([501, 502], list.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        Assert.True(list.Hits.HasIntro);
        Assert.False(list.Hits.HasMessage);
    }

    private static Settings.TextEditorViewModel OpenEditor(ArcadiaDriver arcadia, WaitingListViewModel list)
    {
        Assert.True(list.Hits!.IsTermFocused);
        arcadia.Press(UiAction.Accept);
        return list.Hits.Editor!;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-100")]
    public async Task The_first_result_keeps_the_focus_when_a_later_one_is_checked_first_until_the_player_moves()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        arcadia.SteamGridDb.Finds("Golden Saga", (1601, "Golden Saga One", 2001), (1602, "Golden Saga Two", 2002));
        arcadia.SteamGridDb.Cover("game", "1601", "https://cdn.example/grid/1601.png", 600, 900).Cover("game", "1602", "https://cdn.example/grid/1602.png", 600, 900);
        arcadia.SteamGridDb.SlowPaths["grids/game/1601"] = TimeSpan.FromMilliseconds(300);

        var editing = EditTerm(arcadia, list);
        editing.Write("Golden Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);

        Assert.Equal([1601, 1602], list.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        Assert.Equal(0, FocusedHit(list));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-111")]
    public async Task The_whole_way_through_both_levels_and_an_own_search_works_with_the_controller_alone_and_b_goes_back()
    {
        var images = WaitingListTests.Images();
        images.Serve("https://cdn.example/grid/1602.png", TestImages.Png(600, 900));
        images.Serve("https://cdn.example/hero/1602.png", TestImages.Png(1920, 620));
        var started = await ArcadiaDriver.StartAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: images);
        await WaitingListTests.FindNamesakesAsync(started);
        var (arcadia, list) = await OpenAsync(started);
        using var _ = arcadia;
        arcadia.SteamGridDb.Finds("Golden Saga", (1601, "Golden Saga One", 2001), (1602, "Golden Saga Two", 2002));
        arcadia.SteamGridDb.Cover("game", "1601", "https://cdn.example/grid/1601.png", 600, 900).Cover("game", "1602", "https://cdn.example/grid/1602.png", 600, 900)
            .Hero("game", "1602", "https://cdn.example/hero/1602.png", 1920, 620);

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal(("Golden Crown", true, 0), (list.Hits!.Title, list.InHits, FocusedHit(list)));
        arcadia.Press(UiAction.Right).Press(UiAction.Down);
        Assert.True(list.Hits.IsTermFocused);
        arcadia.Press(UiAction.Up);
        Assert.Equal(1, FocusedHit(list));
        arcadia.Press(UiAction.Up).Press(UiAction.Accept);
        var editing = new TextEditorEditing(list.Hits.Editor!);
        editing.Write("Golden Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);
        Assert.Equal([1601, 1602], list.Hits.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        arcadia.Press(UiAction.Right);
        await WaitingListTests.TakeCoverAndItsBackgroundAsync(arcadia, list);

        // Taken: the game left, the focus is back in the games list on the next one.
        Assert.False(list.InHits);
        Assert.Equal(["Echo Drift", "Iron Ember"], list.Games.Select(g => g.Title));
        Assert.Equal("Iron Ember", list.CurrentGame!.Title);
        Assert.Equal("https://cdn.example/grid/1602.png", new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read("other:crown")?.Cover?.Stamp);

        arcadia.Press(UiAction.Accept).Press(UiAction.Status);
        Assert.False(list.InHits);
        arcadia.Press(UiAction.Accept).Press(UiAction.Back);
        Assert.False(list.InHits);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-110")]
    public async Task Closing_arcadia_while_an_own_search_runs_cancels_it_and_the_next_start_shows_the_list_as_before()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var before = arcadia.Artwork.Waiting.Select(w => (w.Game.Key, string.Join(",", w.Hits.Select(h => h.Id)), w.Match.Checked.Count)).ToList();
        arcadia.SteamGridDb.Hangs = true;
        var editing = EditTerm(arcadia, list);
        editing.Write("Echo Saga");
        editing.Editor.Take();
        await WaitingListTests.WaitUntilAsync(() => arcadia.SteamGridDb.Hanging > 0);

        arcadia.Main.Dispose();

        await WaitingListTests.WaitUntilAsync(() => arcadia.SteamGridDb.Hanging == 0);
        arcadia.SteamGridDb.Hangs = false;
        arcadia.SteamGridDb.Requests.Clear();
        using var restarted = await arcadia.RestartAsync();
        var again = WaitingListTests.OpenList(restarted);
        await WaitingListTests.ShownAsync(again);
        Assert.Equal(before, restarted.Artwork.Waiting.Select(w => (w.Game.Key, string.Join(",", w.Hits.Select(h => h.Id)), w.Match.Checked.Count)));
        Assert.Equal(before[0].Item2, string.Join(",", again.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id)));
        Assert.DoesNotContain(restarted.SteamGridDb.Asked(), a => a.StartsWith("search/", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-105")]
    public async Task A_new_own_search_cancels_the_one_running_and_its_late_answers_never_show()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        FortySagas(arcadia);
        arcadia.SteamGridDb.Finds("Echo Zwei", (1501, "Echo Zwei", null), (1502, "Echo Zwei", null));
        arcadia.SteamGridDb.Cover("game", "1501", "https://cdn.example/grid/1501.png", 600, 900).Cover("game", "1502", "https://cdn.example/grid/1502.png", 600, 900);
        arcadia.SteamGridDb.Delay = TimeSpan.FromMilliseconds(200);

        var editing = EditTerm(arcadia, list);
        editing.Write("Echo Saga");
        editing.Editor.Take();
        arcadia.Press(UiAction.Up);
        editing = new TextEditorEditing(OpenEditor(arcadia, list));
        editing.Write("Echo Zwei");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([1501, 1502], list.Hits!.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-107")]
    [Trait("TestCase", "T506-108")]
    public async Task Moving_through_the_games_waits_for_nothing_and_only_the_game_the_focus_rests_on_loads()
    {
        var images = WaitingListTests.Images();
        var started = await ArcadiaDriver.StartAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: images);
        await WaitingListTests.FindNamesakesAsync(started);
        var (arcadia, list) = await OpenAsync(started);
        using var _ = arcadia;
        images.Requests.Clear();
        arcadia.SteamGridDb.Delay = TimeSpan.FromSeconds(2);

        var moving = System.Diagnostics.Stopwatch.StartNew();
        arcadia.Press(UiAction.Down).Press(UiAction.Down);
        moving.Stop();

        Assert.Equal("Iron Ember", list.Hits!.Title);
        Assert.Equal([701, 702], list.Hits.Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id));
        Assert.True(moving.ElapsedMilliseconds < 500, $"{moving.ElapsedMilliseconds} ms für zwei Schritte");
        arcadia.SteamGridDb.Delay = TimeSpan.Zero;
        await WaitingListTests.ShownAsync(list);

        Assert.All(images.Requests, r => Assert.Matches("/grid/70[12]\\.png$", r.RequestUri!.AbsolutePath));
        Assert.NotEmpty(images.Requests);
    }
}
