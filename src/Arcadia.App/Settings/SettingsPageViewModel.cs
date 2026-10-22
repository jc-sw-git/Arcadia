using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// The settings page (#15): a whole page in place of the library, opened with ≡, Enter or the chip; B or ✕ go back to
/// the same place of the library. Its categories are tabs in the header (LB/RB, Q/E), each a column of rows with an
/// info panel beside it (pattern "Leiste"). Every change applies at once.
/// </summary>
public sealed partial class SettingsPageViewModel : ViewModelBase, IDisposable
{
    public SettingsPageViewModel(IReadOnlyList<SettingsCategoryViewModel> categories, SettingsChanges changes, SettingsCategoryViewModel? first = null)
    {
        Categories = categories;
        Changes = changes;
        foreach (var category in categories)
        {
            category.SelectCommand = new RelayCommand(() => Select(category));
            category.FocusMoved += OnFocusMoved;
            category.PropertyChanged += OnCategoryChanged;
        }

        Select(categories.FirstOrDefault(c => c.Dialog is not null) ?? first ?? categories[0]);
    }

    public IReadOnlyList<SettingsCategoryViewModel> Categories { get; }

    /// <summary>LB/RB only switch between several categories, so the footer names them only then.</summary>
    public bool HasSeveralCategories => Categories.Count > 1;

    public SettingsChanges Changes { get; }

    [ObservableProperty]
    public partial SettingsCategoryViewModel Selected { get; private set; } = null!;

    /// <summary>The focused row of the shown category; the footer and the info panel follow it.</summary>
    [ObservableProperty]
    public partial SettingsRowViewModel? FocusedRow { get; private set; }

    /// <summary>Set by the view: the info panel has more than it shows, so the footer names RS.</summary>
    [ObservableProperty]
    public partial bool InfoOverflows { get; set; }

    /// <summary>Another row or category is shown: the view brings the row into sight and the info panel back to its top.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>RS (or Shift+W/S) scrolls the info panel: -1 up, 1 down.</summary>
    public event EventHandler<int>? InfoScrollRequested;

    /// <returns>False if the action closes the page.</returns>
    public bool Handle(UiAction action)
    {
        var category = Selected;
        if (category.Dialog is not null)
        {
            category.HandleDialog(action);
            return true;
        }

        // While Steam or SISR start, B still leaves; another category may be looked at meanwhile.
        if (category.IsLocked && action is not (UiAction.Back or UiAction.Settings or UiAction.PreviousTab or UiAction.NextTab))
        {
            return true;
        }

        switch (action)
        {
            case UiAction.Up:
                category.MoveFocus(-1);
                break;
            case UiAction.Down:
                category.MoveFocus(1);
                break;
            case UiAction.Left when FocusedRow is { CanChange: true } row:
                row.Change(-1);
                break;
            case UiAction.Right when FocusedRow is { CanChange: true } row:
                row.Change(1);
                break;
            case UiAction.Accept when FocusedRow is { CanActivate: true } row:
                row.Activate();
                break;
            case UiAction.PreviousTab:
                Cycle(-1);
                break;
            case UiAction.NextTab:
                Cycle(1);
                break;
            case UiAction.SecondaryUp:
                InfoScrollRequested?.Invoke(this, -1);
                break;
            case UiAction.SecondaryDown:
                InfoScrollRequested?.Invoke(this, 1);
                break;
            case UiAction.Back:
                return category.Back();
            case UiAction.Settings:
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var category in Categories)
        {
            category.FocusMoved -= OnFocusMoved;
            category.PropertyChanged -= OnCategoryChanged;
            category.Dispose();
        }
    }

    /// <summary>The LB button beside the categories.</summary>
    [RelayCommand]
    private void PreviousCategory() => Cycle(-1);

    /// <summary>The RB button beside the categories.</summary>
    [RelayCommand]
    private void NextCategory() => Cycle(1);

    private void Cycle(int delta) => Select(Categories[ListFocus.Cycle(Categories.ToList().IndexOf(Selected), delta, Categories.Count)]);

    private void Select(SettingsCategoryViewModel category)
    {
        if (Selected == category)
        {
            return;
        }

        if (Selected is { } previous)
        {
            previous.IsSelected = false;
        }

        Selected = category;
        category.IsSelected = true;
        FocusedRow = category.FocusedRow;
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    private void OnFocusMoved(object? sender, EventArgs e)
    {
        if (sender == Selected)
        {
            FocusedRow = Selected.FocusedRow;
            FocusMoved?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCategoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A dialog belongs to its category (e.g. a failed Steam start shown when the page opens): it is shown over it.
        if (e.PropertyName == nameof(SettingsCategoryViewModel.Dialog) && sender is SettingsCategoryViewModel { Dialog: not null } category)
        {
            Select(category);
        }
    }
}
