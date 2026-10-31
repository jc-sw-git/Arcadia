using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// Fills a <see cref="TextBlock"/> with a text whose controller buttons are drawn as small badges, as
/// the legend draws them (<see cref="PadGlyph"/>, <c>Border.glyph.inline</c> in the theme), in the printing of the controller in use (#215). <c>Text</c> is prose (⧉, ≡ and names in brackets such as "[L3]");
/// <c>Buttons</c> is a list of buttons, where names such as A or LB become badges too.
/// </summary>
public static class ButtonTextBlock
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(ButtonTextBlock));

    public static readonly AttachedProperty<string?> ButtonsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Buttons", typeof(ButtonTextBlock));

    /// <summary>A fixed printing for every badge of the text (<see cref="PadGlyph.Symbols"/>); null follows the controller in use.</summary>
    public static readonly AttachedProperty<ButtonSymbols?> SymbolsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, ButtonSymbols?>("Symbols", typeof(ButtonTextBlock));

    static ButtonTextBlock()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((block, change) => Fill(block, change.NewValue as string, namesToo: false));
        ButtonsProperty.Changed.AddClassHandler<TextBlock>((block, change) => Fill(block, change.NewValue as string, namesToo: true));
        SymbolsProperty.Changed.AddClassHandler<TextBlock>((block, _) =>
        {
            foreach (var glyph in (block.Inlines ?? []).OfType<InlineUIContainer>().Select(container => container.Child).OfType<PadGlyph>())
            {
                glyph.Symbols = GetSymbols(block);
            }
        });
    }

    public static ButtonSymbols? GetSymbols(TextBlock block) => block.GetValue(SymbolsProperty);

    public static void SetSymbols(TextBlock block, ButtonSymbols? value) => block.SetValue(SymbolsProperty, value);

    public static string? GetText(TextBlock block) => block.GetValue(TextProperty);

    public static void SetText(TextBlock block, string? value) => block.SetValue(TextProperty, value);

    public static string? GetButtons(TextBlock block) => block.GetValue(ButtonsProperty);

    public static void SetButtons(TextBlock block, string? value) => block.SetValue(ButtonsProperty, value);

    private static void Fill(TextBlock block, string? text, bool namesToo)
    {
        var inlines = block.Inlines ??= [];
        inlines.Clear();
        foreach (var part in ButtonText.Split(text ?? string.Empty, namesToo))
        {
            inlines.Add(part.IsButton
                ? new InlineUIContainer(new PadGlyph { Classes = { "inline" }, Button = part.Text, Symbols = GetSymbols(block) })
                {
                    BaselineAlignment = BaselineAlignment.Center,
                }
                : new Run(part.Text));
        }
    }
}
