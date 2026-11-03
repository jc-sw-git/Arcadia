using Arcadia.App.Services;
using Arcadia.Core.Launchers;

namespace Arcadia.App.Settings;

/// <summary>
/// The order of the launcher list (#136): ◀ ▶ switch a launcher on (it goes to the end of the active ones) or off (to the
/// top of the inactive ones); Y or the mouse lift it, ▲ ▼ or dragging move it, Y or letting go put it down, B puts it back.
/// Put down among the inactive ones it is off.
/// </summary>
internal sealed class LauncherOrder(ILauncherCategory category)
{
    // Switched off while the page is open, the latest first: each goes to the top of the inactive ones (owner, 2026-10-22).
    private readonly List<string> _switchedOff = [];

    // Where the lifted launcher stands: a place among the other active ones, or one more for the top of the inactive ones.
    private int _slot;

    /// <summary>The launcher being moved, or null.</summary>
    public LauncherRowViewModel? Lifted { get; private set; }

    /// <summary>The rows as shown: the active ones in tab order and the inactive ones, with a lifted one where it stands now.</summary>
    public (IReadOnlyList<string> Active, IReadOnlyList<string> Inactive) Arrangement()
    {
        var launchers = category.Host.Launchers;
        var active = launchers.ActiveIds;
        // Still those found first; within each, the ones switched off last at the top.
        IReadOnlyList<string> inactive = launchers.InactiveIds
            .OrderBy(id => launchers.IsFound(id) ? 0 : 1)
            .ThenBy(id => _switchedOff.IndexOf(id) is >= 0 and var index ? index : int.MaxValue)
            .ToList();
        if (Lifted is null)
        {
            return (active, inactive);
        }

        var lifted = Lifted.Launcher.Id;
        var others = active.Where(id => id != lifted).ToList();
        var rest = inactive.Where(id => id != lifted).ToList();
        return _slot > others.Count
            ? (others, [lifted, .. rest])
            : (LauncherSelection.Placed(others, lifted, _slot), rest);
    }

    /// <summary>The launchers as the list shows them: the active ones in tab order, then the inactive ones.</summary>
    public IReadOnlyList<string> ListOrder()
    {
        var (active, inactive) = Arrangement();
        return [.. active, .. inactive];
    }

    public void Lift(LauncherRowViewModel row)
    {
        if (!row.CanMove)
        {
            return;
        }

        category.Focus(row);
        var active = category.Host.Launchers.ActiveIds;
        var index = active.ToList().IndexOf(row.Launcher.Id);
        _slot = index >= 0 ? index : active.Count + 1;
        Lifted = row;
        row.IsLifted = true;
        category.IsReordering = true;
        category.Refresh();
    }

    public void MoveLifted(int delta) => PlaceLifted(_slot + delta);

    /// <summary>The dragged launcher is over the place below <paramref name="rowsAbove"/> of the other rows.</summary>
    public void DragTo(int rowsAbove)
    {
        if (Lifted is not null)
        {
            var activeOthers = Arrangement().Active.Count(id => id != Lifted.Launcher.Id);
            PlaceLifted(Math.Min(rowsAbove, activeOthers + 1));
        }
    }

    /// <summary>The lifted launcher stays where it stands; among the inactive ones it is off.</summary>
    public void Drop()
    {
        if (Lifted is not { } lifted)
        {
            return;
        }

        var id = lifted.Launcher.Id;
        var active = category.Host.Launchers.ActiveIds;
        var others = active.Where(other => other != id).ToList();
        var switchedOff = _slot > others.Count;
        var chosen = LauncherSelection.Placed(others, id, switchedOff ? null : _slot);
        lifted.IsLifted = false;
        Lifted = null;
        category.IsReordering = false;
        if (chosen.SequenceEqual(active))
        {
            category.Refresh();
            return;
        }

        Choose(chosen, switchedOff && active.Contains(id) ? id : null);
    }

    /// <summary>B while a launcher is lifted, or another category: it goes back to where it was.</summary>
    public void PutBack()
    {
        if (Lifted is { } lifted)
        {
            lifted.IsLifted = false;
            Lifted = null;
            category.IsReordering = false;
            category.Refresh();
        }
    }

    /// <summary>◀ ▶ on a row, or its arrows: on goes to the end of the active ones, off to the top of the inactive ones.</summary>
    public void SwitchTo(LauncherRowViewModel row, bool on)
    {
        if (category.IsReordering)
        {
            return;
        }

        var active = category.Host.Launchers.ActiveIds;
        var id = row.Launcher.Id;
        Choose(LauncherSelection.Placed(active, id, on ? active.Count : null), on ? null : id);
    }

    /// <summary>The launchers <paramref name="active"/> are the tabs from now on; <paramref name="switchedOff"/> was just switched off.</summary>
    public void Choose(IReadOnlyList<string> active, string? switchedOff)
    {
        _switchedOff.RemoveAll(active.Contains);
        if (switchedOff is not null)
        {
            _switchedOff.Remove(switchedOff);
            _switchedOff.Insert(0, switchedOff);
        }

        category.Changes.Apply(settings => settings with { ActiveLaunchers = active }, "Launcher");
        AppLog.Write($"Aktive Launcher: {string.Join(", ", active)}");
        category.Host.LauncherChoiceChanged();
        category.Refresh();
    }

    private void PlaceLifted(int slot)
    {
        if (Lifted is null)
        {
            return;
        }

        var activeOthers = category.Host.Launchers.ActiveIds.Count(id => id != Lifted.Launcher.Id);
        slot = Math.Clamp(slot, 0, activeOthers + 1);
        if (slot != _slot)
        {
            _slot = slot;
            category.Refresh();
        }
    }
}
