namespace Arcadia.Core.Search;

/// <summary>The level of the keyboard a key types on: as is, with shift (LB) or with AltGr (RB).</summary>
public enum KeyLevel
{
    Normal,
    Shift,
    AltGr,
}

/// <summary>
/// One character key of a real keyboard with what it types on each level; null where it types
/// nothing. Dead keys (^ ´ ` ¨ ~) type their own character, without joining the next letter.
/// </summary>
public sealed record KeyCap(char Normal, char? Shift, char? AltGr)
{
    /// <summary>What the key types on <paramref name="level"/>, null for nothing.</summary>
    public char? On(KeyLevel level) => level switch
    {
        KeyLevel.Shift => Shift,
        KeyLevel.AltGr => AltGr,
        _ => Normal,
    };

    /// <summary>
    /// The shift character drawn small above the normal one, as on the printed key: only when it isn't
    /// simply the letter in upper case (q shows no Q, but 1 shows !).
    /// </summary>
    public char? ShiftHint => Shift is { } shift && shift != char.ToUpperInvariant(Normal) ? shift : null;
}

/// <summary>The physical shape of a keyboard: the key rows' lengths and the Enter key differ (#110).</summary>
public enum KeyboardForm
{
    /// <summary>European: a tall Enter over two rows, a key between left shift and the first letter.</summary>
    Iso,

    /// <summary>American: a wide Enter in the home row, backslash above it, a wide left shift.</summary>
    Ansi,
}

/// <summary>
/// A real keyboard layout, row by row from the number row down, as printed on the keys (owner,
/// 2026-10-21: the on-screen keyboard is a 1:1 replica of the real keyboard, like Steam's, so it can
/// also serve outside Arcadia, #110). Tab, caps lock, enter and shift aren't in the rows; the mouse
/// mode's keyboard adds them where they sit on the real one (<see cref="FullKeyboard"/>).
/// </summary>
/// <param name="Id">Stored in the settings.</param>
/// <param name="Code">Shown on the layout key, e.g. "DE".</param>
public sealed record KeyboardLayout(string Id, string Code, string Name, string Kind, IReadOnlyList<IReadOnlyList<KeyCap>> Rows)
{
    /// <summary>Where the character keys sit on the real keyboard (<see cref="FullKeyboard"/>).</summary>
    public KeyboardForm Form { get; init; } = KeyboardForm.Iso;

    public static KeyboardLayout German { get; } = new("de", "DE", "Deutsch", "QWERTZ",
    [
        Row("^1234567890ß´", "°!\"§$%&/()=?`", "  ²³   {[]}\\ "),
        Row("qwertzuiopü+", "QWERTZUIOPÜ*", "@ €        ~"),
        Row("asdfghjklöä#", "ASDFGHJKLÖÄ'", string.Empty),
        Row("<yxcvbnm,.-", ">YXCVBNM;:_", "|      µ"),
    ]);

    /// <summary>All layouts, in the order the layout menu lists them (owner, 2026-10-17: all five until #14).</summary>
    public static IReadOnlyList<KeyboardLayout> All { get; } =
    [
        German,
        new("en-us", "US", "English (US)", "QWERTY",
        [
            Row("`1234567890-=", "~!@#$%^&*()_+", string.Empty),
            Row("qwertyuiop[]\\", "QWERTYUIOP{}|", string.Empty),
            Row("asdfghjkl;'", "ASDFGHJKL:\"", string.Empty),
            Row("zxcvbnm,./", "ZXCVBNM<>?", string.Empty),
        ]) { Form = KeyboardForm.Ansi },
        new("en-gb", "UK", "English (UK)", "QWERTY",
        [
            Row("`1234567890-=", "¬!\"£$%^&*()_+", "¦   €"),
            Row("qwertyuiop[]", "QWERTYUIOP{}", "  é   úíó"),
            Row("asdfghjkl;'#", "ASDFGHJKL:@~", "á"),
            Row("\\zxcvbnm,./", "|ZXCVBNM<>?", string.Empty),
        ]),
        new("fr", "FR", "Français", "AZERTY",
        [
            Row("²&é\"'(-è_çà)=", " 1234567890°+", "  ~#{[|`\\^@]}"),
            Row("azertyuiop^$", "AZERTYUIOP¨£", "  €        ¤"),
            Row("qsdfghjklmù*", "QSDFGHJKLM%µ", string.Empty),
            Row("<wxcvbn,;:!", ">WXCVBN?./§", string.Empty),
        ]),
        new("es", "ES", "Español", "QWERTY con Ñ",
        [
            Row("º1234567890'¡", "ª!\"·$%&/()=?¿", "\\|@#~€¬"),
            Row("qwertyuiop`+", "QWERTYUIOP^*", "  €       []"),
            Row("asdfghjklñ´ç", "ASDFGHJKLÑ¨Ç", "          {}"),
            Row("<zxcvbnm,.-", ">ZXCVBNM;:_", string.Empty),
        ]),
    ];

    /// <summary>Some key types something with AltGr; without (US) RB is greyed out.</summary>
    public bool HasAltGr => Rows.Any(row => row.Any(key => key.AltGr is not null));

    /// <summary>The layout stored as <paramref name="id"/>; German for none or one this version doesn't know.</summary>
    public static KeyboardLayout Find(string? id) => All.FirstOrDefault(l => l.Id == id) ?? German;

    /// <summary>
    /// A row from the characters of each level, key by key; a space stands for nothing. The AltGr
    /// characters may stop early when the keys after them have none.
    /// </summary>
    private static KeyCap[] Row(string normal, string shift, string altGr)
    {
        if (shift.Length != normal.Length || altGr.Length > normal.Length)
        {
            throw new ArgumentException($"The levels of the row \"{normal}\" don't line up.");
        }

        return normal.Select((character, i) => new KeyCap(character, At(shift, i), At(altGr, i))).ToArray();
    }

    private static char? At(string level, int index) => index < level.Length && level[index] != ' ' ? level[index] : null;
}
