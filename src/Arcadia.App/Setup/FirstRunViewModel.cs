using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Setup;

/// <summary>
/// The first start's page "Welche Launcher soll Arcadia nutzen?" (#139) in place of the library, pattern "Leiste" (#15):
/// under "Gefunden" the launchers found on this PC, switched on, under "Nicht gefunden" the others, greyed out. ▲ ▼ move,
/// ◀ ▶ (and A) switch; the info panel explains the focused launcher and sums up which become tabs. ≡, Enter or "Fertig"
/// end it; B does nothing, there is nothing to go back to. Only which launchers are active is chosen here; their order,
/// names and logos are in the settings.
/// </summary>
public sealed partial class FirstRunViewModel : ViewModelBase, IPage
{
    private readonly LauncherRegistry _launchers;
    private readonly Action<IReadOnlyList<string>> _done;
    private readonly Dictionary<string, FirstRunLauncherViewModel> _rows = new(StringComparer.Ordinal);
    private int _focus;

    /// <param name="badgeOf">A launcher's logo, as its tab shows it.</param>
    /// <param name="done">"Fertig", with the launchers switched on in registered order.</param>
    public FirstRunViewModel(LauncherRegistry launchers, Func<LauncherInfo, LauncherBadge> badgeOf, Action<IReadOnlyList<string>> done)
    {
        _launchers = launchers;
        _done = done;
        foreach (var integration in launchers.All)
        {
            var launcher = launchers.Info(integration.Launcher.Id);
            _rows[launcher.Id] = new FirstRunLauncherViewModel(launcher, badgeOf(launcher), SwitchTo, Focus);
        }
    }

    /// <summary>The launchers found on this PC, in registered order.</summary>
    public ObservableCollection<FirstRunLauncherViewModel> Found { get; } = [];

    /// <summary>The launchers not found, greyed out.</summary>
    public ObservableCollection<FirstRunLauncherViewModel> Missing { get; } = [];

    public bool HasFound => Found.Count > 0;

    public bool HasMissing => Missing.Count > 0;

    /// <summary>Arcadia still looks for the launchers and reads their games; the page waits with a spinner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    public partial bool IsSearching { get; private set; } = true;

    public bool IsReady => !IsSearching;

    /// <summary>The launcher the info panel explains; null while searching.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocusedRow))]
    public partial FirstRunLauncherViewModel? FocusedRow { get; private set; }

    public bool HasFocusedRow => FocusedRow is not null;

    /// <summary>
    /// The tabs the launchers switched on will be, as the launcher settings preview them (owner, 2026-10-23), with the
    /// focused launcher framed.
    /// </summary>
    [ObservableProperty]
    public partial InfoTabBar TabBar { get; private set; } = new([]);

    public string TabBarTitle => LauncherSettingsViewModel.OrderPreviewTitle;

    public string TabBarSubtitle => LauncherSettingsViewModel.OrderPreviewSubtitle;

    /// <summary>Another launcher has the focus: the view brings it into sight.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>
    /// The launchers were looked for and read, the first time or again (e.g. Arcadia came back to the front): found ones
    /// show their games, and one found for the first time starts switched on, as at the start.
    /// </summary>
    public void Show(IReadOnlyList<LauncherLibrary> libraries)
    {
        var wasSearching = IsSearching;
        var ids = _launchers.All.Select(i => i.Launcher.Id).ToList();
        var found = FirstRunSetup.Preselected(ids, _launchers.IsFound);
        foreach (var id in ids)
        {
            var row = _rows[id];
            var isFound = found.Contains(id);
            var switchOn = isFound && (wasSearching || !row.IsFound);
            row.Show(isFound, _launchers.LocationOf(id), libraries.FirstOrDefault(l => l.Launcher.Id == id)?.Games);
            if (switchOn)
            {
                row.IsOn = true;
            }
        }

        var focused = FocusedRow;
        Replace(Found, found);
        Replace(Missing, ids.Where(id => !found.Contains(id)));
        OnPropertyChanged(nameof(HasFound));
        OnPropertyChanged(nameof(HasMissing));
        IsSearching = false;
        Numbered();
        FocusAt(Math.Max(0, focused is null ? 0 : Rows().ToList().IndexOf(focused)));
    }

    /// <summary>Takes every button: nothing else in the window reacts until "Fertig".</summary>
    public bool Handle(UiAction action)
    {
        if (IsSearching)
        {
            return true;
        }

        switch (action)
        {
            case UiAction.Up:
                FocusAt(ListFocus.Move(_focus, -1, Rows().Count()));
                break;
            case UiAction.Down:
                FocusAt(ListFocus.Move(_focus, 1, Rows().Count()));
                break;
            case UiAction.Left:
                FocusedRow?.Change(-1);
                break;
            case UiAction.Right:
                FocusedRow?.Change(1);
                break;
            case UiAction.Accept:
                FocusedRow?.Toggle();
                break;
            case UiAction.Settings:
                Finish();
                break;
        }

        return true;
    }

    public void Release(UiAction action)
    {
    }

    public void CancelHeldButtons()
    {
    }

    /// <summary>The first start shows no covers.</summary>
    public IEnumerable<GameTileViewModel> ShownTiles => [];

    /// <summary>It covers the whole window, so a dialog of the page under it never takes a press first.</summary>
    public bool CoversWindow => true;

    public void InputKindChanged(InputKind kind)
    {
    }

    /// <summary>"Fertig", ≡ or Enter: the launchers switched on become the tabs.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private void Finish()
    {
        if (IsReady)
        {
            _done(Found.Where(r => r.IsOn).Select(r => r.Launcher.Id).ToList());
        }
    }

    private IEnumerable<FirstRunLauncherViewModel> Rows() => Found.Concat(Missing);

    private void SwitchTo(FirstRunLauncherViewModel row, bool on)
    {
        Focus(row);
        row.IsOn = on;
        Numbered();
    }

    /// <summary>The tab each launcher switched on will be, and the preview of the tabs.</summary>
    private void Numbered()
    {
        var on = Found.Where(r => r.IsOn).ToList();
        foreach (var row in Rows())
        {
            row.TabNumber = on.IndexOf(row) + 1;
        }

        ShowTabBar();
    }

    private void ShowTabBar() =>
        TabBar = new InfoTabBar([.. Found.Where(r => r.IsOn).Select(r => new InfoTab(r.Name, r.Badge, r == FocusedRow))]);

    private void Focus(FirstRunLauncherViewModel row) => FocusAt(Rows().ToList().IndexOf(row));

    private void FocusAt(int index)
    {
        var row = Rows().ElementAtOrDefault(index);
        if (row == FocusedRow)
        {
            // The same launcher, perhaps in another place after a new search.
            _focus = index;
            return;
        }

        if (FocusedRow is { } previous)
        {
            previous.IsFocused = false;
        }

        _focus = index;
        FocusedRow = row;
        if (row is not null)
        {
            row.IsFocused = true;
        }

        ShowTabBar();
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    private void Replace(ObservableCollection<FirstRunLauncherViewModel> shown, IEnumerable<string> ids)
    {
        var rows = ids.Select(id => _rows[id]).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].StartsGroup = i == 0;
            rows[i].EndsGroup = i == rows.Count - 1;
        }

        if (shown.SequenceEqual(rows))
        {
            return;
        }

        shown.Clear();
        foreach (var row in rows)
        {
            shown.Add(row);
        }
    }
}
