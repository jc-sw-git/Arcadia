namespace Arcadia.App.Settings;

/// <summary>
/// A category whose rows the mouse moves by dragging them, as L3 and ▲ ▼ do (#136 launchers, #506 artwork sources): the
/// window tells it where the dragged row is over the others.
/// </summary>
public interface IDraggableRows
{
    /// <summary>The row can be lifted and moved now.</summary>
    bool CanDrag(SettingsRowViewModel row);

    /// <summary>The row is one of the list a dragged row moves within; the others are left out of the count.</summary>
    bool IsInDragList(SettingsRowViewModel row);

    /// <summary>The mouse starts dragging <paramref name="row"/>: lifted as with L3.</summary>
    void BeginDrag(SettingsRowViewModel row);

    /// <summary>The dragged row is over the place below <paramref name="rowsAbove"/> of the other rows of the list.</summary>
    void DragTo(int rowsAbove);

    /// <summary>The mouse let go: the row stays where it stands.</summary>
    void Drop();
}
