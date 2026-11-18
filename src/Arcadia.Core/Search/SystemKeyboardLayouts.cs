namespace Arcadia.Core.Search;

/// <summary>Which of Arcadia's layouts a Windows keyboard layout is (#553, scenario 13).</summary>
public static class SystemKeyboardLayouts
{
    /// <summary>
    /// The id of the layout for a Windows keyboard layout handle (HKL), null when it isn't one of the five. The high word
    /// names the physical layout by the language it was made for (0x0407 German), also when it is used with another
    /// input language; a variant (high word 0xFxxx, e.g. US-International) is a layout of its own and none of the five.
    /// </summary>
    public static string? IdOf(uint keyboardLayoutHandle)
    {
        var device = keyboardLayoutHandle >> 16;
        return device switch
        {
            0x0407 => "de",
            0x0409 => "en-us",
            0x0809 => "en-gb",
            0x040C => "fr",
            0x0C0A or 0x040A => "es",
            _ => null,
        };
    }
}
