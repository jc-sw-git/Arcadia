namespace Arcadia.App.Input;

/// <summary>
/// Whether the button hints show the controller's buttons or the keyboard's keys (#127). One value for the
/// whole window, so every <see cref="Controls.KeyHint"/> switches at once without a binding of its own.
/// </summary>
public static class InputHints
{
    private static bool _usesController = true;

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

    public static event EventHandler? Changed;
}
