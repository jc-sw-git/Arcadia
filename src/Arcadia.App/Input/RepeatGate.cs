namespace Arcadia.App.Input;

/// <summary>
/// What a press acted on: the kind of page on top, the dialog over it and the page's own layout
/// (<see cref="ViewModels.IPage.Layout"/>, e.g. the search with its keyboard out or in).
/// </summary>
public readonly record struct ShownLayer(Type Page, object? Dialog, object? Layout);

/// <summary>
/// Lets the repeats of a held button or key act only where its press did (#476): once the page, a dialog or the page's
/// layout changed (B closed the search, the keyboard slid in, a dialog opened), the hold does nothing more until it is
/// let go, and the new page acts only on a fresh press. UI thread only.
/// </summary>
public sealed class RepeatGate
{
    private readonly Dictionary<UiAction, ShownLayer> _pressedOn = [];

    /// <summary>
    /// Whether the press of <paramref name="action"/> acts with <paramref name="shown"/> on screen: a fresh press always,
    /// remembering where; a repeat only while that is still what's shown.
    /// </summary>
    public bool Allows(UiAction action, bool repeat, ShownLayer shown)
    {
        if (!repeat)
        {
            _pressedOn[action] = shown;
            return true;
        }

        if (_pressedOn.TryGetValue(action, out var pressedOn) && pressedOn == shown)
        {
            return true;
        }

        // Stays shut for this hold, even if the first page comes back before the release.
        _pressedOn.Remove(action);
        return false;
    }

    public void Release(UiAction action) => _pressedOn.Remove(action);

    /// <summary>The releases may never arrive (the window lost the focus): no hold acts on.</summary>
    public void Clear() => _pressedOn.Clear();
}
