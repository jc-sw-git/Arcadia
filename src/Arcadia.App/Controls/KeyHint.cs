using Arcadia.App.Input;
using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Arcadia.App.Controls;

/// <summary>
/// A button hint for the input in use (#127): the controller's buttons (<see cref="Pad"/>) or, after a key
/// press or a click, the keyboard's keys (<see cref="Keys"/>) and a mouse alternative (<see cref="Alt"/>),
/// followed by <see cref="Text"/>. Several buttons are separated by "|", e.g. "LB|RB". Controller buttons are
/// round or a pill, keys square (<c>Border.glyph.kbd</c>), so the two inputs look apart (owner, 2026-10-21).
/// Without anything to show for the input in use the hint hides (class <c>noKeys</c>), e.g. an element that
/// no key reaches keeps only its icon.
/// </summary>
public sealed class KeyHint : StackPanel
{
    public static readonly StyledProperty<string?> PadProperty = AvaloniaProperty.Register<KeyHint, string?>(nameof(Pad));

    public static readonly StyledProperty<string?> KeysProperty = AvaloniaProperty.Register<KeyHint, string?>(nameof(Keys));

    public static readonly StyledProperty<string?> AltProperty = AvaloniaProperty.Register<KeyHint, string?>(nameof(Alt));

    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<KeyHint, string?>(nameof(Text));

    /// <summary>Glyphs on an accent surface, e.g. inside "Spielen".</summary>
    public static readonly StyledProperty<bool> IsDarkProperty = AvaloniaProperty.Register<KeyHint, bool>(nameof(IsDark));

    public KeyHint()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 10;
        Rebuild();
    }

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

    public string? Alt
    {
        get => GetValue(AltProperty);
        set => SetValue(AltProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsDark
    {
        get => GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InputHints.Changed += OnInputChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        InputHints.Changed -= OnInputChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PadProperty || change.Property == KeysProperty || change.Property == AltProperty
            || change.Property == TextProperty || change.Property == IsDarkProperty)
        {
            Rebuild();
        }
    }

    private void OnInputChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var controller = InputHints.UsesController;
        var buttons = Split(controller ? Pad : Keys);
        var alternatives = controller ? [] : Split(Alt);

        Children.Clear();
        Classes.Set("noKeys", buttons.Length + alternatives.Length == 0);
        foreach (var button in buttons)
        {
            Children.Add(Glyph(button, isKey: !controller));
        }

        if (alternatives.Length > 0)
        {
            if (buttons.Length > 0)
            {
                Children.Add(new TextBlock { Classes = { "hint" }, Text = "/" });
            }

            foreach (var alternative in alternatives)
            {
                Children.Add(Glyph(alternative, isKey: true));
            }
        }

        if (!string.IsNullOrEmpty(Text))
        {
            Children.Add(new TextBlock { Classes = { "hint" }, Text = Text });
        }
    }

    private Border Glyph(string text, bool isKey)
    {
        var glyph = new Border { Classes = { "glyph" }, Child = new TextBlock { Text = text } };
        glyph.Classes.Set("kbd", isKey);
        glyph.Classes.Set("round", !isKey && ButtonText.IsRound(text));
        glyph.Classes.Set("dark", IsDark);
        return glyph;
    }

    private static string[] Split(string? buttons) =>
        string.IsNullOrEmpty(buttons) ? [] : buttons.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
