using Arcadia.App.Input;
using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// The label of a button that stands for a controller button, e.g. LB beside the tabs: <see cref="Pad"/> while
/// the controller is in use, in the printing of the controller in use (#215), <see cref="Keys"/> after a key press or a click (#127).
/// </summary>
public sealed class KeyText : TextBlock
{
    public static readonly StyledProperty<string?> PadProperty = AvaloniaProperty.Register<KeyText, string?>(nameof(Pad));

    public static readonly StyledProperty<string?> KeysProperty = AvaloniaProperty.Register<KeyText, string?>(nameof(Keys));

    public string? Pad
    {
        get => GetValue(PadProperty);
        set => SetValue(PadProperty, value);
    }

    public string? Keys
    {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InputHints.Changed += OnInputChanged;
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        InputHints.Changed -= OnInputChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PadProperty || change.Property == KeysProperty)
        {
            Update();
        }
    }

    private void OnInputChanged(object? sender, EventArgs e) => Update();

    // Only pills stand here (LB, RT, …), so the symbol is always text.
    private void Update() => Text = InputHints.UsesController ? ButtonSymbolSets.Symbol(Pad ?? string.Empty, InputHints.Symbols).Text : Keys;
}
