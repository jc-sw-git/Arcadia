using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The hits of an artwork entry (#506): the SteamGridDB games that have an image of the sought kind. Hits known to have
/// one show at once; once the focus rests on the entry, the others are checked (a few at a time, filling the grid in
/// SteamGridDB's order) and the tiles' images load. An own term searches SteamGridDB and shows the first
/// <see cref="SteamGridDbClient.MaxOwnTermHits"/> results with images; nothing of it is kept.
/// A game seeking cover and background has a tab for each (#539, LB/RB or a click): A in a tab picks that kind, also from
/// another hit than the other kind, and goes to the other tab while it has no pick, starting on the picked game; once
/// both are picked they are taken together.
/// </summary>
/// <remarks>UI thread only.</remarks>
public sealed partial class ArtworkHitsViewModel : WaitingHitsViewModel
{
    /// <summary>How many hits are checked at once: enough to fill the grid quickly, few enough not to be slowed down.</summary>
    private const int ParallelChecks = 4;

    private readonly ArtworkService _artwork;

    /// <summary>The hit the focus goes to once it shows, until the player moves: the tab's pick, else the other tab's game.</summary>
    private int? _focusOn;

    /// <param name="take">A or a double click on a hit: the list takes it.</param>
    /// <param name="mark">A click on a hit: the list marks it.</param>
    /// <param name="title">The title the game shows, its own one included (#496); the search term starts from the launcher's.</param>
    public ArtworkHitsViewModel(
        ArtworkService artwork, WaitingGame waiting, string title, WaitingKeyboard keyboard, Action<WaitingHitViewModel> take, Action<WaitingHitViewModel> mark)
        : base(title, SteamGridDbNames.SearchTermOf(waiting.Game.Title), keyboard, take, mark)
    {
        _artwork = artwork;
        Waiting = waiting;
        Sought = waiting.Match.Sought;

        // The tiles after the frame that shows the focus move: building them doesn't delay it (#506, scenario 20).
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () =>
            {
                if (!IsDisposed && !TilesLoad && Hits.Count == 0)
                {
                    ShowKnownHits(waiting.Match);
                }
            },
            Avalonia.Threading.DispatcherPriority.Background);
    }

    public WaitingGame Waiting { get; }

    /// <summary>The kinds SteamGridDB is to give the game after the choice; decided again once the focus rests on it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoughtText), nameof(Intro), nameof(TileKind), nameof(IsHero), nameof(Columns), nameof(SeeksBoth), nameof(TakeHint))]
    public partial IReadOnlyList<ArtworkKind> Sought { get; private set; }

    /// <summary>Cover and background are sought: they are chosen one after the other (#539).</summary>
    public bool SeeksBoth => Sought.Count > 1;

    /// <summary>The tab "Hintergrund" is shown: the tiles are backgrounds. False on "Cover" and when one kind is sought.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Intro), nameof(TileKind), nameof(IsHero), nameof(Columns), nameof(TakeHint), nameof(IsCoverTab))]
    public partial bool IsHeroTab { get; private set; }

    public bool IsCoverTab => !IsHeroTab;

    /// <summary>The cover picked in its tab; null until then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChosenCover), nameof(TakeHint))]
    public partial ArtworkHitViewModel? ChosenCover { get; private set; }

    /// <summary>The background picked in its tab; null until then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChosenHero), nameof(TakeHint))]
    public partial ArtworkHitViewModel? ChosenHero { get; private set; }

    public bool HasChosenCover => ChosenCover is not null;

    public bool HasChosenHero => ChosenHero is not null;

    /// <summary>What A does: pick this tab's kind and go to the other, or, with the other picked, take both.</summary>
    public override string TakeHint => !SeeksBoth ? AppTexts.WaitingTakeHint
        : IsHeroTab ? (HasChosenCover ? AppTexts.WaitingTakeHint : AppTexts.WaitingTakeHeroHint)
        : (HasChosenHero ? AppTexts.WaitingTakeHint : AppTexts.WaitingTakeCoverHint);

    public override string SoughtText => Sought.Count > 1 ? AppTexts.WaitingSoughtBoth
        : Sought[0] == ArtworkKind.Cover ? AppTexts.WaitingSoughtCover
        : AppTexts.WaitingSoughtHero;

    public override string Intro
    {
        get
        {
            if (SeeksBoth)
            {
                return (IsHeroTab, IsOwnTerm) switch
                {
                    (false, false) => AppTexts.WaitingIntroCoverTab(Hits.Count),
                    (false, true) => AppTexts.WaitingIntroCoverTabOwnTerm(Hits.Count),
                    (true, false) => AppTexts.WaitingIntroHeroTab(Hits.Count),
                    (true, true) => AppTexts.WaitingIntroHeroTabOwnTerm(Hits.Count),
                };
            }

            var loads = Sought.Count > 1 ? AppTexts.WaitingLoadsBoth : Sought[0] == ArtworkKind.Cover ? AppTexts.WaitingLoadsCover : AppTexts.WaitingLoadsHero;
            return IsOwnTerm ? AppTexts.WaitingIntroOwnTerm(count: Hits.Count, loads: loads) : AppTexts.WaitingIntro(count: Hits.Count, loads: loads);
        }
    }

    public override string Note => AppTexts.WaitingNote;

    /// <summary>What the tiles show and the hits are checked for: the cover when both are sought, until it is chosen.</summary>
    public ArtworkKind TileKind => IsHeroTab ? ArtworkKind.Hero : NameMatch.TileKindOf(Sought);

    public override bool IsHero => TileKind == ArtworkKind.Hero;

    protected override int MaxTermLength => SteamGridDbClient.MaxNameLength;

    /// <summary>The hits known to have an image of the tile kind, at once, without a request.</summary>
    private void ShowKnownHits(NameMatch match)
    {
        Hits.Clear();
        foreach (var hit in match.Hits.Where(h => match.Checked.Contains(h.Id)))
        {
            Hits.Add(NewHit(hit));
        }

        IntroChanged();
    }

    private WaitingHitViewModel NewHit(SteamGridDbHit hit) =>
        Track(new ArtworkHitViewModel(hit, MarkFromClick, Take) { IsChosen = hit.Id == ChosenInTab?.Hit.Id });

    /// <summary>The pick of the tab shown.</summary>
    private ArtworkHitViewModel? ChosenInTab => IsHeroTab ? ChosenHero : ChosenCover;

    /// <summary>B out of the hits: nothing picked is kept, the game starts again on "Cover" (#539).</summary>
    public void DropPicks()
    {
        if (ChosenCover is null && ChosenHero is null && !IsHeroTab)
        {
            return;
        }

        ChosenCover = null;
        ChosenHero = null;
        foreach (var tile in Hits.OfType<ArtworkHitViewModel>())
        {
            tile.IsChosen = false;
        }

        if (IsHeroTab)
        {
            // A pressed again before the covers are back still lands in the grid: with both sought every hit has a cover.
            _focusOn = Hits.OfType<ArtworkHitViewModel>().FirstOrDefault()?.Hit.Id;
            IsHeroTab = false;
            Reload();
        }
    }

    /// <summary>
    /// A on a hit while both are sought: picked for the tab's kind; with the other kind picked too, both are taken,
    /// otherwise the other tab shows. One kind sought: the list takes the hit.
    /// </summary>
    protected override void Choose(WaitingHitViewModel hit)
    {
        if (!SeeksBoth)
        {
            base.Choose(hit);
            return;
        }

        var picked = (ArtworkHitViewModel)hit;
        if (IsHeroTab)
        {
            ChosenHero = picked;
        }
        else
        {
            ChosenCover = picked;
        }

        foreach (var tile in Hits.OfType<ArtworkHitViewModel>())
        {
            tile.IsChosen = tile == picked;
        }

        if (ChosenCover is { } cover && ChosenHero is { } hero)
        {
            base.Choose(new ArtworkHitViewModel(hero.Hit, MarkFromClick, Take) { Cover = cover.Hit });
            return;
        }

        ShowTab(hero: !IsHeroTab);
    }

    /// <summary>LB/RB: the other tab, while both are sought; false otherwise.</summary>
    public bool SwitchTab()
    {
        if (!SeeksBoth || IsEditing)
        {
            return false;
        }

        ShowTab(hero: !IsHeroTab);
        return true;
    }

    [RelayCommand]
    private void ShowCovers() => ShowTab(hero: false);

    [RelayCommand]
    private void ShowHeroes() => ShowTab(hero: true);

    /// <summary>A tab's hits, the focus on its pick, else on the game picked in the other tab.</summary>
    private void ShowTab(bool hero)
    {
        if (!SeeksBoth || hero == IsHeroTab)
        {
            return;
        }

        IsHeroTab = hero;
        _focusOn = (ChosenInTab ?? (hero ? ChosenCover : ChosenHero))?.Hit.Id;
        Reload();
    }

    /// <summary>The game's own hits: what it seeks now, the known ones with their tiles, the unchecked ones checked.</summary>
    protected override async Task ShowOriginalAsync(TimeSpan rest, CancellationToken cancel)
    {
        await Task.Delay(rest, cancel);
        var game = Waiting.Game;
        if (await Task.Run(() => _artwork.WaitingMatchNow(game), cancel) is not { } match || IsDisposed)
        {
            return;
        }

        cancel.ThrowIfCancellationRequested();
        if (!match.Sought.SequenceEqual(Sought))
        {
            var kindBefore = TileKind;
            if (match.Sought.Count < 2)
            {
                // One kind set meanwhile (e.g. by hand): the other is left, one tab, nothing picked.
                IsHeroTab = false;
                ChosenCover = null;
                ChosenHero = null;
            }

            Sought = match.Sought;
            if (TileKind != kindBefore)
            {
                // Other tiles: the hits shown were checked for the other kind.
                ShowKnownHits(match);
                FocusHit(0);
            }
        }

        // Hits dropped meanwhile (no image, found on another screen) leave; the others stay where the focus is.
        var current = match.Hits.Select(h => h.Id).ToHashSet();
        for (var i = Hits.Count - 1; i >= 0; i--)
        {
            if (!current.Contains(((ArtworkHitViewModel)Hits[i]).Hit.Id))
            {
                Hits.RemoveAt(i);
            }
        }

        LoadShownTiles(cancel);
        if (IsHeroTab)
        {
            // The waiting match knows which hits have a cover; whether they have a background is asked (once a session).
            await CheckInOrderAsync(match.Hits, int.MaxValue, hit => _artwork.CheckHitAsync(hit, ArtworkKind.Hero, cancel), cancel);
            return;
        }

        await CheckInOrderAsync(match.Hits, int.MaxValue, hit => _artwork.CheckWaitingHitAsync(game, hit, cancel), cancel);
    }

    /// <summary>An own term: SteamGridDB's results, the first with images up to <see cref="SteamGridDbClient.MaxOwnTermHits"/>.</summary>
    protected override async Task SearchAsync(string term, CancellationToken cancel)
    {
        IsChecking = true;
        Message = null;
        TilesLoad = true;
        var search = await Task.Run(() => _artwork.SearchByTermAsync(term, cancel), cancel);
        cancel.ThrowIfCancellationRequested();
        switch (search.Outcome)
        {
            case SteamGridDbOutcome.KeyRejected:
                Done(KeyFailure);
                return;
            case SteamGridDbOutcome.NoConnection:
                Done(AppTexts.WaitingUnreachable);
                return;
        }

        var kind = TileKind;
        await CheckInOrderAsync(search.Hits, SteamGridDbClient.MaxOwnTermHits, hit => _artwork.CheckHitAsync(hit, kind, cancel), cancel);
    }

    protected override Task<ArtworkFile?> TileImageAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        var kind = TileKind;
        var hit = ((ArtworkHitViewModel)tile).Hit;
        return Task.Run(() => _artwork.HitImageAsync(Waiting.Game, hit, kind, cancel), cancel);
    }

    /// <summary>
    /// Checks the hits not shown yet, <see cref="ParallelChecks"/> at a time, and shows each with an image at its place in
    /// <paramref name="hits"/>' order without moving the focus off the hit it is on. Stops at the first failure (a key
    /// turned down or SteamGridDB not reachable): one request, not one per hit; the rest is checked the next time.
    /// </summary>
    private async Task CheckInOrderAsync(IReadOnlyList<SteamGridDbHit> hits, int most, Func<SteamGridDbHit, Task<HitCheck>> check, CancellationToken cancel)
    {
        var order = hits.Select((hit, index) => (hit.Id, index)).ToDictionary(e => e.Id, e => e.index);
        var shown = ShownIds();
        var running = new List<Task<(SteamGridDbHit Hit, HitCheck Check)>>();
        string? failure = null;
        IsChecking = hits.Any(h => !shown.Contains(h.Id));
        foreach (var hit in hits.Where(h => !shown.Contains(h.Id)))
        {
            while (running.Count > 0 && (running.Count >= ParallelChecks || Hits.Count + running.Count >= most))
            {
                var finished = Apply(await TakeFinishedAsync(running), order);
                failure ??= finished;
            }

            if (failure is not null || Hits.Count >= most)
            {
                break;
            }

            running.Add(CheckOffThreadAsync(hit, check, cancel));
        }

        while (running.Count > 0)
        {
            var finished = Apply(await TakeFinishedAsync(running), order);
            failure ??= finished;
        }

        cancel.ThrowIfCancellationRequested();
        while (Hits.Count > most)
        {
            Hits.RemoveAt(Hits.Count - 1);
        }

        Done(Hits.Count > 0 ? null : failure ?? AppTexts.WaitingNoHits);
    }

    /// <summary>The game wanted for the focus showed: the focus goes to it, unless the player moved meanwhile.</summary>
    private void FocusIfWanted(int at, SteamGridDbHit hit)
    {
        if (_focusOn == hit.Id && !PlayerMoved)
        {
            _focusOn = null;
            PlayerMoved = true;
            Zone = WaitingHitsFocus.Grid;
            FocusHit(at);
        }
    }

    /// <summary>Why SteamGridDB isn't asked: a deleted key is no rejected one (#532).</summary>
    private string KeyFailure => _artwork.SteamGridDb?.State is null or SteamGridDbKeyState.None
        ? AppTexts.DataSourcesKeyMissing
        : AppTexts.DataSourcesKeyRejected;

    private HashSet<int> ShownIds() => Hits.Select(h => ((ArtworkHitViewModel)h).Hit.Id).ToHashSet();

    private static async Task<(SteamGridDbHit Hit, HitCheck Check)> CheckOffThreadAsync(
        SteamGridDbHit hit, Func<SteamGridDbHit, Task<HitCheck>> check, CancellationToken cancel) =>
        (hit, await Task.Run(() => check(hit), cancel));

    private static async Task<(SteamGridDbHit Hit, HitCheck Check)> TakeFinishedAsync(List<Task<(SteamGridDbHit Hit, HitCheck Check)>> running)
    {
        var finished = await Task.WhenAny(running);
        running.Remove(finished);
        return await finished;
    }

    /// <summary>A hit's check shown: with an image it joins the grid; the failure's message, or null.</summary>
    private string? Apply((SteamGridDbHit Hit, HitCheck Check) result, Dictionary<int, int> order)
    {
        switch (result.Check)
        {
            case HitCheck.HasImage:
                var at = Hits.Count(h => order.GetValueOrDefault(((ArtworkHitViewModel)h).Hit.Id, int.MaxValue) < order[result.Hit.Id]);
                InsertHit(at, NewHit(result.Hit));
                FocusIfWanted(at, result.Hit);
                return null;
            case HitCheck.KeyRejected:
                return KeyFailure;
            case HitCheck.NoConnection:
                return AppTexts.WaitingUnreachable;
            default:
                return null;
        }
    }
}
