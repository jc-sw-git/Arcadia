using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>
/// A tab strip in the header: the launcher tabs and the settings categories (#15). When not all tabs fit, the strip
/// turns so the selected tab stands in its middle, as far as the ends allow, and the wheel turns it; at its widest,
/// dividers mark the gaps to logo and status (owner, 2026-10-21). Its edges fade by themselves (<c>EdgeFade</c>).
/// </summary>
internal sealed class TabStrip
{
    private const double WheelStep = 320;

    private readonly ScrollViewer _scroller;
    private readonly ItemsControl _list;
    private readonly Control _dividerLeft;
    private readonly Control _dividerRight;
    private readonly Func<bool> _isShown;

    public TabStrip(ScrollViewer scroller, ItemsControl list, Control dividerLeft, Control dividerRight, Func<bool> isShown)
    {
        _scroller = scroller;
        _list = list;
        _dividerLeft = dividerLeft;
        _dividerRight = dividerRight;
        _isShown = isShown;
        scroller.ScrollChanged += (_, _) => UpdateDividers();
        scroller.SizeChanged += (_, _) => UpdateDividers();
        scroller.PointerWheelChanged += OnWheel;
    }

    /// <summary>Turns the strip so <paramref name="tab"/> stands in its middle.</summary>
    public void Center(object? tab)
    {
        if (tab is null || _list.ContainerFromItem(tab) is not { } container)
        {
            return;
        }

        var left = container.TranslatePoint(default, _list)?.X ?? 0;
        SetOffset(left + (container.Bounds.Width / 2) - (_scroller.Viewport.Width / 2));
    }

    public void UpdateDividers()
    {
        var max = Math.Max(0, _scroller.Extent.Width - _scroller.Viewport.Width);
        var widest = max > 0 && _isShown();
        _dividerLeft.Classes.Set("shown", widest);
        _dividerRight.Classes.Set("shown", widest);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        SetOffset(_scroller.Offset.X - (e.Delta.Y + e.Delta.X) * WheelStep);
        e.Handled = true;
    }

    private void SetOffset(double offset)
    {
        var max = Math.Max(0, _scroller.Extent.Width - _scroller.Viewport.Width);
        _scroller.Offset = new Vector(Math.Clamp(offset, 0, max), 0);
        Dispatcher.UIThread.Post(UpdateDividers, DispatcherPriority.Background);
    }
}
