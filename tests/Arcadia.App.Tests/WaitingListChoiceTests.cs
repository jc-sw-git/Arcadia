using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #539 through the view models: a game seeking cover and background is chosen in two steps, also from two hits; a click
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
    [Trait("TestCase", "T539-4")]
    public async Task A_on_a_cover_brings_the_backgrounds_starting_on_its_own_game_and_another_one_can_be_taken()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var hits = Hits(list);
        Assert.Equal((true, AppTexts.WaitingIntroCoverStep(2)), (hits.SeeksBoth, hits.Intro));

        // A into the hits, ▶ to the second cover (502), A keeps it: the backgrounds follow, on 502's own one.
        arcadia.Press(UiAction.Accept).Press(UiAction.Right);
        Assert.Equal(AppTexts.WaitingTakeCoverHint, list.AcceptHint);
        arcadia.Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((true, ArtworkKind.Hero, WaitingHitsViewModel.HeroColumns), (hits.IsHeroStep, hits.TileKind, hits.Columns));
        Assert.Equal((502, "Echo Drift"), (hits.ChosenCover!.Hit.Id, hits.ChosenCover.Name));
        Assert.Equal((AppTexts.WaitingIntroHeroStep(2), AppTexts.WaitingOtherCover), (hits.Intro, list.BackHint));
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
    public async Task B_on_the_backgrounds_goes_back_to_the_covers_on_the_one_chosen_and_B_again_to_the_games()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        var hits = Hits(list);
        arcadia.Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Accept);
        await WaitingListTests.ShownAsync(list);

        arcadia.Press(UiAction.Back);
        await WaitingListTests.ShownAsync(list);

        Assert.Equal((false, null, true), (hits.IsHeroStep, hits.ChosenCover, list.InHits));
        Assert.Equal((ArtworkKind.Cover, 502), (hits.TileKind, HitId(list.Hits!.Hits[FocusedHit(list)])));
        Assert.Equal(AppTexts.Back, list.BackHint);
        Assert.Single(arcadia.Artwork.Waiting, w => w.Game.Key == WaitingListTests.Games[0].Key);

        arcadia.Press(UiAction.Back);
        Assert.False(list.InHits);
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
        Assert.False(Hits(list).IsHeroStep);
        Assert.Single(arcadia.Artwork.Waiting, w => w.Game.Key == WaitingListTests.Games[0].Key);

        // The double click: the cover is kept, the backgrounds follow; there a double click takes the background.
        second.TakeCommand.Execute(null);
        await WaitingListTests.ShownAsync(list);
        Assert.True(Hits(list).IsHeroStep);
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
