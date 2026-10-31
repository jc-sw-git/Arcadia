using Arcadia.Input;

namespace Arcadia.App.Input;

/// <summary>
/// Whether the button hints show the controller's buttons or the keyboard's keys (#127). One value for the
/// whole window, so every <see cref="Controls.KeyHint"/> switches at once without a binding of its own. Likewise
/// which controller's printing they show (#215).
/// </summary>
public static class InputHints
{
    private static bool _usesController = true;
    private static ButtonSymbols _symbols = ButtonSymbols.Xbox;

    /// <summary>The last input came from a controller; false after a key press or a click.</summary>
    public static bool UsesController
    {
        get => _usesController;
        set
        {
            if (_usesController == value)
            {
                return;
            }

            _usesController = value;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>The printing of the controller in use (<see cref="ViewModels.MainViewModel.ButtonSymbols"/>).</summary>
    public static ButtonSymbols Symbols
    {
        get => _symbols;
        set
        {
            if (_symbols == value)
            {
                return;
            }

            _symbols = value;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? Changed;
}
