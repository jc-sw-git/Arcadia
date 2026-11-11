namespace Arcadia.App.Input;

/// <summary>
/// Whether the press Main is handling is a fresh one or the repeat of a held button or key (#476). Wherever a step past
/// the end goes round to the other end, a held one stops at the end once; only a fresh press goes round. Main sets it
/// around each press (<see cref="ViewModels.MainViewModel.Handle(UiAction, bool)"/>), so the pages and dialogs that step
/// through items ask it where they step, rather than every <c>Handle</c> taking it along.
/// </summary>
/// <remarks>UI thread only, like every press; outside a press (a click, a timer) it is false.</remarks>
public static class HeldPress
{
    [ThreadStatic]
    private static bool t_isRepeat;

    /// <summary>The press being handled repeats a held button or key.</summary>
    public static bool IsRepeat => t_isRepeat;

    /// <summary>Runs <paramref name="handle"/> as a repeat (true) or a fresh press (false).</summary>
    public static void Run(bool repeat, Action handle)
    {
        var outer = t_isRepeat;
        t_isRepeat = repeat;
        try
        {
            handle();
        }
        finally
        {
            t_isRepeat = outer;
        }
    }
}
