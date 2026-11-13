using Arcadia.App.Controls;
using Arcadia.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>
/// Keeps the focused row of a tag dialog (#495) in sight and out of the edge fade when the list is taller than its place,
/// as the waiting list does: by index first, since the virtualised list may have no row for it yet.
/// </summary>
internal sealed class TagRowsInSight(ItemsControl rows, EdgeFade fade)
{
    private OwnTagsDialogViewModel? _dialog;
    private Func<OwnTagsDialogViewModel, int> _focused = _ => -1;

    public void Follow(OwnTagsDialogViewModel? dialog, Func<OwnTagsDialogViewModel, int> focused)
    {
        _dialog?.FocusMoved -= OnFocusMoved;
        _dialog = dialog;
        _focused = focused;
        _dialog?.FocusMoved += OnFocusMoved;
    }

    private void OnFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_dialog is not { } dialog || _focused(dialog) is not (var index and >= 0))
        {
            return;
        }

        rows.ScrollIntoView(index);
        if (rows.ContainerFromIndex(index) is { } row)
        {
            fade.ScrollIntoView(row);
        }
    }, DispatcherPriority.Loaded);
}
