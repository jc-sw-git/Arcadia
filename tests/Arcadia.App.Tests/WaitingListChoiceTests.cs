using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #539 through the view models: a game seeking cover and background has a tab for each, also picked from two hits; a click
/// marks a hit and a double click takes it; the note names the skip button by its glyph. The made-up launcher of
/// <see cref="WaitingListTests"/> without images, so every game seeks both; no window.
/// </summary>
public sealed class WaitingListChoiceTests
{
    private static async Task<(ArcadiaDriver Arcadia, WaitingListViewModel List)> OpenAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        await WaitingListTests.FindNamesakesAsync(arcadia);
        var list = WaitingListTests.OpenList(arcadia);
        await WaitingListTests.ShownAsync(list);
        return (arcadia, list);
    }

    private static ArtworkHitsViewModel Hits(WaitingListViewModel list) => Assert.IsType<ArtworkHitsViewModel>(list.Hits);

    private static int FocusedHit(WaitingListViewModel list) => list.Hits!.Hits.Select((hit, i) => (hit, i)).Single(e => e.hit.IsFocused).i;

    private static int HitId(WaitingHitViewModel hit) => ((ArtworkHitViewModel)hit).Hit.Id;

    private static CachedArtwork? Cached(ArcadiaDriver arcadia, Game game) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(game.Key);

    [AvaloniaFact]
    [Trait("TestCase", "T539-3")]
    public async Task A_on_a_cover_picks_it_and_shows_the_backgrounds_on_its_own_game_where_another_one_can_be_taken()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var hits = Hits(list);
        Assert.Equal((true, true, false, AppTexts.WaitingIntroCoverTab(2)), (hits.SeeksBoth, list.HasArtworkTabs, hits.IsHeroTab, hits.Intro));

        // A into the hits, ▶ to the second cover (502), A picks it: the tab "Hintergrund" follows, on 502's own one.
        arcadia.Press(UiAction.Accept).Press(UiAction.Right);
        Assert.Equal(AppTexts.WaitingTakeCoverHint, list.AcceptHint);
        arcadia.Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((true, ArtworkKind.Hero, WaitingHitsViewModel.HeroColumns), (hits.IsHeroTab, hits.TileKind, hits.Columns));
        Assert.Equal((502, "Echo Drift", false), (hits.ChosenCover!.Hit.Id, hits.ChosenCover.Name, hits.HasChosenHero));
        Assert.Equal((AppTexts.WaitingIntroHeroTab(2), AppTexts.WaitingTakeHint), (hits.Intro, list.AcceptHint));
        Assert.Equal(502, HitId(list.Hits!.Hits[FocusedHit(list)]));
        Assert.All(list.Hits.Hits, hit => Assert.NotNull(hit.Image));

        // ◀ to 501's background, A takes cover 502 and background 501 and goes on to the next game.
        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(("Golden Crown", false), (list.Hits!.Title, list.InHits));
        var cached = Cached(arcadia, WaitingListTests.Games[0])!;
        Assert.Equal(("https://cdn.example/grid/502.png", "https://cdn.example/hero/501.png"), (cached.Cover!.Stamp, cached.Hero!.Stamp));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-4")]
    public async Task LB_RB_turn_the_tabs_each_keeps_its_pick_and_the_background_can_be_picked_first()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var hits = Hits(list);

        // RB to "Hintergrund" without a pick, A on the second background picks it and turns to "Cover".
        arcadia.Press(UiAction.NextTab);
        await WaitingListTests.ShownAsync(list);
        Assert.Equal((true, ArtworkKind.Hero), (hits.IsHeroTab, hits.TileKind));
        arcadia.Press(UiAction.Accept).Press(UiAction.Right);
        Assert.Equal(AppTexts.WaitingTakeHeroHint, list.AcceptHint);
        arcadia.Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((false, 502), (hits.IsHeroTab, hits.ChosenHero!.Hit.Id));
        Assert.Equal(502, HitId(list.Hits!.Hits[FocusedHit(list)]));

        // LB back to "Hintergrund": the pick is still marked and focused; nothing was taken.
        arcadia.Press(UiAction.PreviousTab);
        await WaitingListTests.ShownAsync(list);
        Assert.True(hits.IsHeroTab);
        Assert.Equal([false, true], list.Hits.Hits.Select(h => ((ArtworkHitViewModel)h).IsChosen));
        Assert.Equal(502, HitId(list.Hits.Hits[FocusedHit(list)]));
        Assert.Single(arcadia.Artwork.Waiting, w => w.Game.Key == WaitingListTests.Games[0].Key);

        // To "Cover", ◀ to 501, A takes cover 501 with background 502.
        arcadia.Press(UiAction.NextTab);
        await WaitingListTests.ShownAsync(list);
        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        var cached = Cached(arcadia, WaitingListTests.Games[0])!;
        Assert.Equal(("https://cdn.example/grid/501.png", "https://cdn.example/hero/502.png"), (cached.Cover!.Stamp, cached.Hero!.Stamp));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-4")]
    public async Task B_goes_back_to_the_games_and_skips_nothing()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        arcadia.Press(UiAction.Back);

        Assert.False(list.InHits);
        Assert.Equal(AppTexts.Close, list.BackHint);
        Assert.Equal(3, arcadia.Artwork.Waiting.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-4")]
    public async Task B_drops_the_pick_and_the_game_starts_again_on_the_covers()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var hits = Hits(list);
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);
        Assert.True(hits.HasChosenCover);

        arcadia.Press(UiAction.Back).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((false, false, false, AppTexts.WaitingTakeCoverHint), (hits.HasChosenCover, hits.HasChosenHero, hits.IsHeroTab, list.AcceptHint));
        Assert.All(list.Hits!.Hits, hit => Assert.False(((ArtworkHitViewModel)hit).IsChosen));
        Assert.Equal(3, arcadia.Artwork.Waiting.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-7")]
    public async Task A_click_only_marks_a_hit_and_a_double_click_takes_it()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var second = list.Hits!.Hits[1];

        second.MarkCommand.Execute(null);

        Assert.True(list.InHits);
        Assert.True(second.IsFocused);
        Assert.Same(second, list.Hits.MarkedHit);
        Assert.False(Hits(list).IsHeroTab);
        Assert.Single(arcadia.Artwork.Waiting, w => w.Game.Key == WaitingListTests.Games[0].Key);

        // The double click: the cover is picked, the backgrounds follow; there a double click takes both.
        second.TakeCommand.Execute(null);
        await WaitingListTests.ShownAsync(list);
        Assert.True(Hits(list).IsHeroTab);
        list.Hits.Hits[0].TakeCommand.Execute(null);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Golden Crown", list.Hits!.Title);
        Assert.Equal("https://cdn.example/grid/502.png", Cached(arcadia, WaitingListTests.Games[0])!.Cover!.Stamp);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-7")]
    public async Task A_click_on_a_hit_of_a_game_shown_before_takes_nothing()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var old = list.Hits!.Hits[0];
        arcadia.Press(UiAction.Down);

        old.TakeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, arcadia.Artwork.Waiting.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T539-10")]
    public async Task The_notes_name_the_skip_button_as_a_glyph()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;

        Assert.Contains("[Y]", list.Hits!.Note, StringComparison.Ordinal);
        Assert.Contains("[Y]", AppTexts.WaitingNoteMetadata, StringComparison.Ordinal);
    }
}
