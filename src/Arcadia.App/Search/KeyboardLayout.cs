namespace Arcadia.App.Search;

/// <summary>
/// The character keys of the on-screen keyboard, independent of the UI language. Search is
/// case-insensitive, so there is no shift.
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

    /// <summary>The layout stored as <paramref name="id"/>; German for none or one this version doesn't know.</summary>
    public static KeyboardLayout Find(string? id) => All.FirstOrDefault(l => l.Id == id) ?? German;
}
