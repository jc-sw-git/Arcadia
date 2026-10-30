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
public sealed partial class SettingsPageViewModel : ViewModelBase, IPage, IDisposable
{
    private readonly IPageHost _host;

    public SettingsPageViewModel(IReadOnlyList<SettingsCategoryViewModel> categories, SettingsChanges changes, IPageHost host, SettingsCategoryViewModel? first = null)
    {
        _host = host;
        Categories = categories;
        Changes = changes;
        foreach (var category in categories)
        {
            category.SelectCommand = new RelayCommand(() => ChooseTab(category));
            category.FocusMoved += OnFocusMoved;
            category.PropertyChanged += OnCategoryChanged;
        }

        var links = categories
            .SelectMany(target => target.LauncherPageLinks.Select(link =>
                (target.Name.ToUpperInvariant(), link, (Action)(() => OpenLink(target, link.TargetRowKey)))))
            .ToList();
        foreach (var launchers in categories.OfType<LauncherSettingsViewModel>())
        {
            launchers.ShowLinks(links);
        }

        Select(categories.FirstOrDefault(c => c.Dialog is not null) ?? first ?? categories[0]);
    }

    /// <summary>The category a link was followed from, which B goes back to; null when no link led here.</summary>
    private SettingsCategoryViewModel? _linkedFrom;

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

    /// <summary>Takes every button; B at the top and ≡ ask the window to close the page.</summary>
    public bool Handle(UiAction action)
    {
        var category = Selected;
        if (category.Dialog is not null)
        {
            category.HandleDialog(action);
            return true;
        }

        if (category.Editor is { } editor)
        {
            editor.Handle(action);
            return true;
        }

        if (category.Handle(action))
        {
            return true;
        }

        if (action != UiAction.Accept && FocusedRow is HoldRowViewModel held)
        {
            // Anything else while [A] is held breaks the hold off, e.g. the focus moving away.
            held.Hold.Cancel();
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
            case UiAction.Back when category.Back():
                break;
            case UiAction.Back when _linkedFrom is { } from:
                Select(from);
                break;
            case UiAction.Back or UiAction.Settings:
                _host.Close(this);
                break;
        }

        return true;
    }

    /// <summary>A button or key was let go: ends a held erase, shift or AltGr while a text is written, or a held row (#242).</summary>
    public void Release(UiAction action)
    {
        Selected.Editor?.Release(action);
        if (action == UiAction.Accept && FocusedRow is HoldRowViewModel held)
        {
            held.Hold.Release();
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHeldButtons()
    {
        Selected.Editor?.CancelHeldButtons();
        (FocusedRow as HoldRowViewModel)?.Hold.Cancel();
    }

    /// <summary>The settings show no covers.</summary>
    public IEnumerable<GameTileViewModel> ShownTiles => [];

    /// <summary>It covers the whole window, so a dialog of the page under it never takes a press first.</summary>
    public bool CoversWindow => true;

    /// <summary>The keyboard of a text being written names the controller's buttons or the keys (#137).</summary>
    public void InputKindChanged(InputKind kind)
    {
        if (Selected.Editor is { } editor)
        {
            editor.Keyboard.UsesController = kind == InputKind.Gamepad;
        }
    }

    public void Dispose()
    {
        foreach (var category in Categories)
        {
            category.Leave();
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

    private void Cycle(int delta) => ChooseTab(Categories[ListFocus.Cycle(Categories.ToList().IndexOf(Selected), delta, Categories.Count)]);

    /// <summary>
    /// A category's tab is chosen: it opens at its top level. A link and the way back of a link keep where it was, so
    /// B after a link lands on the launcher page it came from.
    /// </summary>
    private void ChooseTab(SettingsCategoryViewModel category)
    {
        if (category != Selected)
        {
            category.ShowTopLevel();
        }

        Select(category);
    }

    /// <summary>A link on a launcher's page (#169): its category with the row focused; B goes back to the page.</summary>
    private void OpenLink(SettingsCategoryViewModel target, string rowKey)
    {
        var from = Selected;
        Select(target);
        target.FocusRow(rowKey);
        _linkedFrom = from;
    }

    private void Select(SettingsCategoryViewModel category)
    {
        // Any other way to a category ends the way back of a link.
        _linkedFrom = null;
        if (Selected == category)
        {
            return;
        }

        if (Selected is { } previous)
        {
            previous.Leave();
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
