using Avalonia.Input;

namespace Arcadia.App.Input;

/// <summary>
/// The keyboard controls lie by position, not by letter (#588): the key where W sits on a US keyboard moves up on every
/// layout, so the one-hand cluster around WASD stays together on AZERTY (ZQSD) as on QWERTZ. Letters and the digit row
/// are named by their US QWERTY place; every other key (Space, Esc, Enter, arrows, F-keys) keeps the key Windows reports.
/// </summary>
public static class KeyPosition
{
    /// <summary>The key a US QWERTY keyboard has at the place of <paramref name="physical"/>, for letters and digits; else <paramref name="key"/>.</summary>
    public static Key Of(Key key, PhysicalKey physical) =>
        physical is >= PhysicalKey.A and <= PhysicalKey.Z or >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9
            ? physical.ToQwertyKey()
            : key;

    public static Key Of(KeyEventArgs e) => Of(e.Key, e.PhysicalKey);
}
