using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>
/// "Filter" (#44 part A, designs T44F02/T44F03/T44F09, T44Modal): the tab's categories on the left, the values of the one
/// in focus on the right, on top "Mit dieser Auswahl N von M Einträgen" live. ▲ ▼ move, ▶ or A go into the values, ◀ at
/// their left edge and B come back; B on the categories, R3 and the ✕ close it, and only then the choice applies (no
/// cancel: "Alle Filter zurücksetzen" is there). It counts the entries the tab held when it opened (#44 case 18).
/// </summary>
public sealed partial class FilterModalViewModel : ViewModelBase
{
    public static string Title => AppTexts.FilterTitle;

    public static string ResetText => AppTexts.FilterResetAll;

    public static string AllTypesText => AppTexts.FilterAllTypes;

    /// <summary>The type values stand in a grid of this many columns (design T44Modal).</summary>
    public const int TypeColumns = 3;

    private static readonly (InstalledChoice Choice, string Label)[] InstalledOptions =
    [
        (InstalledChoice.All, AppTexts.FilterAll),
        (InstalledChoice.Installed, AppTexts.FilterOnlyInstalled),
        (InstalledChoice.NotInstalled, AppTexts.FilterOnlyNotInstalled),
    ];

    private readonly IReadOnlyList<Game> _games;
    private readonly FilterOffer _offer;
    private readonly Action<FilterModalViewModel> _close;
    private readonly Dictionary<FilterValueViewModel, Func<GameFilter, GameFilter>> _choose = [];
    private readonly Dictionary<FilterValueViewModel, Func<GameFilter, FilterCounts, (bool Selected, int Count)>> _show = [];
    private int _focusedValue;

    /// <param name="close">The modal closes; the library applies <see cref="Chosen"/>.</param>
    public FilterModalViewModel(string launcherName, IReadOnlyList<Game> games, FilterOffer offer, GameFilter current, Action<FilterModalViewModel> close)
    {
        LauncherName = launcherName;
        _games = games;
        _offer = offer;
        _close = close;
        Chosen = current;
        var categories = new List<FilterCategoryViewModel>();
        if (offer.HasInstalled)
        {
            categories.Add(new FilterCategoryViewModel(FilterCategory.Installed, AppTexts.Installed, "IconInstalled", PickCategory));
        }

        if (offer.HasTypes)
        {
            categories.Add(new FilterCategoryViewModel(FilterCategory.Type, AppTexts.FilterType, "IconTypeGame", PickCategory));
        }

        Categories = categories;
        Current = categories[0];
        ShowCategory(categories[0]);
        FocusCategoryRow(categories[0]);
        Recount();
    }

    public string LauncherName { get; }

    public IReadOnlyList<FilterCategoryViewModel> Categories { get; }

    /// <summary>The category whose values show on the right: the one in focus, or the one the focus went into.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRadios), nameof(ShowsTypes), nameof(MovesInGrid), nameof(AcceptText))]
    public partial FilterCategoryViewModel Current { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<FilterValueViewModel> Values { get; private set; } = [];

    /// <summary>"Installiert": one of three, one per line.</summary>
    public bool ShowsRadios => Current.Category == FilterCategory.Installed;

    /// <summary>"Typ": several at once, in a grid of <see cref="TypeColumns"/>.</summary>
    public bool ShowsTypes => Current.Category == FilterCategory.Type;

    /// <summary>The choice so far; it applies when the modal closes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    public partial GameFilter Chosen { get; private set; }

    /// <summary>Something is chosen: "Alle Filter zurücksetzen" can be chosen, the hit count is in the accent colour.</summary>
    public bool IsActive => Chosen.IsActive;

    /// <summary>The focus is on the values, not on the categories.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveText), nameof(AcceptText), nameof(BackText), nameof(MovesInGrid))]
    public partial bool IsInValues { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AcceptText))]
    public partial bool IsResetFocused { get; private set; }

    /// <summary>"N" of "Mit dieser Auswahl N von M Einträgen".</summary>
    [ObservableProperty]
    public partial string HitsText { get; private set; } = string.Empty;

    /// <summary>"von M Einträgen".</summary>
    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    public string MoveText => IsInValues ? AppTexts.FilterValue : AppTexts.Category;

    /// <summary>The type grid moves on all four directions.</summary>
    public bool MovesInGrid => IsInValues && ShowsTypes;

    public string AcceptText => !IsInValues ? (IsResetFocused ? AppTexts.FilterHintReset : AppTexts.FilterValues) : ShowsTypes ? AppTexts.FilterHintToggle : AppTexts.Select;

    public string BackText => IsInValues ? AppTexts.Back : AppTexts.FilterHintCloseAndApply;

    /// <summary>A press while the modal is open; it takes every one.</summary>
    public void Handle(UiAction action)
    {
        if (action == UiAction.KeyboardLayout)
        {
            // R3 closes and applies, wherever the focus is (#44).
            _close(this);
            return;
        }

        if (IsInValues)
        {
            HandleInValues(action);
        }
        else
        {
            HandleInCategories(action);
        }
    }

    /// <summary>The ✕, a click beside the modal.</summary>
    [RelayCommand]
    private void Close() => _close(this);

    /// <summary>"Alle Filter zurücksetzen" was chosen: it clears what the tab doesn't show as well.</summary>
    public bool WasReset { get; private set; }

    [RelayCommand]
    private void Reset()
    {
        if (!IsActive)
        {
            return;
        }

        // A click while the focus is in the values: back to the categories first, so no press goes to hidden values.
        LeaveValues();
        WasReset = true;
        Chosen = GameFilter.None;
        Recount();
        // The row can't be chosen without a filter: the focus goes on to the first category.
        FocusCategoryAt(0);
    }

    private void HandleInCategories(UiAction action)
    {
        var rows = CategoryRowCount;
        var index = IsResetFocused ? 0 : Categories.ToList().IndexOf(Current) + (IsActive ? 1 : 0);
        switch (action)
        {
            case UiAction.Up:
                FocusCategoryAt(Math.Max(index - 1, 0));
                break;
            case UiAction.Down:
                FocusCategoryAt(Math.Min(index + 1, rows - 1));
                break;
            case UiAction.Accept when IsResetFocused:
                Reset();
                break;
            case UiAction.Right when IsResetFocused:
                // The reset row has no values to go into.
                break;
            case UiAction.Right or UiAction.Accept:
                EnterValues();
                break;
            case UiAction.Back:
                _close(this);
                break;
        }
    }

    private void HandleInValues(UiAction action)
    {
        var columns = ShowsTypes ? TypeColumns : 1;
        switch (action)
        {
            case UiAction.Up:
                FocusValue(_focusedValue - columns >= 0 ? _focusedValue - columns : _focusedValue);
                break;
            case UiAction.Down:
                // From a full row onto a shorter last one: the last value, as in the library's grid.
                FocusValue(_focusedValue + columns < Values.Count ? _focusedValue + columns
                    : _focusedValue / columns < (Values.Count - 1) / columns ? Values.Count - 1 : _focusedValue);
                break;
            case UiAction.Left when _focusedValue % columns == 0:
            case UiAction.Back:
                LeaveValues();
                break;
            case UiAction.Left:
                FocusValue(_focusedValue - 1);
                break;
            case UiAction.Right when columns > 1 && _focusedValue % columns < columns - 1 && _focusedValue + 1 < Values.Count:
                FocusValue(_focusedValue + 1);
                break;
            case UiAction.Accept:
                Choose(Values[_focusedValue]);
                break;
        }
    }

    // "Alle Filter zurücksetzen" is a row only while something is chosen.
    private int CategoryRowCount => Categories.Count + (IsActive ? 1 : 0);

    private void FocusCategoryAt(int row)
    {
        if (IsActive && row == 0)
        {
            SetCategoryFocus(null);
            IsResetFocused = true;
            return;
        }

        var category = Categories[row - (IsActive ? 1 : 0)];
        ShowCategory(category);
        FocusCategoryRow(category);
    }

    private void FocusCategoryRow(FilterCategoryViewModel category)
    {
        IsResetFocused = false;
        SetCategoryFocus(category);
    }

    private void SetCategoryFocus(FilterCategoryViewModel? focused)
    {
        foreach (var category in Categories)
        {
            category.IsFocused = category == focused;
        }
    }

    private void PickCategory(FilterCategoryViewModel category)
    {
        LeaveValues();
        ShowCategory(category);
        FocusCategoryRow(category);
    }

    private void EnterValues()
    {
        SetCategoryFocus(null);
        IsInValues = true;
        // A single choice starts on the chosen value, a multiple one on its first ("Alle Typen") (design T44Modal).
        FocusValue(ShowsRadios ? Math.Max(0, Values.ToList().FindIndex(value => value.IsSelected)) : 0);
    }

    private void LeaveValues()
    {
        if (!IsInValues)
        {
            return;
        }

        IsInValues = false;
        Values[_focusedValue].IsFocused = false;
        FocusCategoryRow(Current);
    }

    private void FocusValue(int index)
    {
        Values[_focusedValue].IsFocused = false;
        _focusedValue = index;
        Values[index].IsFocused = IsInValues;
    }

    private void Choose(FilterValueViewModel value)
    {
        Chosen = _choose[value](Chosen);
        Recount();
    }

    private void PickValue(FilterValueViewModel value)
    {
        if (!IsInValues)
        {
            IsResetFocused = false;
            SetCategoryFocus(null);
            IsInValues = true;
        }

        FocusValue(Values.ToList().IndexOf(value));
        Choose(value);
    }

    /// <summary>Shows the category's values on the right; the focus stays on the categories.</summary>
    private void ShowCategory(FilterCategoryViewModel category)
    {
        foreach (var row in Categories)
        {
            row.IsCurrent = row == category;
        }

        Current = category;
        _choose.Clear();
        _show.Clear();
        _focusedValue = 0;
        var values = new List<FilterValueViewModel>();
        if (category.Category == FilterCategory.Installed)
        {
            foreach (var (choice, label) in InstalledOptions)
            {
                Add(values, new FilterValueViewModel(label, null, PickValue), filter => filter.WithInstalled(choice),
                    (filter, counts) => (filter.Installed == choice, counts.Of(choice)));
            }
        }
        else
        {
            Add(values, new FilterValueViewModel(AllTypesText, null, PickValue), filter => filter.WithAllTypes(),
                (filter, counts) => (filter.Types.Count == 0, counts.WithInstalled));
            foreach (var type in _offer.Types)
            {
                Add(values, new FilterValueViewModel(GameTypeTexts.PluralName(type), GameTypeTexts.IconKey(type), PickValue),
                    filter => filter.WithType(type, !filter.Types.Contains(type)),
                    (filter, counts) => (filter.Types.Contains(type), counts.Of(type)));
            }
        }

        Values = values;
        Recount();
    }

    private void Add(
        List<FilterValueViewModel> values,
        FilterValueViewModel value,
        Func<GameFilter, GameFilter> choose,
        Func<GameFilter, FilterCounts, (bool Selected, int Count)> show)
    {
        values.Add(value);
        _choose[value] = choose;
        _show[value] = show;
    }

    /// <summary>The hits on top, the counts beside the values and every category's summary follow the choice.</summary>
    private void Recount()
    {
        var counts = Chosen.Count(_games);
        HitsText = Formatting.Count(counts.Hits);
        TotalText = AppTexts.FilterOfTotal(Formatting.Count(counts.Total));
        foreach (var value in Values)
        {
            var (selected, count) = _show[value](Chosen, counts);
            value.IsSelected = selected;
            value.Count = count;
            value.CountText = Formatting.Count(count);
        }

        foreach (var category in Categories)
        {
            Summarize(category);
        }
    }

    private void Summarize(FilterCategoryViewModel category)
    {
        if (category.Category == FilterCategory.Installed)
        {
            category.Summary = category.SummaryFallback = InstalledOptions.First(option => option.Choice == Chosen.Installed).Label;
            category.IsChosen = Chosen.Installed != InstalledChoice.All;
            return;
        }

        var types = Chosen.Types;
        category.Summary = types.Count == 0 ? AllTypesText : string.Join(", ", types.Select(GameTypeTexts.PluralName));
        category.SummaryFallback = types.Count > 1 ? FilterCategoryViewModel.SeveralText : category.Summary;
        category.IsChosen = types.Count > 0;
    }
}
