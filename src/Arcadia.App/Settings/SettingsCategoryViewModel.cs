using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Navigation;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A category of the settings page, a tab in its header (#15): a column of rows (pattern "Leiste") with the focus on
/// one of them. Only categories with content exist; each new one is a subclass, added in <see cref="MainViewModel"/>.
/// </summary>
public abstract partial class SettingsCategoryViewModel : ViewModelBase, IDisposable
{
    private int _focus = -1;

    protected SettingsCategoryViewModel(string name, string icon)
    {
        Name = name;
        Icon = StreamGeometry.Parse(icon);
    }

    public string Name { get; }

    /// <summary>A line drawing in a 24 × 24 box, drawn in the tab's text colour.</summary>
    public Geometry Icon { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// The worst status message of the category, as a symbol beside its name: a warning or an error (owner, 2026-10-22).
    /// Information shows nothing, so it is never <see cref="HealthSeverity.Info"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMarker), nameof(IsMarkerError))]
    public partial HealthSeverity? Marker { get; protected set; }

    public bool HasMarker => Marker is not null;

    public bool IsMarkerError => Marker == HealthSeverity.Error;

    public ObservableCollection<SettingsRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial SettingsRowViewModel? FocusedRow { get; private set; }

    /// <summary>A dialog of the category over the page (e.g. "Steam beenden?"); it takes every input until it closes.</summary>
    [ObservableProperty]
    public partial object? Dialog { get; protected set; }

    /// <summary>A click on the category's tab.</summary>
    public IRelayCommand? SelectCommand { get; set; }

    /// <summary>The focus moved to another row, by the controller, a key or a click.</summary>
    public event EventHandler? FocusMoved;

    public void MoveFocus(int delta) => FocusAt(ListFocus.Move(_focus, delta, Rows.Count));

    /// <summary>An input while <see cref="Dialog"/> is open; B closes the dialog, not the page.</summary>
    public virtual void HandleDialog(UiAction action)
    {
    }

    /// <summary>B on the rows: true if the category went a level up itself (later: a launcher's page back to the list).</summary>
    public virtual bool Back() => false;

    public virtual void Dispose()
    {
    }

    /// <summary>
    /// Shows <paramref name="rows"/> in this order. Rows shown before keep their instance, and the focus stays on the row
    /// it stood on, wherever that is now; nothing is redrawn when nothing changed.
    /// </summary>
    protected void SetRows(IReadOnlyList<SettingsRowViewModel> rows)
    {
        if (Rows.SequenceEqual(rows))
        {
            return;
        }

        var focusedKey = FocusedRow?.Key;
        var previousIndex = Math.Max(0, _focus);
        Rows.Clear();
        string? group = null;
        foreach (var row in rows)
        {
            row.StartsGroup = row.Group is not null && row.Group != group;
            group = row.Group;
            row.Pointed = r => FocusAt(Rows.IndexOf(r));
            Rows.Add(row);
        }

        var index = ListFocus.Keep(focusedKey, previousIndex, [.. Rows.Select(r => r.Key)]);
        if (index >= 0 && Rows[index] == FocusedRow)
        {
            // Still on the same row: the info panel shows the same, only the place changed.
            _focus = index;
        }
        else
        {
            FocusAt(index);
        }
    }

    private void FocusAt(int index)
    {
        var row = index >= 0 && index < Rows.Count ? Rows[index] : null;
        if (index == _focus && row == FocusedRow)
        {
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

        FocusMoved?.Invoke(this, EventArgs.Empty);
    }
}
