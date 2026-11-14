using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Launcher" (#136): which launchers are active, i.e. read, watched and shown as tabs, and in which order.
/// Above "Aktiv · erscheinen als Tabs" in tab order, below "Nicht aktiv", the installed ones first. ◀ ▶ switch a launcher
/// on or off, L3 lifts it to move it (<see cref="LauncherOrder"/>). X, or the row "Alle neu einlesen" below the launchers
/// (for the mouse), reads every launcher again. A or a click open a launcher's page (#137): its account
/// (<see cref="LauncherAccountRow"/>), "Allgemein" (<see cref="LauncherGeneralRows"/>), "Bibliothek"
/// (<see cref="LauncherLibraryRows"/>) and "Controller" (<see cref="LauncherControllerRows"/>); LT/RT turn to the
/// launcher before or after it in the list, B goes back to the list. Applies at once. This class routes the input and
/// puts the rows together; the parts do the rest.
/// </summary>
public sealed class LauncherSettingsViewModel : SettingsCategoryViewModel, ILauncherCategory, IDraggableRows
{
    private static string ActiveGroup => AppTexts.LauncherGroupActive;
    private static string InactiveGroup => AppTexts.LauncherGroupInactive;
    private static string DropGroup => AppTexts.LauncherGroupDrop;
    private static string ListL3Action => AppTexts.Move;

    private static string ListXAction => AppTexts.RescanAll;

    private readonly ILauncherSettingsHost _host;
    private readonly SettingsChanges _changes;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, LauncherRowViewModel> _rows = new(StringComparer.Ordinal);

    private readonly LauncherOrder _order;
    private readonly LauncherRowStates _states;
    private readonly LauncherPageHeadViewModel _head;
    private readonly LauncherAccountRow _account;
    private readonly LauncherGeneralRows _general;
    private readonly LauncherLibraryRows _library;
    private readonly LauncherControllerRows _controller;
    private readonly ActionRowViewModel _rescanAllRow;

    // The launcher whose page is shown (#137); null on the list.
    private string? _page;

    /// <param name="focus">The launcher to show first, e.g. from its empty tab's "Launcher-Einstellungen".</param>
    public LauncherSettingsViewModel(ILauncherSettingsHost host, SettingsChanges changes, TimeProvider time, string? focus = null)
        : base(AppTexts.LaunchersCategory, "M4.5 3h4A1.5 1.5 0 0 1 10 4.5v4A1.5 1.5 0 0 1 8.5 10h-4A1.5 1.5 0 0 1 3 8.5v-4A1.5 1.5 0 0 1 4.5 3z M15.5 3h4A1.5 1.5 0 0 1 21 4.5v4a1.5 1.5 0 0 1-1.5 1.5h-4A1.5 1.5 0 0 1 14 8.5v-4A1.5 1.5 0 0 1 15.5 3z M4.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4A1.5 1.5 0 0 1 8.5 21h-4A1.5 1.5 0 0 1 3 19.5v-4A1.5 1.5 0 0 1 4.5 14z M15.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4a1.5 1.5 0 0 1-1.5 1.5h-4a1.5 1.5 0 0 1-1.5-1.5v-4a1.5 1.5 0 0 1 1.5-1.5z")
    {
        _host = host;
        _changes = changes;
        _time = time;
        L3ActionText = ListL3Action;
        XActionText = ListXAction;
        FocusMoved += (_, _) => ShowL3Action();

        _order = new LauncherOrder(this);
        _states = new LauncherRowStates(this);
        foreach (var integration in host.Launchers.All)
        {
            var launcher = host.Launchers.Info(integration.Launcher.Id);
            _rows[launcher.Id] = new LauncherRowViewModel(launcher, host.Logos.BadgeOf(launcher), _order.SwitchTo, OpenPage);
        }

        _head = new LauncherPageHeadViewModel(Turn, () => Back());
        _account = new LauncherAccountRow(this);
        _general = new LauncherGeneralRows(this, on => _order.SwitchTo(_rows[_page!], on));
        _library = new LauncherLibraryRows(this, _order);
        _controller = new LauncherControllerRows(this);

        // X does the same; the row is there so that the mouse alone can do it too.
        _rescanAllRow = new ActionRowViewModel("launchers-rescan-all", AppTexts.RescanAll, RescanAll)
        {
            Group = LauncherLibraryRows.LibraryGroup,
            ActionText = AppTexts.ScanNow,
            ShortcutPad = "X",
            ShortcutKeys = "F",
            Info =
            [
                new InfoText(AppTexts.RescanAllInfo),
            ],
        };

        // Pan is switched in the category "Controller": coming back here, the Steam Controller rows follow it.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsSelected) && IsSelected)
            {
                Refresh();
            }
        };

        Refresh();
        if (focus is not null && _rows.TryGetValue(focus, out var row))
        {
            Focus(row);
        }
    }

    /// <summary>The launcher whose page is shown, or null on the list.</summary>
    public string? PageLauncherId => _page;

    ILauncherSettingsHost ILauncherCategory.Host => _host;

    SettingsChanges ILauncherCategory.Changes => _changes;

    TimeProvider ILauncherCategory.Time => _time;

    bool ILauncherCategory.IsPicking { get; set; }

    bool ILauncherCategory.IsReordering
    {
        get => IsReordering;
        set => IsReordering = value;
    }

    object? ILauncherCategory.Dialog
    {
        get => Dialog;
        set => Dialog = value;
    }

    TextEditorViewModel? ILauncherCategory.Editor
    {
        get => Editor;
        set => Editor = value;
    }

    void ILauncherCategory.Focus(SettingsRowViewModel row) => Focus(row);

    /// <summary>The library was read again or its tabs changed: states, counts and order are shown anew.</summary>
    public void Refresh()
    {
        var (active, inactive) = _order.Arrangement();
        var tabs = active.Select(id => _rows[id]).ToList();
        foreach (var id in active.Concat(inactive))
        {
            _states.Show(_rows[id], tabs);
        }

        // A launcher without games is information, which the category's tab doesn't show (owner, 2026-10-24).
        Marker = _host.Launchers.ActiveIds.Any(id => _rows[id].Severity > HealthSeverity.Info) ? HealthSeverity.Warning : null;
        if (_page is { } page)
        {
            ShowPage(page, tabs);
            return;
        }

        foreach (var id in active)
        {
            _rows[id].Group = ActiveGroup;
        }

        foreach (var id in inactive)
        {
            _rows[id].Group = _order.Lifted is null ? InactiveGroup : DropGroup;
        }

        _rescanAllRow.IsEnabled = active.Count > 0;
        _rescanAllRow.Subtitle = active.Count > 0 ? AppTexts.RescanAllSubtitle : AppTexts.NoLauncherActive;
        SetRows([.. active.Select(id => _rows[id]), .. inactive.Select(id => _rows[id]), _rescanAllRow]);
    }

    /// <summary>A launcher's name, keyword or logo changed elsewhere: the rows show it.</summary>
    public void RefreshLooks()
    {
        foreach (var (id, row) in _rows)
        {
            var launcher = _host.Launchers.Info(id);
            row.ShowLooks(launcher, _host.Logos.BadgeOf(launcher));
        }

        Refresh();
    }

    public override bool Handle(UiAction action)
    {
        if (_page is not null)
        {
            switch (action)
            {
                case UiAction.PreviousPage:
                    Turn(-1);
                    return true;
                case UiAction.NextPage:
                    Turn(1);
                    return true;
                default:
                    // L3 and X have nothing to do on a launcher's page.
                    return action is UiAction.ViewOptions or UiAction.Search;
            }
        }

        if (_order.Lifted is not null)
        {
            switch (action)
            {
                case UiAction.Up:
                    _order.MoveLifted(-1);
                    break;
                case UiAction.Down:
                    _order.MoveLifted(1);
                    break;
                case UiAction.ViewOptions:
                    Drop();
                    break;
                case UiAction.Back:
                    Leave();
                    break;
            }

            // Anything else waits until the launcher is put down.
            return true;
        }

        switch (action)
        {
            case UiAction.ViewOptions:
                if (FocusedRow is LauncherRowViewModel row)
                {
                    _order.Lift(row);
                }

                return true;
            case UiAction.Search:
                RescanAll();
                return true;
            default:
                return false;
        }
    }

    /// <summary>L3 lifts only a launcher, so the footer names it only there, not on "Alle neu einlesen".</summary>
    private void ShowL3Action()
    {
        if (_page is null)
        {
            L3ActionText = FocusedRow is LauncherRowViewModel ? ListL3Action : null;
        }
    }

    private void RescanAll()
    {
        // A click on the row while a launcher is lifted waits until it is put down, as X does.
        if (_order.Lifted is null)
        {
            _host.RescanLaunchers();
        }
    }

    /// <summary>B on a launcher's page: back to the list, on that launcher.</summary>
    public override bool Back()
    {
        if (_page is not { } id)
        {
            return false;
        }

        // B drops a sign-in under way and stays on the page (owner, 2026-10-26).
        if (_account.PageAccount is { State.IsSigningIn: true } account)
        {
            account.CancelSignIn();
            return true;
        }

        ClosePage();
        Focus(_rows[id]);
        return true;
    }

    /// <summary>The tab "Launcher" is chosen again: the list, on the launcher whose page was open, else its first row.</summary>
    public override void ShowTopLevel()
    {
        if (_page is { } id)
        {
            ClosePage();
            Focus(_rows[id]);
        }
        else
        {
            base.ShowTopLevel();
        }
    }

    /// <summary>B while a launcher is lifted, or another category: it goes back to where it was; a text being written is dropped.</summary>
    public override void Leave()
    {
        Editor?.Drop();
        _account.PageAccount?.CancelSignIn();
        _order.PutBack();
    }

    public bool CanDrag(SettingsRowViewModel row) => row is LauncherRowViewModel { CanMove: true };

    public bool IsInDragList(SettingsRowViewModel row) => row is LauncherRowViewModel;

    /// <summary>The mouse starts dragging <paramref name="row"/>: lifted as with L3.</summary>
    public void BeginDrag(SettingsRowViewModel row)
    {
        if (_order.Lifted is null && row is LauncherRowViewModel launcher)
        {
            _order.Lift(launcher);
        }
    }

    /// <summary>The dragged launcher is over the place below <paramref name="rowsAbove"/> of the other rows.</summary>
    public void DragTo(int rowsAbove) => _order.DragTo(rowsAbove);

    /// <summary>L3, or the mouse let go: the lifted launcher stays where it stands; among the inactive ones it is off.</summary>
    public void Drop() => _order.Drop();

    /// <summary>Opens a launcher's page with the focus on one of its rows, e.g. from the controller settings.</summary>
    public void ShowPage(string launcherId, string rowKey)
    {
        if (_rows.TryGetValue(launcherId, out var row))
        {
            OpenPage(row);
            FocusRow(rowKey);
        }
    }

    /// <summary>A or a click on a launcher in the list: its page, the focus on its first row.</summary>
    private void OpenPage(LauncherRowViewModel row)
    {
        if (IsReordering)
        {
            return;
        }

        Focus(row);
        _page = row.Launcher.Id;
        L3ActionText = null;
        XActionText = null;
        Head = _head;
        TurnText = AppTexts.Launcher;
        Refresh();
        Focus(_general.ActiveRow);
    }

    private void ClosePage()
    {
        Editor?.Drop();
        _account.PageAccount?.CancelSignIn();
        _page = null;
        Head = null;
        TurnText = null;
        XActionText = ListXAction;
        Refresh();
        ShowL3Action();
    }

    /// <summary>LT/RT or a click on a neighbour: the page of the launcher before or after this one in the list; the focus stays on its row.</summary>
    private void Turn(int delta)
    {
        if (_page is not { } id || Editor is not null)
        {
            return;
        }

        var order = _order.ListOrder();
        var next = order[ListFocus.Cycle(order.ToList().IndexOf(id), delta, order.Count, HeldPress.IsRepeat)];
        if (next != id)
        {
            _account.PageAccount?.CancelSignIn();
            _page = next;
            Refresh();
        }
    }

    private void ShowPage(string id, IReadOnlyList<LauncherRowViewModel> tabs)
    {
        var launchers = _host.Launchers;
        var row = _rows[id];
        var launcher = row.Launcher;
        var name = launcher.DisplayName;
        var isActive = tabs.Contains(row);
        var isFound = launchers.IsFound(id);
        ShowHead(row, tabs, isActive, isFound);

        var tabBar = new InfoTabBar([.. tabs.Select(tab => new InfoTab(tab.Launcher.DisplayName, tab.Badge, tab == row))]);
        var library = isActive ? _host.LibraryOf(id) : null;
        var account = launchers.Registered(id)?.Account;
        if (account is not null)
        {
            _account.Show(account, name, isActive, isFound);
        }

        SetRows(
        [
            .. account is null ? Array.Empty<SettingsRowViewModel>() : [_account.Row],
            .. _general.Show(launcher, isActive, isFound, tabBar),
            .. _library.Show(id, name, isActive, isFound, library),
            .. _controller.Show(id, name, launchers.Registered(id)!.Launcher.GamesUseSteamInput, library),
        ]);
    }

    /// <summary>The page's head: the launcher, its state and the neighbours LT/RT turn to.</summary>
    private void ShowHead(LauncherRowViewModel row, IReadOnlyList<LauncherRowViewModel> tabs, bool isActive, bool isFound)
    {
        _head.Badge = row.Badge;
        _head.Name = row.Launcher.DisplayName;
        _head.IsActive = isActive;
        _head.StateText = isActive ? AppTexts.LauncherActiveTab(tabs.ToList().IndexOf(row) + 1) : isFound ? AppTexts.NotActive : AppTexts.NotInstalled;
        var order = _order.ListOrder();
        var place = order.ToList().IndexOf(row.Launcher.Id);
        LauncherNeighbour? Neighbour(int delta) => order.Count < 2 ? null
            : _rows[order[ListFocus.Cycle(place, delta, order.Count)]] is var other ? new LauncherNeighbour(other.Launcher.DisplayName, other.Badge) : null;
        _head.Previous = Neighbour(-1);
        _head.Next = Neighbour(1);
    }

    /// <summary>
    /// The question before signing out: ◀ ▶ choose, A answers, B keeps the sign-in. The one before resetting the games'
    /// modes (#178): A resets, B keeps them.
    /// </summary>
    public override void HandleDialog(UiAction action)
    {
        switch (Dialog)
        {
            case ConfirmationViewModel confirmation:
                var answer = confirmation.Answer(action);
                if (answer != ConfirmationAnswer.Open)
                {
                    // Closed first: the confirmed action may open the next dialog.
                    Dialog = null;
                    if (answer == ConfirmationAnswer.Confirmed)
                    {
                        confirmation.Confirm();
                    }
                }

                break;
            case SettingsQuestionViewModel question:
                question.Handle(action);
                break;
        }
    }

    public override void Dispose()
    {
        _account.Dispose();
        base.Dispose();
    }
}
