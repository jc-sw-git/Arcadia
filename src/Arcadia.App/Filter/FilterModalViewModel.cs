using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>The on-screen keyboard the value search takes while it is typed (#466): the settings' shared one.</summary>
public sealed record FilterKeyboard(Func<IKeyboardTarget, OnScreenKeyboardViewModel> Take, Action<IKeyboardTarget> GiveBack);

/// <summary>
/// "Filter" (#44, #466, designs T44F02–T44F09, T44Modal): the tab's categories on the left, the values of the one in focus
/// on the right (<see cref="Pane"/>), on top "Mit dieser Auswahl N von M Einträgen". ▲ ▼ move, ▶ or A go into the values,
/// ◀ at their left edge and B come back; B on the categories, R3 and the ✕ close it, and only then the choice applies (no
/// cancel: "Alle Filter zurücksetzen" is there). It counts the entries and metadata the tab held when it opened (#44 case
/// 18, #466 case 16), off the UI thread, so a press in a tab of thousands of entries and tags never waits for the counts.
/// </summary>
public sealed partial class FilterModalViewModel : ViewModelBase, IFilterPaneHost
{
    public static string Title => AppTexts.FilterTitle;

    public static string ResetText => AppTexts.FilterResetAll;

    public static string AllTypesText => AppTexts.FilterAllTypes;

    /// <summary>The type values stand in a grid of this many columns (design T44Modal).</summary>
    public const int TypeColumns = FilterOptionsPane.TypeColumns;

    private static readonly (FilterCategory Category, string IconKey)[] CategoryIcons =
    [
        (FilterCategory.Installed, "IconInstalled"),
        (FilterCategory.Type, "IconTypeGame"),
        (FilterCategory.Genre, "IconFilterGenre"),
        (FilterCategory.Tag, "IconFilterTag"),
        (FilterCategory.Developer, "IconFilterDeveloper"),
        (FilterCategory.Publisher, "IconFilterPublisher"),
        (FilterCategory.Year, "IconFilterYear"),
        (FilterCategory.AgeRating, "IconFilterAgeRating"),
    ];

    private readonly Action<FilterModalViewModel> _close;
    private readonly FilterKeyboard? _keyboard;
    private readonly Dictionary<FilterCategory, FilterPane> _panes = [];
    private int _countRun;

    /// <param name="close">The modal closes; the library applies <see cref="Chosen"/>.</param>
    /// <param name="keyboard">The on-screen keyboard of the value search; null where nothing can be typed.</param>
    public FilterModalViewModel(FilterModalSetup setup, GameFilter current, Action<FilterModalViewModel> close, FilterKeyboard? keyboard = null)
    {
        Setup = setup;
        LauncherName = setup.LauncherName;
        IsMetadataLoading = setup.MetadataLoading;
        _close = close;
        _keyboard = keyboard;
        Chosen = current;
        var categories = new List<FilterCategoryViewModel>();
        foreach (var (category, icon) in CategoryIcons.Where(entry => setup.Offer.Has(entry.Category)))
        {
            _panes[category] = category switch
            {
                FilterCategory.Installed or FilterCategory.Type => new FilterOptionsPane(category, this),
                FilterCategory.Year => new FilterYearPane(this),
                FilterCategory.AgeRating => new FilterRatingPane(this),
                _ => new FilterListPane(category, this),
            };
            categories.Add(new FilterCategoryViewModel(category, LabelOf(category), icon, PickCategory));
        }

        foreach (var list in _panes.Values.OfType<FilterListPane>())
        {
            list.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FilterListPane.Editor))
                {
                    HintsChanged();
                }
            };
        }

        Categories = categories;
        // The counts at the opening, at once: the first look is never empty.
        Show(current.Count(setup.Games, setup.Facts));
        Current = categories[0];
        ShowCategory(categories[0]);
        FocusCategoryRow(categories[0]);
    }

    public FilterModalSetup Setup { get; }

    public string LauncherName { get; }

    public IReadOnlyList<FilterCategoryViewModel> Categories { get; }

    /// <summary>The category whose values show on the right: the one in focus, or the one the focus went into.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pane), nameof(OptionsPane), nameof(ListPane), nameof(YearPane), nameof(RatingPane), nameof(Values),
        nameof(ShowsRadios), nameof(ShowsTypes), nameof(MovesInGrid), nameof(AcceptText), nameof(MoveText), nameof(BackText),
        nameof(SearchText), nameof(PageText), nameof(OrderText), nameof(StepText), nameof(StepTenText))]
    public partial FilterCategoryViewModel Current { get; private set; }

    /// <summary>The values of <see cref="Current"/>.</summary>
    public FilterPane Pane => _panes[Current.Category];

    public FilterOptionsPane? OptionsPane => Pane as FilterOptionsPane;

    public FilterListPane? ListPane => Pane as FilterListPane;

    public FilterYearPane? YearPane => Pane as FilterYearPane;

    public FilterRatingPane? RatingPane => Pane as FilterRatingPane;

    /// <summary>The values of "Installiert" or "Typ"; empty for the other categories.</summary>
    public IReadOnlyList<FilterValueViewModel> Values => OptionsPane?.Values ?? [];

    /// <summary>"Installiert": one of three, one per line.</summary>
    public bool ShowsRadios => OptionsPane?.ShowsRadios == true;

    /// <summary>"Typ": several at once, in a grid of <see cref="TypeColumns"/>.</summary>
    public bool ShowsTypes => OptionsPane?.ShowsTypes == true;

    /// <summary>The choice so far; it applies when the modal closes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    public partial GameFilter Chosen { get; private set; }

    /// <summary>Something is chosen: "Alle Filter zurücksetzen" can be chosen, the hit count is in the accent colour.</summary>
    public bool IsActive => Chosen.IsActive;

    /// <summary>The focus is on the values, not on the categories.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveText), nameof(AcceptText), nameof(BackText), nameof(MovesInGrid), nameof(SearchText), nameof(PageText),
        nameof(OrderText), nameof(StepText), nameof(StepTenText))]
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

    /// <summary>A metadata run goes on (#466 case 16): on top "Metadaten werden noch geladen …"; the counts stay those of the opening.</summary>
    [ObservableProperty]
    public partial bool IsMetadataLoading { get; private set; }

    public static string MetadataLoadingText => AppTexts.FilterMetadataLoading;

    /// <summary>The value search being typed: the modal makes room for the on-screen keyboard below it.</summary>
    public TextEditorViewModel? Editor => ListPane?.Editor;

    public bool IsWriting => Pane.IsWriting;

    /// <summary>The latest choice's counts, made off the UI thread and shown on it; done once they show.</summary>
    public Task Counting { get; private set; } = Task.CompletedTask;

    public string MoveText => IsInValues ? Pane.MoveText : AppTexts.Category;

    /// <summary>The values move on all four directions.</summary>
    public bool MovesInGrid => IsInValues && Pane.MovesInGrid;

    public string AcceptText => !IsInValues ? (IsResetFocused ? AppTexts.FilterHintReset : AppTexts.FilterValues) : Pane.AcceptText;

    public string BackText => !IsInValues ? AppTexts.FilterHintCloseAndApply : ListPane?.BackText ?? AppTexts.Back;

    public string? SearchText => IsInValues ? Pane.SearchText : null;

    public string? PageText => IsInValues ? Pane.PageText : null;

    public string? OrderText => IsInValues ? Pane.OrderText : null;

    public string? StepText => IsInValues ? Pane.StepText : null;

    public string? StepTenText => IsInValues ? Pane.StepTenText : null;

    /// <summary>"Alle Filter zurücksetzen" was chosen: it clears what the tab doesn't show as well.</summary>
    public bool WasReset { get; private set; }

    /// <summary>A press while the modal is open; it takes every one.</summary>
    public void Handle(UiAction action)
    {
        if (action == UiAction.KeyboardLayout && !Pane.IsWriting)
        {
            // R3 closes and applies, wherever the focus is (#44); with the keyboard open it is its layout menu.
            _close(this);
            return;
        }

        if (IsInValues)
        {
            if (!Pane.Handle(action))
            {
                LeaveValues();
            }

            HintsChanged();
        }
        else
        {
            HandleInCategories(action);
        }
    }

    /// <summary>A button was let go: ends a held key of the value search's keyboard.</summary>
    public void Release(UiAction action) => Editor?.Release(action);

    public void CancelHeldButtons() => Editor?.CancelHeldButtons();

    /// <summary>A metadata run started or ended while the modal is open; the counts stay those of the opening.</summary>
    public void ShowMetadataLoading(bool loading) => IsMetadataLoading = loading;

    void IFilterPaneHost.Change(Func<GameFilter, GameFilter> change)
    {
        Chosen = change(Chosen);
        foreach (var category in Categories)
        {
            Summarize(category);
        }

        Recount();
    }

    TextEditorViewModel IFilterPaneHost.Write(string text, Action<string> typed, Action closed)
    {
        if (_keyboard is null)
        {
            throw new InvalidOperationException("The filter has no keyboard to type with."); // not a UI text (a wiring mistake)
        }

        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            text,
            written =>
            {
                // Called at each key: the list follows what is typed.
                if (editor is not null)
                {
                    typed(written);
                }

                return null;
            },
            _ => true,
            closed,
            _keyboard.Take,
            _keyboard.GiveBack);
        return editor;
    }

    void IFilterPaneHost.FocusValues(FilterPane pane)
    {
        if (!IsInValues && _panes.GetValueOrDefault(Current.Category) == pane)
        {
            IsResetFocused = false;
            SetCategoryFocus(null);
            IsInValues = true;
            pane.Enter();
        }
    }

    void IFilterPaneHost.HintsChanged() => HintsChanged();

    /// <summary>The ✕, a click beside the modal.</summary>
    [RelayCommand]
    private void Close() => _close(this);

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
        ((IFilterPaneHost)this).Change(_ => GameFilter.None);
        // The row can't be chosen without a filter: the focus goes on to the first category.
        FocusCategoryAt(0);
    }

    private void HintsChanged()
    {
        OnPropertyChanged(nameof(MoveText));
        OnPropertyChanged(nameof(AcceptText));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(MovesInGrid));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(OrderText));
        OnPropertyChanged(nameof(StepText));
        OnPropertyChanged(nameof(StepTenText));
        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(IsWriting));
    }

    private void HandleInCategories(UiAction action)
    {
        var rows = CategoryRowCount;
        var index = IsResetFocused ? 0 : Categories.ToList().IndexOf(Current) + (IsActive ? 1 : 0);
        switch (action)
        {
            case UiAction.Up:
                FocusCategoryAt(ListFocus.Cycle(index, -1, rows, HeldPress.IsRepeat));
                break;
            case UiAction.Down:
                FocusCategoryAt(ListFocus.Cycle(index, 1, rows, HeldPress.IsRepeat));
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
        Pane.Enter();
        HintsChanged();
    }

    private void LeaveValues()
    {
        if (!IsInValues)
        {
            return;
        }

        IsInValues = false;
        Pane.Leave();
        FocusCategoryRow(Current);
        HintsChanged();
    }

    /// <summary>Shows the category's values on the right; the focus stays on the categories.</summary>
    private void ShowCategory(FilterCategoryViewModel category)
    {
        foreach (var row in Categories)
        {
            row.IsCurrent = row == category;
        }

        Current = category;
    }

    /// <summary>
    /// The counts of the latest choice, made off the UI thread; a newer choice's counts replace older ones still running.
    /// The tab's entries and facts are those of the opening and never change, so the work reads them without a lock.
    /// </summary>
    private void Recount()
    {
        var run = ++_countRun;
        var chosen = Chosen;
        var shown = ShowWhenCountedAsync(Task.Run(() => chosen.Count(Setup.Games, Setup.Facts)), run);
        Counting = shown;
        shown.Forget(AppTexts.ErrorContextUi);
    }

    private async Task ShowWhenCountedAsync(Task<FilterCounts> counting, int run)
    {
        var counts = await counting;
        if (run == _countRun)
        {
            Show(counts);
        }
    }

    /// <summary>The hits on top, the counts beside the values and every category's summary follow the choice.</summary>
    private void Show(FilterCounts counts)
    {
        HitsText = Formatting.Count(counts.Hits);
        TotalText = AppTexts.FilterOfTotal(Formatting.Count(counts.Total));
        foreach (var pane in _panes.Values)
        {
            pane.Show(Chosen, counts);
        }

        foreach (var category in Categories)
        {
            Summarize(category);
        }
    }

    private void Summarize(FilterCategoryViewModel category)
    {
        var (summary, fallback, chosen) = _panes[category.Category].Summarize(Chosen);
        category.Summary = summary;
        category.SummaryFallback = fallback;
        category.IsChosen = chosen;
    }

    private static string LabelOf(FilterCategory category) => category switch
    {
        FilterCategory.Installed => AppTexts.Installed,
        FilterCategory.Type => AppTexts.FilterType,
        FilterCategory.Genre => AppTexts.FilterGenre,
        FilterCategory.Tag => AppTexts.FilterTags,
        FilterCategory.Developer => AppTexts.FilterDevelopers,
        FilterCategory.Publisher => AppTexts.FilterPublishers,
        FilterCategory.Year => AppTexts.FilterYear,
        _ => AppTexts.FilterAgeRating,
    };
}
