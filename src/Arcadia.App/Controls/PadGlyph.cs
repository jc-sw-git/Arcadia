using Arcadia.App.Input;
using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// One controller button as a badge (<c>Border.glyph</c>), drawn with the printing of the controller in use (#215):
/// <see cref="Button"/> is the neutral name (A, LB, ⧉, …), and the symbol follows <see cref="InputHints.Symbols"/> at once.
/// Face buttons are round, the others a pill. PlayStation's shapes are vector icons from the theme (<c>PadIcon…</c>, styled
/// as <c>Border.glyph > Path</c>), because fonts draw ✕ ○ □ △ in different sizes.
/// </summary>
public sealed class PadGlyph : Border
{
    public static readonly StyledProperty<string?> ButtonProperty = AvaloniaProperty.Register<PadGlyph, string?>(nameof(Button));

    private ButtonSymbol? _shown;

    public PadGlyph()
    {
        Classes.Add("glyph");
    }

    public string? Button
    {
        get => GetValue(ButtonProperty);
        set => SetValue(ButtonProperty, value);
    }

    /// <summary>What the badge shows now, e.g. for a test: the text, or the icon's name.</summary>
    public ButtonSymbol? Shown => _shown;

    protected override Type StyleKeyOverride => typeof(Border);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InputHints.Changed += OnInputChanged;
        _shown = null;
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
        if (change.Property == ButtonProperty)
        {
            Update();
        }
    }

    private void OnInputChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var symbol = ButtonSymbolSets.Symbol(Button ?? string.Empty, InputHints.Symbols);
        if (symbol == _shown)
        {
            return;
        }

        _shown = symbol;
        Classes.Set("round", symbol.IsRound);
        Child = symbol.Icon == ButtonIcon.None
            ? new TextBlock { Text = symbol.Text }
            : new Path { Data = Icon(symbol.Icon) };
    }

    // The theme's geometry; detached (before the first show) the control finds only the application's resources.
    private Geometry? Icon(ButtonIcon icon) =>
        this.TryFindResource($"PadIcon{icon}", out var found) || (Application.Current?.TryFindResource($"PadIcon{icon}", out found) ?? false)
            ? found as Geometry
            : null;
}
