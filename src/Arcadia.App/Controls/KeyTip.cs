using Arcadia.App.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Arcadia.App.Controls;

/// <summary>
/// A tooltip that names keys in brackets, e.g. "Suchen (F)": the letters follow the player's keyboard layout (#588), also
/// when the layout changes while the tooltip's control is shown. Set instead of <c>ToolTip.Tip</c>.
/// </summary>
public static class KeyTip
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Text", typeof(KeyTip));

    // The control's own handler for InputHints.Changed, so it can be taken off again when the control leaves the window.
    private static readonly AttachedProperty<EventHandler?> UpdaterProperty =
        AvaloniaProperty.RegisterAttached<Control, EventHandler?>("Updater", typeof(KeyTip));

    static KeyTip() => TextProperty.Changed.AddClassHandler<Control>((control, _) => Follow(control));

    public static string? GetText(Control control) => control.GetValue(TextProperty);

    public static void SetText(Control control, string? value) => control.SetValue(TextProperty, value);

    private static void Follow(Control control)
    {
        if (control.GetValue(UpdaterProperty) is null)
        {
            EventHandler updater = (_, _) => Show(control);
            control.SetValue(UpdaterProperty, updater);
            control.AttachedToVisualTree += (_, _) => InputHints.Changed += updater;
            control.DetachedFromVisualTree += (_, _) => InputHints.Changed -= updater;
            if (control.IsAttachedToVisualTree())
            {
                InputHints.Changed += updater;
            }
        }

        Show(control);
    }

    private static void Show(Control control) =>
        ToolTip.SetTip(control, GetText(control) is { } text ? KeyLabels.PrintedInBrackets(text) : null);
}
