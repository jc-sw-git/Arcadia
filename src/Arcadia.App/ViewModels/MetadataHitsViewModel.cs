using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The hits of a metadata entry (#494, D6): "Gesucht: Metadaten" and IGDB's main games for the game's name, in IGDB's order,
/// each with IGDB's cover (or the placeholder), name and year. The hits are known from the search that put the game on the
/// list, so they show without a request; once the focus rests on the entry, the covers load, only while IGDB's access is
/// usable. An own term searches IGDB's main games and shows the first <see cref="MetadataService.MaxOwnTermHits"/>;
/// nothing of it is kept.
/// </summary>
/// <remarks>UI thread only.</remarks>
public sealed class MetadataHitsViewModel : WaitingHitsViewModel
{
    private readonly MetadataService _metadata;
    private readonly ArtworkService? _artwork;

    /// <param name="artwork">Downloads the covers like the artwork entries' tiles; null: placeholders only.</param>
    /// <param name="title">The title the game shows, its own one included (#496); the search term starts from the launcher's.</param>
    public MetadataHitsViewModel(
        MetadataService metadata, ArtworkService? artwork, WaitingMetadataGame waiting, string title, WaitingKeyboard keyboard, Action<WaitingHitViewModel> take)
        : base(title, SteamGridDbNames.SearchTermOf(waiting.Game.Title), keyboard, take)
    {
        _metadata = metadata;
        _artwork = artwork;
        Waiting = waiting;
        ShowHits(waiting.Hits);
    }

    public WaitingMetadataGame Waiting { get; }

    public override string SoughtText => AppTexts.WaitingSoughtMetadata;

    public override string Intro => IsOwnTerm ? AppTexts.WaitingIntroMetadataOwnTerm(Hits.Count) : AppTexts.WaitingIntroMetadata(Hits.Count);

    public override string Note => AppTexts.WaitingNoteMetadata;

    protected override int MaxTermLength => IgdbQuery.MaxTerm;

    /// <summary>The game's own hits as they are now; the covers load once the focus rests on it.</summary>
    protected override async Task ShowOriginalAsync(TimeSpan rest, CancellationToken cancel)
    {
        await Task.Delay(rest, cancel);
        if (IsDisposed)
        {
            return;
        }

        if (Hits.Count == 0)
        {
            ShowHits(Waiting.Hits);
        }

        LoadShownTiles(cancel);
        Done(Hits.Count > 0 ? null : AppTexts.WaitingNoHits);
    }

    /// <summary>An own term: IGDB's main games for it; without a usable access or network the reason instead.</summary>
    protected override async Task SearchAsync(string term, CancellationToken cancel)
    {
        IsChecking = true;
        Message = null;
        TilesLoad = true;
        var search = await Task.Run(() => _metadata.SearchByTermAsync(term, cancel), cancel);
        cancel.ThrowIfCancellationRequested();
        switch (search.Outcome)
        {
            case IgdbOutcome.Rejected:
                Done(_metadata.Igdb?.State == IgdbAccessState.Rejected ? AppTexts.DataSourcesIgdbRejected : AppTexts.DataSourcesIgdbNeedsAccess);
                return;
            case IgdbOutcome.NoConnection:
                Done(AppTexts.WaitingIgdbUnreachable);
                return;
        }

        foreach (var hit in search.Games)
        {
            InsertHit(Hits.Count, Track(new MetadataHitViewModel(hit, Take)));
        }

        Done(Hits.Count > 0 ? null : AppTexts.WaitingNoHits);
    }

    /// <summary>IGDB's cover, only from IGDB's image host and only while its access is usable: no request to IGDB without one.</summary>
    protected override Task<ArtworkFile?> TileImageAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        var hit = ((MetadataHitViewModel)tile).Hit;
        var game = Waiting.Game;
        return _artwork is not null && _metadata.Igdb is { IsUsable: true } && IgdbImages.CoverOf(hit.CoverId) is { } address
            ? Task.Run(() => _artwork.OfferedCoverAsync(game, address, cancel), cancel)
            : Task.FromResult<ArtworkFile?>(null);
    }

    private void ShowHits(IReadOnlyList<IgdbGame> hits)
    {
        Hits.Clear();
        foreach (var hit in hits)
        {
            Hits.Add(Track(new MetadataHitViewModel(hit, Take)));
        }

        IntroChanged();
    }
}
