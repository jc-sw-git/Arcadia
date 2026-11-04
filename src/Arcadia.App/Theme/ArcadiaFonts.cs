using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Arcadia.App;

/// <summary>
/// The fonts of every view (#24): Figtree, embedded, as in the designs; Inter, embedded too, only for the symbols Figtree
/// lacks (▲▼◀▶ ↵ ⇧ ⌫ ␣). Symbols neither font has (⧉ ≡ ↹) are drawn as theme icons. Embedded, so text looks and measures
/// the same on every machine; the theme's <c>ContentControlThemeFontFamily</c> names the same pair.
/// </summary>
public static class ArcadiaFonts
{
    public const string Figtree = "fonts:Figtree#Figtree";

    public const string Inter = "fonts:Inter#Inter";

    public static AppBuilder WithArcadiaFonts(this AppBuilder builder) => builder
        .WithInterFont()
        .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(new Uri("fonts:Figtree"), new Uri("avares://Arcadia/Assets/Fonts"))))
        .With(new FontManagerOptions
        {
            DefaultFamilyName = Figtree,
            FontFallbacks = [new FontFallback { FontFamily = new FontFamily(Inter) }],
        });
}
