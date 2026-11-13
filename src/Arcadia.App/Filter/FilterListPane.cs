using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>
/// Genre, tags, developer or publisher (#466, designs T44F04–T44F06): "Alle …" first, then every value with the hits it
/// would bring, three to a row; "Häufigste zuerst" puts the chosen values on top and those without hits at the end,
/// "A–Z" groups them by letter beside the rows. [L3] switches the order (genres start in A–Z), LT/RT page or jump a
/// letter, [X] searches the values with the on-screen keyboard. The order is set when the list is shown, sorted again or
/// searched, not at each choice, so a value doesn't jump away from the focus. Own tags are the first group of "Tags".
/// </summary>
public sealed partial class FilterListPane : FilterPane
{
    public const int Columns = 3;

    /// <summary>Rows in sight; fewer while the on-screen keyboard takes the lower half (design T44F06).</summary>
    public const int RowsInSight = 9;

    public const int RowsInSightWhileWriting = 4;

    private readonly FilterList _list;
    private readonly List<FilterValueViewModel> _values = [];
    private readonly FilterValueViewModel _all;
    private List<FilterListRow> _rows = [];
    private GameFilter _chosen = GameFilter.None;
    private FilterCounts? _counts;
    private int _focusRow;
    private int _focusColumn;
    private int _firstRow;

    public FilterListPane(FilterCategory category, IFilterPaneHost host)
        : base(category, host)
    {
        _list = ListOf(category);
        IsAlphabetical = _list == FilterList.Genre;
        _all = new FilterValueViewModel(AllText(_list), null, Pick) { IsAll = true };
        foreach (var own in _list == FilterList.Tag ? host.Setup.Offer.OwnTags : [])
        {
            _values.Add(new FilterValueViewModel(own.Label, null, Pick) { Key = own.Key, IsOwn = true });
        }

        foreach (var name in host.Setup.Offer.NamesOf(_list))
        {
            _values.Add(new FilterValueViewModel(name.Label, null, Pick) { Key = name.Key, Entries = name.Entries });
        }

        WithUnknownInfo = AppTexts.FilterWithUnknownInfo(LackingText(_list));
    }

    public FilterList List => _list;

    public string SearchLabel => _list switch
    {
        FilterList.Genre => AppTexts.FilterSearchGenres,
        FilterList.Tag => AppTexts.FilterSearchTags,
        FilterList.Developer => AppTexts.FilterSearchDevelopers,
        _ => AppTexts.FilterSearchPublishers,
    };

    /// <summary>"A–Z" grouped by letter; else "Häufigste zuerst".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderText))]
    public partial bool IsAlphabetical { get; private set; }

    /// <summary>The values' search; empty shows them all.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearch))]
    public partial string Search { get; private set; } = string.Empty;

    public bool HasSearch => Search.Length > 0;

    /// <summary>The value search being typed, with the on-screen keyboard over the lower half.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWriting), nameof(AcceptText), nameof(BackText))]
    public partial TextEditorViewModel? Editor { get; private set; }

    public override bool IsWriting => Editor is not null;

    [ObservableProperty]
    public partial IReadOnlyList<FilterListRow> VisibleRows { get; private set; } = [];

    /// <summary>The letters of "A–Z" beside the list, the focused value's highlighted.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FilterLetter> Letters { get; private set; } = [];

    /// <summary>"Mehrere wählbar · …", "2 gewählt · …" or, while searching, how many values match.</summary>
    [ObservableProperty]
    public partial string MatchNote { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool FadesTop { get; private set; }

    [ObservableProperty]
    public partial bool FadesBottom { get; private set; }

    /// <summary>The scroll bar's thumb as fractions of the track: where it starts and how long it is.</summary>
    [ObservableProperty]
    public partial double ScrollStart { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScrollEnd))]
    public partial double ScrollLength { get; private set; } = 1;

    /// <summary>The scroll bar's maximum: its values are fractions of all rows.</summary>
    public double ScrollEnd => 1 - ScrollLength;

    [ObservableProperty]
    public partial bool HasScrollBar { get; private set; }

    /// <summary>"Ohne Angabe einbeziehen" shows once something is chosen (#44).</summary>
    [ObservableProperty]
    public partial bool ShowsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknown { get; private set; }

    [ObservableProperty]
    public partial bool IsWithUnknownFocused { get; private set; }

    /// <summary>How many entries without a value the switch adds.</summary>
    [ObservableProperty]
    public partial string WithUnknownCount { get; private set; } = string.Empty;

    public string WithUnknownInfo { get; }

    public override bool MovesInGrid => true;

    public override string AcceptText => IsWriting ? AppTexts.FilterHintType : AppTexts.FilterHintToggle;

    public string BackText => IsWriting ? AppTexts.FilterHintHideKeyboard : AppTexts.Back;

    public override string? SearchText => IsWriting ? null : AppTexts.FilterHintSearch;

    public override string? PageText => IsWriting || _rows.Count <= RowsInSight ? null : IsAlphabetical ? AppTexts.FilterHintLetter : AppTexts.FilterHintPage;

    public override string? OrderText => IsWriting ? null : AppTexts.FilterHintOrder(IsAlphabetical ? AppTexts.FilterAlphabetical : AppTexts.FilterByFrequency);

    public override bool Handle(UiAction action)
    {
        if (Editor is { } editor)
        {
            editor.Handle(action);
            return true;
        }

        switch (action)
        {
            case UiAction.Back:
                return false;
            case UiAction.Up:
                Step(-1);
                break;
            case UiAction.Down:
                Step(1);
                break;
            case UiAction.Left when IsWithUnknownFocused || _focusColumn == 0:
                return false;
            case UiAction.Left:
                FocusCell(_focusRow, _focusColumn - 1);
                break;
            case UiAction.Right when !IsWithUnknownFocused && _focusColumn < CellsOf(_focusRow) - 1:
                FocusCell(_focusRow, _focusColumn + 1);
                break;
            case UiAction.Accept when IsWithUnknownFocused:
                ToggleWithUnknown();
                break;
            case UiAction.Accept:
                if (FocusedValue is { } value)
                {
                    Choose(value);
                }

                break;
            case UiAction.ViewOptions:
                ToggleOrder();
                break;
            case UiAction.Search:
                WriteSearch();
                break;
            case UiAction.PreviousPage:
                Jump(-1);
                break;
            case UiAction.NextPage:
                Jump(1);
                break;
        }

        return true;
    }

    public override void Show(GameFilter chosen, FilterCounts counts)
    {
        _chosen = chosen;
        var choice = chosen.NamesOf(_list);
        var first = _counts is null;
        _counts = counts;
        _all.IsSelected = !choice.IsActive;
        _all.Count = counts.AllOf(Category);
        _all.CountText = CountOf(_all.Count);
        foreach (var value in _values)
        {
            value.IsSelected = choice.Has(value.Key, value.IsOwn);
            value.Count = counts.Of(_list, value.Key, value.IsOwn);
            value.CountText = CountOf(value.Count);
        }

        ShowsWithUnknown = choice.IsActive;
        IsWithUnknown = choice.WithUnknown;
        WithUnknownCount = CountOf(counts.Lacking(_list));
        if (!ShowsWithUnknown && IsWithUnknownFocused)
        {
            IsWithUnknownFocused = false;
            FocusCell(_focusRow, _focusColumn);
        }

        if (first)
        {
            Arrange();
        }

        ShowMatchNote();
    }

    public override (string Summary, string Fallback, bool IsChosen) Summarize(GameFilter chosen)
    {
        var choice = chosen.NamesOf(_list);
        if (!choice.IsActive)
        {
            return (AllText(_list), AllText(_list), false);
        }

        var labels = choice.Own.Concat(choice.Names.Select(LabelOf));
        return (WithUnknownTail(string.Join(", ", labels), choice.WithUnknown), WithUnknownTail(AppTexts.FilterSeveral, choice.WithUnknown), true);
    }

    /// <summary>A click on a value: it takes the focus and is chosen or cleared.</summary>
    private void Pick(FilterValueViewModel value)
    {
        Host.FocusValues(this);
        if (FindCell(value) is { } cell)
        {
            IsWithUnknownFocused = false;
            FocusCell(cell.Row, cell.Column);
        }

        Choose(value);
    }

    /// <summary>The wheel over the list: three rows a notch, the focus stays where it is.</summary>
    public void Scroll(int rows)
    {
        _firstRow = Math.Clamp(_firstRow + rows, 0, Math.Max(0, _rows.Count - InSight));
        ShowRows();
    }

    [RelayCommand]
    private void ToggleOrder()
    {
        var focused = FocusedValue;
        IsAlphabetical = !IsAlphabetical;
        Arrange(focused);
    }

    [RelayCommand]
    private void ToggleWithUnknown()
    {
        if (ShowsWithUnknown)
        {
            Host.Change(filter => filter.WithNames(_list, filter.NamesOf(_list).WithUnknownEntries(!filter.NamesOf(_list).WithUnknown)));
        }
    }

    [RelayCommand]
    private void WriteSearch()
    {
        if (Editor is not null)
        {
            return;
        }

        Host.FocusValues(this);
        TextEditorViewModel? editor = null;
        editor = Host.Write(Search, typed =>
        {
            Search = typed.Trim();
            Arrange();
        }, () =>
        {
            if (Editor == editor)
            {
                Editor = null;
                editor?.Dispose();
                Arrange();
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(OrderText));
                OnPropertyChanged(nameof(PageText));
            }
        });
        Editor = editor;
        Arrange();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(OrderText));
        OnPropertyChanged(nameof(PageText));
    }

    protected override void Entered()
    {
        IsWithUnknownFocused = false;
        FocusCell(0, 0);
    }

    /// <summary>Leaving the category clears its search (#466 case 3).</summary>
    protected override void Left()
    {
        if (Editor is { } editor)
        {
            Editor = null;
            editor.Dispose();
        }

        IsWithUnknownFocused = false;
        if (FocusedValue is { } value)
        {
            value.IsFocused = false;
        }

        if (HasSearch)
        {
            Search = string.Empty;
            Arrange();
        }

        _firstRow = 0;
        ShowRows();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(OrderText));
    }

    private int InSight => IsWriting ? RowsInSightWhileWriting : RowsInSight;

    private FilterValueViewModel? FocusedValue => _focusRow < _rows.Count && _focusColumn < _rows[_focusRow].Cells.Count ? _rows[_focusRow].Cells[_focusColumn] : null;

    private int CellsOf(int row) => row < _rows.Count ? _rows[row].Cells.Count : 0;

    /// <summary>
    /// ▲▼: the row above or below, in the same column as far as it reaches; the switch "Ohne Angabe einbeziehen" stands
    /// above the first row. Round at the ends (#476); a held button stops there.
    /// </summary>
    private void Step(int delta)
    {
        var cellRows = Enumerable.Range(0, _rows.Count).Where(row => _rows[row].Cells.Count > 0).ToList();
        if (cellRows.Count == 0)
        {
            return;
        }

        if (IsWithUnknownFocused)
        {
            if (delta > 0 || !IsRepeat)
            {
                IsWithUnknownFocused = false;
                FocusCell(delta > 0 ? cellRows[0] : cellRows[^1], 0);
            }

            return;
        }

        var at = Math.Max(0, cellRows.IndexOf(_focusRow));
        var pastTop = delta < 0 && at == 0;
        var pastBottom = delta > 0 && at == cellRows.Count - 1;
        if (ShowsWithUnknown && (pastTop || (pastBottom && !IsRepeat)))
        {
            Unfocus();
            IsWithUnknownFocused = true;
            ShowRows();
            return;
        }

        FocusCell(cellRows[Arcadia.Core.Navigation.ListFocus.Cycle(at, delta, cellRows.Count, IsRepeat)], _focusColumn);
    }

    /// <summary>LT/RT: in "A–Z" the first value of the next or previous letter, else a page of rows.</summary>
    private void Jump(int direction)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        IsWithUnknownFocused = false;
        if (!IsAlphabetical)
        {
            FocusCell(NearestCellRow(Math.Clamp(_focusRow + direction * InSight, 0, _rows.Count - 1), direction), _focusColumn);
            return;
        }

        var letterRows = Enumerable.Range(0, _rows.Count).Where(row => _rows[row].Lead is { Length: > 0 }).ToList();
        var groupStart = letterRows.LastOrDefault(row => row <= _focusRow, -1);
        var target = direction > 0 ? letterRows.FirstOrDefault(row => row > _focusRow, -1) : letterRows.LastOrDefault(row => row < groupStart, -1);
        if (target >= 0)
        {
            FocusCell(target, 0);
        }
    }

    private int NearestCellRow(int row, int direction)
    {
        for (var step = 0; step < _rows.Count; step++)
        {
            foreach (var candidate in new[] { row + step * direction, row - step * direction })
            {
                if (candidate >= 0 && candidate < _rows.Count && _rows[candidate].Cells.Count > 0)
                {
                    return candidate;
                }
            }
        }

        return _focusRow;
    }

    private void FocusCell(int row, int column)
    {
        Unfocus();
        if (_rows.Count == 0)
        {
            _focusRow = _focusColumn = 0;
            ShowRows();
            return;
        }

        _focusRow = Math.Clamp(row, 0, _rows.Count - 1);
        if (_rows[_focusRow].Cells.Count == 0)
        {
            _focusRow = NearestCellRow(_focusRow, 1);
        }

        _focusColumn = Math.Clamp(column, 0, Math.Max(0, CellsOf(_focusRow) - 1));
        if (FocusedValue is { } value)
        {
            value.IsFocused = IsActive && !IsWriting;
        }

        // The focused row stays in sight with two rows below it where the list goes on (design T44Modal).
        if (_focusRow < _firstRow)
        {
            _firstRow = Math.Max(0, _focusRow - 1);
        }
        else if (_focusRow >= _firstRow + InSight - 2)
        {
            _firstRow = Math.Min(Math.Max(0, _rows.Count - InSight), _focusRow - InSight + 3);
        }

        ShowRows();
    }

    private void Unfocus()
    {
        if (FocusedValue is { } value)
        {
            value.IsFocused = false;
        }
    }

    private void Choose(FilterValueViewModel value)
    {
        if (value.IsAll)
        {
            Host.Change(filter => filter.WithNames(_list, NameChoice.None));
            return;
        }

        Host.Change(filter => filter.WithNames(_list, filter.NamesOf(_list).With(value.Key, value.IsOwn, !filter.NamesOf(_list).Has(value.Key, value.IsOwn))));
    }

    /// <summary>Orders and groups the values into rows again, keeping the focus on <paramref name="keep"/> or the same value.</summary>
    private void Arrange(FilterValueViewModel? keep = null)
    {
        keep ??= FocusedValue;
        Unfocus();
        var shown = HasSearch
            ? _values.Where(value => CultureInfo.CurrentCulture.CompareInfo.IndexOf(value.Label, Search, CompareOptions.IgnoreCase) >= 0).ToList()
            : _values;
        var own = shown.Where(value => value.IsOwn).ToList();
        var others = shown.Where(value => !value.IsOwn).ToList();
        var rows = new List<FilterListRow>();
        if (!HasSearch)
        {
            rows.Add(new FilterListRow(null, IsAlphabetical ? string.Empty : null, [_all]));
        }

        if (own.Count > 0 && !HasSearch)
        {
            rows.Add(FilterListRow.Heading(AppTexts.FilterOwnTagsHeading));
            AddRows(rows, Ordered(own));
            rows.Add(FilterListRow.Heading(AppTexts.FilterOtherTagsHeading));
            AddRows(rows, Ordered(others));
        }
        else
        {
            AddRows(rows, Ordered([.. own, .. others]));
        }

        _rows = rows;
        Letters = IsAlphabetical
            ? [.. rows.Select(row => row.Lead).Where(lead => lead is { Length: > 0 }).Distinct().Select(lead => new FilterLetter(lead!))]
            : [];
        var target = keep is null ? null : FindCell(keep);
        _firstRow = 0;
        FocusCell(target?.Row ?? 0, target?.Column ?? 0);
        OnPropertyChanged(nameof(PageText));
        ShowMatchNote();
    }

    /// <summary>Below the list: the search's hits while one is typed, else how the choice matches.</summary>
    private void ShowMatchNote()
    {
        var choice = _chosen.NamesOf(_list);
        MatchNote = HasSearch ? MatchNoteOf(_rows.Sum(row => row.Cells.Count))
            : choice.IsActive ? AppTexts.FilterMatchNoteChosen(choice.Names.Count + choice.Own.Count) : AppTexts.FilterMatchNote;
    }

    private void AddRows(List<FilterListRow> rows, List<FilterValueViewModel> values)
    {
        FilterListRow? row = null;
        string? letter = null;
        foreach (var value in values)
        {
            var lead = IsAlphabetical ? LetterOf(value) : null;
            if (row is null || row.Cells.Count == Columns || (IsAlphabetical && lead != letter))
            {
                // A–Z: the letter stands beside its group's first row, so a group of one value costs one row.
                row = new FilterListRow(null, IsAlphabetical ? (lead != letter ? lead : string.Empty) : null, []);
                rows.Add(row);
                letter = lead;
            }

            row.Cells.Add(value);
        }
    }

    private List<FilterValueViewModel> Ordered(List<FilterValueViewModel> values)
    {
        var compare = StringComparer.Create(Formatting.Culture, ignoreCase: true);
        return IsAlphabetical
            ? [.. values.OrderBy(value => value.Label, compare)]
            : [.. values
                .OrderByDescending(value => value.IsSelected)
                .ThenByDescending(value => value.Count)
                .ThenByDescending(value => value.Entries)
                .ThenBy(value => value.Label, compare)];
    }

    private (int Row, int Column)? FindCell(FilterValueViewModel value)
    {
        for (var row = 0; row < _rows.Count; row++)
        {
            var column = _rows[row].Cells.IndexOf(value);
            if (column >= 0)
            {
                return (row, column);
            }
        }

        return null;
    }

    private void ShowRows()
    {
        var inSight = InSight;
        _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, _rows.Count - inSight));
        VisibleRows = [.. _rows.Skip(_firstRow).Take(inSight)];
        FadesTop = _firstRow > 0;
        FadesBottom = _firstRow + inSight < _rows.Count;
        HasScrollBar = _rows.Count > inSight;
        ScrollStart = _rows.Count == 0 ? 0 : (double)_firstRow / _rows.Count;
        ScrollLength = _rows.Count == 0 ? 1 : Math.Min(1, (double)inSight / _rows.Count);
        var current = IsActive && !IsWithUnknownFocused ? LetterOf(FocusedValue) : null;
        foreach (var letter in Letters)
        {
            letter.IsCurrent = letter.Letter == current;
        }
    }

    private string LabelOf(string key) =>
        _values.FirstOrDefault(value => !value.IsOwn && FilterNames.Same(value.Key, key))?.Label ?? key;

    private static string? LetterOf(FilterValueViewModel? value)
    {
        if (value is null || value.IsAll || value.Label.Length == 0)
        {
            return null;
        }

        var first = value.Label.Normalize(System.Text.NormalizationForm.FormD)[0];
        return char.IsLetter(first) ? char.ToUpper(first, Formatting.Culture).ToString() : "#";
    }

    private static string MatchNoteOf(int count) => count switch
    {
        0 => AppTexts.FilterSearchNoMatch,
        1 => AppTexts.FilterSearchOneMatch,
        _ => AppTexts.FilterSearchMatches(Formatting.Count(count)),
    };

    public static FilterList ListOf(FilterCategory category) => category switch
    {
        FilterCategory.Genre => FilterList.Genre,
        FilterCategory.Tag => FilterList.Tag,
        FilterCategory.Developer => FilterList.Developer,
        _ => FilterList.Publisher,
    };

    private static string AllText(FilterList list) => list switch
    {
        FilterList.Genre => AppTexts.FilterAllGenres,
        FilterList.Tag => AppTexts.FilterAllTags,
        FilterList.Developer => AppTexts.FilterAllDevelopers,
        _ => AppTexts.FilterAllPublishers,
    };

    private static string LackingText(FilterList list) => list switch
    {
        FilterList.Genre => AppTexts.FilterLackingGenre,
        FilterList.Tag => AppTexts.FilterLackingTags,
        FilterList.Developer => AppTexts.FilterLackingDevelopers,
        _ => AppTexts.FilterLackingPublishers,
    };
}

/// <summary>
/// A row of a long list (#466): a group's heading ("EIGENE TAGS") or up to three values, in "A–Z" with the letter
/// beside the group's first row (<paramref name="Lead"/> empty on its other rows, null outside "A–Z").
/// </summary>
public sealed record FilterListRow(string? HeadingText, string? Lead, List<FilterValueViewModel> Cells)
{
    public bool IsHeading => HeadingText is not null;

    public bool HasLead => Lead is not null;

    public static FilterListRow Heading(string text) => new(text, null, []);
}

/// <summary>A letter of the bar beside "A–Z"; the focused value's is highlighted.</summary>
public sealed partial class FilterLetter(string letter) : ObservableObject
{
    public string Letter => letter;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}
