namespace Arcadia.App.Search;

/// <summary>
/// The character keys of the on-screen keyboard, independent of the UI language, in lower case;
/// shift (LT) types them in upper case.
/// </summary>
/// <param name="Id">Stored in the settings.</param>
/// <param name="Code">Shown on the layout key, e.g. "DE".</param>
public sealed record KeyboardLayout(string Id, string Code, string Name, string Kind, IReadOnlyList<string> Rows)
{
    public static KeyboardLayout German { get; } = new("de", "DE", "Deutsch", "QWERTZ", ["1234567890ß", "qwertzuiopü", "asdfghjklöä", "yxcvbnm-:.'"]);

    /// <summary>All layouts, in the order the layout menu lists them (owner, 2026-10-17: all five until #14).</summary>
    public static IReadOnlyList<KeyboardLayout> All { get; } =
    [
        German,
        new("en-us", "US", "English (US)", "QWERTY", ["1234567890-", "qwertyuiop'", "asdfghjkl;:", "zxcvbnm,.?"]),
        new("en-gb", "UK", "English (UK)", "QWERTY", ["1234567890-", "qwertyuiop'", "asdfghjkl;#", "zxcvbnm,.?"]),
        new("fr", "FR", "Français", "AZERTY", ["1234567890-", "azertyuiopé", "qsdfghjklmè", "wxcvbnàç.'"]),
        new("es", "ES", "Español", "QWERTY con Ñ", ["1234567890¿", "qwertyuiop´", "asdfghjklñç", "zxcvbnm-.'"]),
    ];

    /// <summary>
    /// The page RT switches to: the punctuation and symbols game titles use, the same for every
    /// layout. Not in <see cref="All"/>: it is no layout of its own.
    /// </summary>
    public static KeyboardLayout Symbols { get; } = new("symbols", "#&?", "Sonderzeichen", string.Empty, ["!?&+-_=/\\*#", "()[]{}<>@%~", ".,:;'\"`^°§|", "€$£¥®™²³½·…"]);

    /// <summary>The layout stored as <paramref name="id"/>; German for none or one this version doesn't know.</summary>
    public static KeyboardLayout Find(string? id) => All.FirstOrDefault(l => l.Id == id) ?? German;
}
