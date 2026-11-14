using Arcadia.App.Input;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>
/// "Altersfreigabe" (#466, provisional; design T44F08): first the system (only those the tab has ratings of, the region's
/// or else the first present one to start with), then one or more of its ratings, "Alle" first; switching the system
/// clears the ratings. No conversion: an entry without this system's rating is "ohne Angabe". The focus goes down from
/// the systems to the ratings and the switch.
/// </summary>
public sealed partial class FilterRatingPane : FilterPane
{
    private readonly AgeRatingBoard _defaultBoard;
    private Stop _stop = Stop.Ratings;
    private int _system;
    private int _rating;
    private RatingChoice? _choice;

    public FilterRatingPane(IFilterPaneHost host)
        : base(FilterCategory.AgeRating, host)
    {
        _defaultBoard = host.Setup.Offer.DefaultBoard(host.Setup.FirstAgeRatingBoard);
        Systems = [.. host.Setup.Offer.Boards.Select(board => new FilterChipViewModel(NameOf(board), chip => PickSystem(chip)) { Tag = board })];
        Board = _defaultBoard;
        Ratings = Tiles(Board);
    }

    private enum Stop
    {
        Systems,
        Ratings,
        WithUnknown,
    }

    public IReadOnlyList<FilterChipViewModel> Systems { get; }

    /// <summary>"Alle" and the ratings of <see cref="Board"/>, in its order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FilterRatingTile> Ratings { get; private set; }

    [ObservableProperty]
    public partial AgeRatingBoard Board { get; private set; }

    [ObservableProperty]
    public partial bool ShowsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknownFocused { get; private set; }

    [ObservableProperty]
    public partial string WithUnknownCount { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string WithUnknownInfo { get; private set; } = string.Empty;

    public static string Note => AppTexts.FilterRatingNote;

    public override bool MovesInGrid => _stop != Stop.WithUnknown;

    public override string MoveText => AppTexts.FilterHintChoose;

    public override string AcceptText => _stop == Stop.Systems ? AppTexts.FilterHintSystem : AppTexts.FilterHintToggle;

    public override bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                return false;
            case UiAction.Up:
                Move(-1);
                break;
            case UiAction.Down:
                Move(1);
                break;
            case UiAction.Left when _stop == Stop.WithUnknown || IndexIn(_stop) == 0:
                return false;
            case UiAction.Left:
                FocusIn(_stop, IndexIn(_stop) - 1);
                break;
            case UiAction.Right when _stop == Stop.Systems:
                FocusIn(_stop, Math.Min(Systems.Count - 1, _system + 1));
                break;
            case UiAction.Right when _stop == Stop.Ratings:
                FocusIn(_stop, Math.Min(Ratings.Count - 1, _rating + 1));
                break;
            case UiAction.Accept when _stop == Stop.Systems:
                PickSystem(Systems[_system]);
                break;
            case UiAction.Accept when _stop == Stop.Ratings:
                PickRating(Ratings[_rating]);
                break;
            case UiAction.Accept:
                ToggleWithUnknown();
                break;
        }

        return true;
    }

    public override void Show(GameFilter chosen, FilterCounts counts)
    {
        _choice = chosen.Rating;
        var board = _choice?.Board ?? _defaultBoard;
        if (board != Board || Ratings.Count == 0)
        {
            Board = board;
            Ratings = Tiles(board);

            // The ratings are new; the focus stays where it was, on the system just picked or at the same place among the ratings.
            FocusIn(_stop, IndexIn(_stop));
        }

        foreach (var system in Systems)
        {
            system.IsSelected = (AgeRatingBoard)system.Tag! == board;
        }

        foreach (var tile in Ratings)
        {
            tile.IsSelected = tile.Rating is null ? _choice is not { IsActive: true } : _choice?.Ratings.Contains(tile.Rating) == true;
            var count = tile.Rating is null ? counts.AllOf(FilterCategory.AgeRating) : counts.OfRating(board, tile.Rating);
            tile.HasNoHits = count == 0;
            tile.CountText = tile.Rating is null ? (count == 1 ? AppTexts.FilterEntriesOne : AppTexts.FilterEntries(CountOf(count))) : CountOf(count);
        }

        ShowsWithUnknown = _choice is { IsActive: true };
        IsWithUnknown = _choice?.WithUnknown == true;
        WithUnknownCount = CountOf(counts.LackingRating(board));
        WithUnknownInfo = AppTexts.FilterWithUnknownInfo(AppTexts.FilterLackingRating(NameOf(board)));
        if (!ShowsWithUnknown && _stop == Stop.WithUnknown)
        {
            FocusIn(Stop.Ratings, _rating);
        }
    }

    public override (string Summary, string Fallback, bool IsChosen) Summarize(GameFilter chosen)
    {
        if (chosen.Rating is not { IsActive: true } rating)
        {
            return (AppTexts.FilterAllRatings, AppTexts.FilterAllRatings, false);
        }

        var summary = AppTexts.FilterRatingSummary(board: NameOf(rating.Board), ratings: string.Join(", ", rating.Ratings));
        var shown = WithUnknownTail(summary, rating.WithUnknown);
        return (shown, rating.Ratings.Count > 1 ? WithUnknownTail(AppTexts.FilterSeveral, rating.WithUnknown) : shown, true);
    }

    [RelayCommand]
    private void ToggleWithUnknown()
    {
        if (_choice is { IsActive: true } rating)
        {
            Host.Change(filter => filter.WithRating(rating.WithUnknownEntries(!rating.WithUnknown)));
        }
    }

    // The design starts on "Alle" (T44F08).
    protected override void Entered() => FocusIn(Stop.Ratings, 0);

    protected override void Left() => FocusIn(_stop, IndexIn(_stop), focused: false);

    /// <summary>A system: its ratings show, the ones chosen under another system are cleared (#466 case 13).</summary>
    private void PickSystem(FilterChipViewModel chip)
    {
        Host.FocusValues(this);
        FocusIn(Stop.Systems, Systems.ToList().IndexOf(chip));
        var board = (AgeRatingBoard)chip.Tag!;
        if (board != (_choice?.Board ?? _defaultBoard))
        {
            Host.Change(filter => filter.WithRating(RatingChoice.Of(board, [])));
        }
    }

    private void PickRating(FilterRatingTile tile)
    {
        Host.FocusValues(this);
        FocusIn(Stop.Ratings, Ratings.ToList().IndexOf(tile));
        var board = Board;
        Host.Change(filter =>
        {
            var current = filter.Rating is { } rating && rating.Board == board ? rating : RatingChoice.Of(board, []);
            return filter.WithRating(tile.Rating is null ? RatingChoice.Of(board, []) : current.With(tile.Rating, !current.Ratings.Contains(tile.Rating)));
        });
    }

    private void Move(int delta)
    {
        var stops = new List<Stop>();
        if (Systems.Count > 1)
        {
            stops.Add(Stop.Systems);
        }

        stops.Add(Stop.Ratings);
        if (ShowsWithUnknown)
        {
            stops.Add(Stop.WithUnknown);
        }

        var at = Math.Max(0, stops.IndexOf(_stop));
        var next = stops[Arcadia.Core.Navigation.ListFocus.Cycle(at, delta, stops.Count, IsRepeat)];
        FocusIn(next, IndexIn(next));
    }

    private int IndexIn(Stop stop) => stop == Stop.Systems ? _system : _rating;

    private void FocusIn(Stop stop, int index, bool focused = true)
    {
        _stop = stop;
        if (stop == Stop.Systems)
        {
            _system = Math.Clamp(index, 0, Math.Max(0, Systems.Count - 1));
        }
        else if (stop == Stop.Ratings)
        {
            _rating = Math.Clamp(index, 0, Math.Max(0, Ratings.Count - 1));
        }

        var on = focused && IsActive;
        for (var i = 0; i < Systems.Count; i++)
        {
            Systems[i].IsFocused = on && stop == Stop.Systems && i == _system;
        }

        for (var i = 0; i < Ratings.Count; i++)
        {
            Ratings[i].IsFocused = on && stop == Stop.Ratings && i == _rating;
        }

        IsWithUnknownFocused = on && stop == Stop.WithUnknown;
        OnPropertyChanged(nameof(MovesInGrid));
        OnPropertyChanged(nameof(AcceptText));
        Host.HintsChanged();
    }

    private List<FilterRatingTile> Tiles(AgeRatingBoard board) =>
        [new FilterRatingTile(null, AppTexts.FilterAll, AppTexts.FilterAll.ToUpperInvariant(), PickRating), .. AgeRatingScale.Of(board).Select(rating => new FilterRatingTile(rating, rating, NameOf(board), PickRating))];

    /// <summary>USK, PEGI, ESRB: the boards' own names, not UI texts.</summary>
    private static string NameOf(AgeRatingBoard board) => board.ToString().ToUpperInvariant();
}

/// <summary>A rating's tile: the system above, the rating large, its hits below; "Alle" clears the ratings.</summary>
public sealed partial class FilterRatingTile(string? rating, string value, string caption, Action<FilterRatingTile> pick) : ObservableObject
{
    /// <summary>The board's own value; null for "Alle".</summary>
    public string? Rating => rating;

    public string Value => value;

    public string Caption => caption;

    public bool IsAll => rating is null;

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [ObservableProperty]
    public partial bool HasNoHits { get; set; }

    [RelayCommand]
    private void Pick() => pick(this);
}
