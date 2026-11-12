using Arcadia.App.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// The order of a list of sources in "Datenquellen" (artwork #506, metadata #494), as in the launcher list: L3 or the mouse
/// lift a source, ▲ ▼ or dragging move it (no wrap at the ends), L3 or letting go put it down and save the order, B puts it
/// back where it was.
/// </summary>
/// <param name="saved">The order as saved.</param>
/// <param name="save">Saves a changed order.</param>
/// <param name="changed">A lift or the place of the lifted source changed: the rows are put anew.</param>
public sealed class SourceOrder<TSource>(Func<IReadOnlyList<TSource>> saved, Action<IReadOnlyList<TSource>> save, Action changed)
    where TSource : struct, Enum
{
    private TSource? _lifted;
    private int _slot;

    public TSource? Lifted => _lifted;

    public bool IsLifted => _lifted is not null;

    /// <summary>The sources as shown: in their order, a lifted one at its place now.</summary>
    public IReadOnlyList<TSource> Arrangement()
    {
        var order = saved();
        if (_lifted is not { } lifted)
        {
            return order;
        }

        var others = order.Where(s => !s.Equals(lifted)).ToList();
        others.Insert(Math.Clamp(_slot, 0, others.Count), lifted);
        return others;
    }

    /// <summary>
    /// L3 on a source lifts it; while one is lifted, ▲ ▼ move it, L3 drops it, B puts it back and anything else waits.
    /// True if the input was for the order.
    /// </summary>
    public bool Handle(UiAction action, TSource? focused)
    {
        if (_lifted is null)
        {
            if (action == UiAction.ViewOptions && focused is { } source)
            {
                Lift(source);
                return true;
            }

            return false;
        }

        switch (action)
        {
            case UiAction.Up:
                PlaceAt(_slot - 1);
                break;
            case UiAction.Down:
                PlaceAt(_slot + 1);
                break;
            case UiAction.ViewOptions:
                Drop();
                break;
            case UiAction.Back:
                PutBack();
                break;
        }

        return true;
    }

    public void Lift(TSource source)
    {
        if (_lifted is not null)
        {
            return;
        }

        _lifted = source;
        _slot = saved().ToList().IndexOf(source);
        changed();
    }

    /// <summary>The lifted source goes to <paramref name="slot"/> among all, clamped to the list.</summary>
    public void PlaceAt(int slot)
    {
        if (_lifted is null)
        {
            return;
        }

        slot = Math.Clamp(slot, 0, saved().Count - 1);
        if (slot != _slot)
        {
            _slot = slot;
            changed();
        }
    }

    /// <summary>The lifted source stays where it stands; a changed order is saved.</summary>
    public void Drop()
    {
        if (_lifted is null)
        {
            return;
        }

        var order = Arrangement();
        _lifted = null;
        if (!order.SequenceEqual(saved()))
        {
            save(order);
        }

        changed();
    }

    /// <summary>B while a source is lifted, or another category: it goes back to where it was.</summary>
    public void PutBack()
    {
        if (_lifted is null)
        {
            return;
        }

        _lifted = null;
        changed();
    }
}
