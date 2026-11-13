using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>
/// "Erscheinungsjahr" (#466, design T44F07): a bar chart of the entries per year from the tab's earliest to its latest
/// (pre-orders included), the quick picks, "von" and "bis" (◀▶ one year, LT/RT ten, never past the tab's years, "von"
/// never after "bis") and "Ohne Angabe einbeziehen". The focus goes down from the quick picks to "von", "bis" and the switch.
/// </summary>
public sealed partial class FilterYearPane : FilterPane
{
    private const int ChartHeight = 160;

    private readonly int _earliest;
    private readonly int _latest;
    private readonly int _thisYear;
    private Stop _stop = Stop.From;
    private int _pick;
    private YearChoice? _choice;

    public FilterYearPane(IFilterPaneHost host)
        : base(FilterCategory.Year, host)
    {
        _earliest = host.Setup.Offer.EarliestYear;
        _latest = host.Setup.Offer.LatestYear;
        _thisYear = host.Setup.ThisYear;
        Picks = [.. YearPicks.All.Select(pick => new FilterChipViewModel(LabelOf(pick), chip => PickChip(chip)) { Tag = pick })];
        Bars = [.. Enumerable.Range(_earliest, _latest - _earliest + 1).Select(year => new FilterYearBar(year, year % 5 == 0 || year == _earliest || year == _latest))];
        ChartText = AppTexts.FilterYearsChart(from: _earliest, to: _latest);
        WithUnknownInfo = AppTexts.FilterWithUnknownInfo(AppTexts.FilterLackingYear);
    }

    private enum Stop
    {
        Picks,
        From,
        To,
        WithUnknown,
    }

    public IReadOnlyList<FilterChipViewModel> Picks { get; }

    public IReadOnlyList<FilterYearBar> Bars { get; }

    public string ChartText { get; }

    [ObservableProperty]
    public partial string FromText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ToText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFromFocused { get; private set; }

    [ObservableProperty]
    public partial bool IsToFocused { get; private set; }

    [ObservableProperty]
    public partial bool ShowsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknownFocused { get; private set; }

    [ObservableProperty]
    public partial string WithUnknownCount { get; private set; } = string.Empty;

    public string WithUnknownInfo { get; }

    public override bool MovesInGrid => _stop == Stop.Picks;

    public override string MoveText => _stop == Stop.Picks ? AppTexts.FilterHintChoose : AppTexts.FilterHintRow;

    public override string AcceptText => _stop == Stop.WithUnknown ? AppTexts.FilterHintToggle : AppTexts.FilterHintChoose;

    public override string? StepText => _stop is Stop.From or Stop.To ? AppTexts.FilterHintYear : null;

    public override string? StepTenText => _stop is Stop.From or Stop.To ? AppTexts.FilterHintTenYears : null;

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
            case UiAction.Left when _stop == Stop.Picks && _pick == 0:
                return false;
            case UiAction.Left when _stop == Stop.Picks:
                FocusPick(_pick - 1);
                break;
            case UiAction.Right when _stop == Stop.Picks:
                FocusPick(Math.Min(Picks.Count - 1, _pick + 1));
                break;
            case UiAction.Left or UiAction.Right when _stop is Stop.From or Stop.To:
                StepYear(_stop, action == UiAction.Left ? -1 : 1);
                break;
            case UiAction.PreviousPage or UiAction.NextPage when _stop is Stop.From or Stop.To:
                StepYear(_stop, action == UiAction.PreviousPage ? -10 : 10);
                break;
            case UiAction.Left when _stop == Stop.WithUnknown:
                return false;
            case UiAction.Accept when _stop == Stop.Picks:
                PickChip(Picks[_pick]);
                break;
            case UiAction.Accept when _stop == Stop.WithUnknown:
                ToggleWithUnknown();
                break;
        }

        return true;
    }

    public override void Show(GameFilter chosen, FilterCounts counts)
    {
        _choice = chosen.Years;
        var (from, to) = Range;
        FromText = Formatting.Number(from);
        ToText = Formatting.Number(to);
        var picked = YearPicks.Of(_choice, _thisYear);
        foreach (var chip in Picks)
        {
            var pick = (YearPick)chip.Tag!;
            chip.IsSelected = pick == picked;
            chip.HasNoHits = YearPicks.RangeOf(pick, _thisYear) is { } range && counts.OfYears(range.From, range.To) == 0;
        }

        var most = Math.Max(1, Bars.Max(bar => counts.OfYear(bar.Year)));
        foreach (var bar in Bars)
        {
            bar.Height = Math.Round(ChartHeight * counts.OfYear(bar.Year) / (double)most);
            bar.IsInRange = _choice is not null && bar.Year >= from && bar.Year <= to;
            bar.IsChosenRange = _choice is not null;
        }

        ShowsWithUnknown = _choice is not null;
        IsWithUnknown = _choice?.WithUnknown == true;
        WithUnknownCount = CountOf(counts.LackingYear);
        if (!ShowsWithUnknown && _stop == Stop.WithUnknown)
        {
            Focus(Stop.To);
        }
    }

    public override (string Summary, string Fallback, bool IsChosen) Summarize(GameFilter chosen)
    {
        if (chosen.Years is not { } years)
        {
            return (AppTexts.FilterAllYears, AppTexts.FilterAllYears, false);
        }

        var summary = YearPicks.Of(years, _thisYear) is { } pick ? LabelOf(pick)
            : years.From == years.To ? Formatting.Number(years.From)
            : AppTexts.FilterYearRange(from: Formatting.Number(Math.Max(years.From, _earliest)), to: Formatting.Number(Math.Min(years.To, _latest)));
        return (WithUnknownTail(summary, years.WithUnknown), WithUnknownTail(AppTexts.FilterSeveral, years.WithUnknown), true);
    }

    [RelayCommand]
    private void FromEarlier() => Click(Stop.From, -1);

    [RelayCommand]
    private void FromLater() => Click(Stop.From, 1);

    [RelayCommand]
    private void ToEarlier() => Click(Stop.To, -1);

    [RelayCommand]
    private void ToLater() => Click(Stop.To, 1);

    [RelayCommand]
    private void ToggleWithUnknown()
    {
        if (_choice is { } years)
        {
            Host.Change(filter => filter.WithYears(years with { WithUnknown = !years.WithUnknown }));
        }
    }

    protected override void Entered() => Focus(Stop.From);

    protected override void Left() => Focus(_stop, focused: false);

    /// <summary>The range shown: the choice within the tab's years, else all of them.</summary>
    private (int From, int To) Range => _choice is { } years
        ? (Math.Clamp(years.From, _earliest, _latest), Math.Clamp(years.To, _earliest, _latest))
        : (_earliest, _latest);

    private void Click(Stop stop, int years)
    {
        Host.FocusValues(this);
        Focus(stop);
        StepYear(stop, years);
    }

    private void StepYear(Stop stop, int years)
    {
        var (from, to) = Range;
        if (stop == Stop.From)
        {
            from = Math.Clamp(from + years, _earliest, to);
        }
        else
        {
            to = Math.Clamp(to + years, from, _latest);
        }

        var withUnknown = _choice?.WithUnknown == true;
        // The tab's whole range is "Alle Jahre" (design T44F07).
        Host.Change(filter => filter.WithYears(from == _earliest && to == _latest ? null : new YearChoice(from, to, withUnknown)));
    }

    private void PickChip(FilterChipViewModel chip)
    {
        Host.FocusValues(this);
        _pick = Picks.ToList().IndexOf(chip);
        Focus(Stop.Picks);
        var withUnknown = _choice?.WithUnknown == true;
        Host.Change(filter => filter.WithYears(YearPicks.RangeOf((YearPick)chip.Tag!, _thisYear) is { } range ? new YearChoice(range.From, range.To, withUnknown) : null));
    }

    private void Move(int delta)
    {
        var stops = new List<Stop> { Stop.Picks, Stop.From, Stop.To };
        if (ShowsWithUnknown)
        {
            stops.Add(Stop.WithUnknown);
        }

        var at = stops.IndexOf(_stop);
        Focus(stops[Arcadia.Core.Navigation.ListFocus.Cycle(at, delta, stops.Count, IsRepeat)]);
    }

    private void FocusPick(int index)
    {
        _pick = index;
        Focus(Stop.Picks);
    }

    private void Focus(Stop stop, bool focused = true)
    {
        _stop = stop;
        var on = focused && IsActive;
        for (var i = 0; i < Picks.Count; i++)
        {
            Picks[i].IsFocused = on && stop == Stop.Picks && i == _pick;
        }

        IsFromFocused = on && stop == Stop.From;
        IsToFocused = on && stop == Stop.To;
        IsWithUnknownFocused = on && stop == Stop.WithUnknown;
        OnPropertyChanged(nameof(MovesInGrid));
        OnPropertyChanged(nameof(MoveText));
        OnPropertyChanged(nameof(AcceptText));
        OnPropertyChanged(nameof(StepText));
        OnPropertyChanged(nameof(StepTenText));
        Host.HintsChanged();
    }

    private static string LabelOf(YearPick pick) => pick switch
    {
        YearPick.LastThreeYears => AppTexts.FilterLastThreeYears,
        YearPick.Twenties => AppTexts.FilterTwenties,
        YearPick.Tens => AppTexts.FilterTens,
        YearPick.Noughties => AppTexts.FilterNoughties,
        YearPick.Before2000 => AppTexts.FilterBefore2000,
        _ => AppTexts.FilterAllYears,
    };
}

/// <summary>A year's bar in the chart of "Erscheinungsjahr": its height and whether it lies in the chosen range.</summary>
public sealed partial class FilterYearBar(int year, bool hasLabel) : ObservableObject
{
    public int Year => year;

    /// <summary>The year under the bar, every fifth and the ends.</summary>
    public string Label => hasLabel ? Formatting.Number(year) : string.Empty;

    [ObservableProperty]
    public partial double Height { get; set; }

    [ObservableProperty]
    public partial bool IsInRange { get; set; }

    /// <summary>A range is chosen: the bars outside it dim.</summary>
    [ObservableProperty]
    public partial bool IsChosenRange { get; set; }
}

/// <summary>A chip of the filter (#466): a quick pick of the year, a rating system; one at a time is chosen.</summary>
public sealed partial class FilterChipViewModel(string label, Action<FilterChipViewModel> pick) : ObservableObject
{
    public string Label => label;

    /// <summary>What the chip stands for (a <see cref="YearPick"/>, an age rating board).</summary>
    public object? Tag { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A quick pick without hits: greyed and still choosable (#466 case 11).</summary>
    [ObservableProperty]
    public partial bool HasNoHits { get; set; }

    [RelayCommand]
    private void Pick() => pick(this);
}
