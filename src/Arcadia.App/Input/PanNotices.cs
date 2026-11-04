using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Avalonia.Threading;

namespace Arcadia.App.Input;

/// <summary>
/// Where a notice about Pan or the emulated controller goes (#243 part B2): a toast while Arcadia is in front, the
/// small overlay notice (like the mouse mode's) while a game or another program is, where a toast would go unseen.
/// Callable from any thread.
/// </summary>
public sealed class PanNotices(ToastViewModel toast, Func<bool> isArcadiaActive, Action<string> showOverlay)
{
    /// <param name="level">The level of the notice's source (#433); it is an entry of "Letzte Meldungen" either way.</param>
    public void Show(HealthSeverity level, string text) => Dispatcher.UIThread.Post(() =>
    {
        if (isArcadiaActive())
        {
            toast.Show(level, text);
        }
        else
        {
            // Behind a game the toast would go unseen, but the list keeps it.
            toast.History.Add(level, Messages.MessageKind.Toast, text);
            showOverlay(text);
        }
    });
}
