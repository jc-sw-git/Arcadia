using Arcadia.App.ViewModels;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Where a notice about Pan or the emulated controller goes (#243 part B2): a toast while Arcadia is in front, the
/// small overlay notice (like the mouse mode's) while a game or another program is, where a toast would go unseen.
/// Callable from any thread.
/// </summary>
public sealed class PanNotices(ToastViewModel toast, Func<bool> isArcadiaActive, Action<string> showOverlay)
{
    public void Show(string text) => Dispatcher.UIThread.Post(() =>
    {
        if (isArcadiaActive())
        {
            toast.Show(text);
        }
        else
        {
            showOverlay(text);
        }
    });
}
