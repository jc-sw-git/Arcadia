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

    /// <summary>What Y does on the rows, for the footer (e.g. "Verschieben"); null if nothing.</summary>
    [ObservableProperty]
    public partial string? YActionText { get; protected set; }

    /// <summary>What X does on the rows, for the footer (e.g. "Alle neu einlesen"); null if nothing.</summary>
    [ObservableProperty]
    public partial string? XActionText { get; protected set; }

    /// <summary>Above the rows, e.g. a launcher's logo and name on its page (#137); null shows nothing.</summary>
    [ObservableProperty]
    public partial object? Head { get; protected set; }

    /// <summary>A text being written with the on-screen keyboard (#137); it takes every input until it closes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    public partial TextEditorViewModel? Editor { get; protected set; }

    public bool IsEditing => Editor is not null;

    /// <summary>
    /// A row is lifted and moves with ▲ ▼ (#136): the footer names only that, and the mouse passing over rows doesn't take
    /// the focus from it.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReordering { get; protected set; }

    /// <summary>A click on the category's tab.</summary>
    public IRelayCommand? SelectCommand { get; set; }

    /// <summary>The focus moved to another row, by the controller, a key or a click.</summary>
    public event EventHandler? FocusMoved;

    public void MoveFocus(int delta) => FocusAt(ListFocus.Move(_focus, delta, Rows.Count));

    /// <summary>An input for the category itself before the page's own (e.g. Y, X); true if it took it.</summary>
    public virtual bool Handle(UiAction action) => false;

    /// <summary>Another category is shown or the page closes: something half done (a lifted row) is dropped.</summary>
    public virtual void Leave()
    {
    }

    /// <summary>The category's tab is chosen (LB/RB, a click): it starts at its top level, not where it was left.</summary>
    public virtual void ShowTopLevel()
    {
    }

    /// <summary>An input while <see cref="Dialog"/> is open; B closes the dialog, not the page.</summary>
    public virtual void HandleDialog(UiAction action)
    {
    }

    /// <summary>B on the rows: true if the category went a level up itself (later: a launcher's page back to the list).</summary>
    public virtual bool Back() => false;

    /// <summary>Puts the focus on the row with this key, if the category shows it now (e.g. the row that says why Pan doesn't run).</summary>
    public void FocusRow(string key)
    {
        if (Rows.FirstOrDefault(r => r.Key == key) is { } row)
        {
            Focus(row);
        }
    }

    public virtual void Dispose()
    {
    }

    /// <summary>
    /// Shows <paramref name="rows"/> in this order. Rows shown before keep their instance, and the focus stays on the row
    /// it stood on, wherever that is now; nothing is redrawn when nothing changed.
    /// </summary>
    protected void SetRows(IReadOnlyList<SettingsRowViewModel> rows)
    {
        // A row may change its group without moving (a launcher switched off, #136).
        string? group = null;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            row.StartsGroup = i == 0 || row.Group != group;
            row.EndsGroup = i == rows.Count - 1 || rows[i + 1].Group != row.Group;
            group = row.Group;
        }

        if (Rows.SequenceEqual(rows))
        {
            return;
        }

        var focusedKey = FocusedRow?.Key;
        var previousIndex = Math.Max(0, _focus);
        Rows.Clear();
        foreach (var row in rows)
        {
            row.Pointed = r =>
            {
                // A lifted row or a text being written keeps the focus where it is.
                if (!IsReordering && !IsEditing)
                {
                    FocusAt(Rows.IndexOf(r));
                }
            };
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

    /// <summary>Puts the focus on <paramref name="row"/>, e.g. the launcher a tab's "Launcher-Einstellungen" was chosen for.</summary>
    protected void Focus(SettingsRowViewModel row) => FocusAt(Rows.IndexOf(row));

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
