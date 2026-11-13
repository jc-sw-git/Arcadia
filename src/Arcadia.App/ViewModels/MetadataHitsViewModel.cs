using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The hits of a metadata entry (#494, D6): "Gesucht: Metadaten" and IGDB's main games for the game's name, in IGDB's order,
/// as a list of names and years beside an info box with the marked hit's facts (#539): games of the same name tell apart
/// by release, developer and description, not by a cover. The hits are known from the search that put the game on the
/// list, so they show without a request. An own term searches IGDB's main games and shows the first
/// <see cref="MetadataService.MaxOwnTermHits"/>; nothing of it is kept.
/// </summary>
/// <remarks>UI thread only.</remarks>
public sealed class MetadataHitsViewModel : WaitingHitsViewModel
{
    private readonly MetadataService _metadata;

    /// <param name="title">The title the game shows, its own one included (#496); the search term starts from the launcher's.</param>
    public MetadataHitsViewModel(
        MetadataService metadata, WaitingMetadataGame waiting, string title, WaitingKeyboard keyboard, Action<WaitingHitViewModel> take, Action<WaitingHitViewModel> mark)
        : base(title, SteamGridDbNames.SearchTermOf(waiting.Game.Title), keyboard, take, mark)
    {
        _metadata = metadata;
        Waiting = waiting;
        ShowHits(waiting.Hits);
    }

    public WaitingMetadataGame Waiting { get; }

    public override string SoughtText => AppTexts.WaitingSoughtMetadata;

    public override string Intro => IsOwnTerm ? AppTexts.WaitingIntroMetadataOwnTerm(Hits.Count) : AppTexts.WaitingIntroMetadata(Hits.Count);

    public override string Note => AppTexts.WaitingNoteMetadata;

    public override bool ShowsFacts => true;

    protected override int MaxTermLength => IgdbQuery.MaxTerm;

    /// <summary>The game's own hits as they are now.</summary>
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
            InsertHit(Hits.Count, new MetadataHitViewModel(hit, MarkFromClick, Take));
        }

        Done(Hits.Count > 0 ? null : AppTexts.WaitingNoHits);
    }

    /// <summary>The list shows no images: a cover tells games of the same name apart least (#539).</summary>
    protected override Task<ArtworkFile?> TileImageAsync(WaitingHitViewModel tile, CancellationToken cancel) => Task.FromResult<ArtworkFile?>(null);

    private void ShowHits(IReadOnlyList<IgdbGame> hits)
    {
        Hits.Clear();
        foreach (var hit in hits)
        {
            Hits.Add(new MetadataHitViewModel(hit, MarkFromClick, Take));
        }

        IntroChanged();
    }
}
