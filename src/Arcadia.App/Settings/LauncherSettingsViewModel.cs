using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Search;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Launcher" (#136): which launchers are active, i.e. read, watched and shown as tabs, and in which order.
/// Above "Aktiv · erscheinen als Tabs" in tab order, below "Nicht aktiv", the installed ones first. ◀ ▶ switch a launcher
/// on (it goes to the end of the active ones) or off (to the top of the inactive ones); Y lifts it, ▲ ▼ move it, Y puts it
/// down and B cancels; put down among the inactive ones it is off. X reads every launcher again. Applies at once.
/// </summary>
public sealed class LauncherSettingsViewModel : SettingsCategoryViewModel
{
    private const string ActiveGroup = "AKTIV · ERSCHEINEN ALS TABS";
    private const string InactiveGroup = "NICHT AKTIV";
    private const string DropGroup = "NICHT AKTIV · HIERHER VERSCHIEBEN SCHALTET IHN AB";

    private readonly ILauncherSettingsHost _host;
    private readonly SettingsChanges _changes;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, LauncherRowViewModel> _rows = new(StringComparer.Ordinal);

    // Switched off while the page is open, the latest first: each goes to the top of the inactive ones (owner, 2026-10-22).
    private readonly List<string> _switchedOff = [];

    private LauncherRowViewModel? _lifted;

    // Where the lifted launcher stands: a place among the other active ones, or one more for the top of the inactive ones.
    private int _slot;

    /// <param name="focus">The launcher to show first, e.g. from its empty tab's "Launcher-Einstellungen".</param>
    public LauncherSettingsViewModel(ILauncherSettingsHost host, SettingsChanges changes, TimeProvider time, string? focus = null)
        : base("Launcher", "M4.5 3h4A1.5 1.5 0 0 1 10 4.5v4A1.5 1.5 0 0 1 8.5 10h-4A1.5 1.5 0 0 1 3 8.5v-4A1.5 1.5 0 0 1 4.5 3z M15.5 3h4A1.5 1.5 0 0 1 21 4.5v4a1.5 1.5 0 0 1-1.5 1.5h-4A1.5 1.5 0 0 1 14 8.5v-4A1.5 1.5 0 0 1 15.5 3z M4.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4A1.5 1.5 0 0 1 8.5 21h-4A1.5 1.5 0 0 1 3 19.5v-4A1.5 1.5 0 0 1 4.5 14z M15.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4a1.5 1.5 0 0 1-1.5 1.5h-4a1.5 1.5 0 0 1-1.5-1.5v-4a1.5 1.5 0 0 1 1.5-1.5z")
    {
        _host = host;
        _changes = changes;
        _time = time;
        YActionText = "Verschieben";
        XActionText = "Alle neu einlesen";
        foreach (var integration in host.Launchers.All)
        {
            _rows[integration.Launcher.Id] = new LauncherRowViewModel(integration.Launcher, SwitchTo);
        }

        Refresh();
        if (focus is not null && _rows.TryGetValue(focus, out var row))
        {
            Focus(row);
        }
    }

    /// <summary>The library was read again or its tabs changed: states, counts and order are shown anew.</summary>
    public void Refresh()
    {
        var (active, inactive) = Arrangement();
        var tabs = active.Select(id => _rows[id]).ToList();
        foreach (var id in active.Concat(inactive))
        {
            ShowState(_rows[id], tabs);
        }

        foreach (var id in active)
        {
            _rows[id].Group = ActiveGroup;
        }

        foreach (var id in inactive)
        {
            _rows[id].Group = _lifted is null ? InactiveGroup : DropGroup;
        }

        SetRows([.. active.Select(id => _rows[id]), .. inactive.Select(id => _rows[id])]);
        Marker = _host.Launchers.ActiveIds.Any(id => _rows[id].HasSeverity) ? HealthSeverity.Warning : null;
    }

    public override bool Handle(UiAction action)
    {
        if (_lifted is not null)
        {
            switch (action)
            {
                case UiAction.Up:
                    MoveLifted(-1);
                    break;
                case UiAction.Down:
                    MoveLifted(1);
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
                    Lift(row);
                }

                return true;
            case UiAction.Search:
                _host.RescanLaunchers();
                return true;
            default:
                return false;
        }
    }

    /// <summary>B while a launcher is lifted, or another category: it goes back to where it was.</summary>
    public override void Leave()
    {
        if (_lifted is { } lifted)
        {
            lifted.IsLifted = false;
            _lifted = null;
            IsReordering = false;
            Refresh();
        }
    }

    /// <summary>The mouse starts dragging <paramref name="row"/>: lifted as with Y.</summary>
    public void BeginDrag(LauncherRowViewModel row)
    {
        if (_lifted is null)
        {
            Lift(row);
        }
    }

    /// <summary>The dragged launcher is over the place below <paramref name="rowsAbove"/> of the other rows.</summary>
    public void DragTo(int rowsAbove)
    {
        if (_lifted is not null)
        {
            var activeOthers = Arrangement().Active.Count(id => id != _lifted.Launcher.Id);
            PlaceLifted(Math.Min(rowsAbove, activeOthers + 1));
        }
    }

    /// <summary>Y, or the mouse let go: the lifted launcher stays where it stands; among the inactive ones it is off.</summary>
    public void Drop()
    {
        if (_lifted is not { } lifted)
        {
            return;
        }

        var id = lifted.Launcher.Id;
        var active = _host.Launchers.ActiveIds;
        var others = active.Where(other => other != id).ToList();
        var switchedOff = _slot > others.Count;
        var chosen = LauncherSelection.Placed(others, id, switchedOff ? null : _slot);
        lifted.IsLifted = false;
        _lifted = null;
        IsReordering = false;
        if (chosen.SequenceEqual(active))
        {
            Refresh();
            return;
        }

        Choose(chosen, switchedOff && active.Contains(id) ? id : null);
    }

    private void Lift(LauncherRowViewModel row)
    {
        if (!row.CanMove)
        {
            return;
        }

        Focus(row);
        var active = _host.Launchers.ActiveIds;
        var index = active.ToList().IndexOf(row.Launcher.Id);
        _slot = index >= 0 ? index : active.Count + 1;
        _lifted = row;
        row.IsLifted = true;
        IsReordering = true;
        Refresh();
    }

    private void MoveLifted(int delta) => PlaceLifted(_slot + delta);

    private void PlaceLifted(int slot)
    {
        if (_lifted is null)
        {
            return;
        }

        var activeOthers = _host.Launchers.ActiveIds.Count(id => id != _lifted.Launcher.Id);
        slot = Math.Clamp(slot, 0, activeOthers + 1);
        if (slot != _slot)
        {
            _slot = slot;
            Refresh();
        }
    }

    /// <summary>◀ ▶ on a row, or its arrows: on goes to the end of the active ones, off to the top of the inactive ones.</summary>
    private void SwitchTo(LauncherRowViewModel row, bool on)
    {
        var active = _host.Launchers.ActiveIds;
        var id = row.Launcher.Id;
        Choose(LauncherSelection.Placed(active, id, on ? active.Count : null), on ? null : id);
    }

    private void Choose(IReadOnlyList<string> active, string? switchedOff)
    {
        _switchedOff.RemoveAll(active.Contains);
        if (switchedOff is not null)
        {
            _switchedOff.Remove(switchedOff);
            _switchedOff.Insert(0, switchedOff);
        }

        _changes.Apply(settings => settings with { ActiveLaunchers = active }, "Launcher");
        AppLog.Write($"Aktive Launcher: {string.Join(", ", active)}");
        _host.LauncherChoiceChanged();
        Refresh();
    }

    /// <summary>The rows as shown: the active ones in tab order and the inactive ones, with a lifted one where it stands now.</summary>
    private (IReadOnlyList<string> Active, IReadOnlyList<string> Inactive) Arrangement()
    {
        var launchers = _host.Launchers;
        var active = launchers.ActiveIds;
        // Still those found first; within each, the ones switched off last at the top.
        IReadOnlyList<string> inactive = launchers.InactiveIds
            .OrderBy(id => launchers.IsFound(id) ? 0 : 1)
            .ThenBy(id => _switchedOff.IndexOf(id) is >= 0 and var index ? index : int.MaxValue)
            .ToList();
        if (_lifted is null)
        {
            return (active, inactive);
        }

        var lifted = _lifted.Launcher.Id;
        var others = active.Where(id => id != lifted).ToList();
        var rest = inactive.Where(id => id != lifted).ToList();
        return _slot > others.Count
            ? (others, [lifted, .. rest])
            : (LauncherSelection.Placed(others, lifted, _slot), rest);
    }

    private void ShowState(LauncherRowViewModel row, IReadOnlyList<LauncherRowViewModel> tabs)
    {
        var launchers = _host.Launchers;
        var id = row.Launcher.Id;
        var name = row.Launcher.DisplayName;
        var place = tabs.ToList().IndexOf(row);
        var isActive = place >= 0;
        var isFound = launchers.IsFound(id);
        var library = isActive ? _host.LibraryOf(id) : null;
        var keyword = $"{SearchQuery.PrefixMarker}{row.Launcher.SearchKeyword}";

        string? warning = null;
        string? note = null;
        if (isActive && !isFound)
        {
            warning = "Nicht gefunden";
            note = $"Arcadia findet {name} auf diesem PC nicht mehr. Ist es deinstalliert, schalte den Launcher hier ab, dann verschwindet sein Tab.";
        }
        else if (library is { Games.Count: 0 })
        {
            warning = "Keine Spiele gefunden";
            note = $"{name} ist installiert, aber Arcadia hat dort keine Spiele gefunden. Sobald du dort ein Spiel kaufst oder installierst, erscheint es im Tab.";
        }

        var status = (isActive, isFound, library) switch
        {
            (true, _, { } shown) => $"{GameCount(shown.Games.Count)} · {keyword}",
            (true, _, null) => "Wird eingelesen …",
            (false, true, _) => "Installiert",
            _ => "Nicht gefunden",
        };

        var tabNumber = isActive ? place + 1 : (int?)null;
        var installed = library?.Games.Count(g => g.IsInstalled) ?? 0;
        var integration = launchers.Registered(id);
        var location = launchers.LocationOf(id);
        List<InfoBlock> info = [new InfoTabBar([.. tabs.Select(tab => new InfoTab(tab.Launcher.DisplayName, tab.Monogram, tab.TileBrush, tab.Launcher.Id == _host.SelectedLauncherId, tab == row))])];
        if (note is not null)
        {
            info.Add(new InfoNote(note));
        }

        info.Add(new InfoLauncher(name, row.Monogram, row.TileBrush, isActive ? $"Aktiv · Tab {tabNumber}" : isFound ? "Nicht aktiv" : "Nicht installiert", isActive));
        info.Add(new InfoFacts("ALLGEMEIN",
        [
            new InfoFact("Aktiv", isActive ? "An" : "Aus", IsMuted: !isActive, IsGood: isActive),
            new InfoFact("Tab", tabNumber is { } number ? number <= 10 ? $"{number} · Taste {number % 10}" : $"{number}" : "–", IsMuted: !isActive),
            new InfoFact("Name im Tab", name),
            new InfoFact("Such-Kürzel", keyword),
            new InfoFact("Logo", "Standard"),
        ]));
        var scannedAt = _host.ScannedAt(id);
        info.Add(new InfoFacts("BIBLIOTHEK",
        [
            library is null
                ? new InfoFact("Spiele", isActive ? "werden eingelesen" : "nicht eingelesen", IsMuted: true)
                : new InfoFact("Spiele", library.Games.Count == installed ? GameCount(installed) : $"{GameCount(library.Games.Count)} · {installed} installiert"),
            new InfoFact("Zuletzt eingelesen", Ago(scannedAt), IsMuted: scannedAt is null),
        ]));
        info.Add(new InfoFacts("ORT",
        [
            new InfoFact("Gefunden unter", location ?? "Nicht gefunden", IsMuted: location is null),
            new InfoFact("Installationen", (isActive, integration?.InstallMonitor) switch
            {
                (false, _) => "nicht überwacht",
                (true, null) => "nur aus Arcadia gestartete",
                _ => "werden überwacht",
            }, IsMuted: !isActive, IsGood: isActive && integration?.InstallMonitor is not null),
            new InfoFact("Laufende Spiele", (isActive, integration?.RunningGames) switch
            {
                (false, _) => "nicht überwacht",
                (true, null) => "an ihren Prozessen erkannt",
                _ => "meldet der Launcher",
            }, IsMuted: !isActive, IsGood: isActive),
        ]));

        row.Show(isActive, isFound, tabNumber, status, warning, info);
    }

    private static string GameCount(int count) => count == 1 ? "1 Spiel" : $"{count} Spiele";

    private string Ago(DateTimeOffset? at)
    {
        if (at is not { } time)
        {
            return "–";
        }

        var age = _time.GetUtcNow() - time;
        return age < TimeSpan.FromMinutes(1) ? "gerade eben"
            : age < TimeSpan.FromHours(1) ? $"vor {(int)age.TotalMinutes} Min."
            : $"um {time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)} Uhr";
    }
}
