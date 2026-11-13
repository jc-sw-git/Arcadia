using Avalonia.Input;

namespace Arcadia.App.Input;

/// <summary>
/// The key that does what a controller button does (<see cref="UiActionMap.FromKey"/>), as a text names it while the
/// keyboard is in use (#539): "[Y] überspringt" shows V then, not a button the player isn't holding.
/// </summary>
public static class KeyNames
{
    /// <summary>Each button with a key of its own: the key, its modifiers and the label on the badge.</summary>
    public static IReadOnlyDictionary<string, (Key Key, KeyModifiers Modifiers, string Label)> ByButton { get; } =
        new Dictionary<string, (Key, KeyModifiers, string)>(StringComparer.Ordinal)
        {
            ["A"] = (Key.Space, KeyModifiers.None, AppTexts.KeySpace),
            ["B"] = (Key.Escape, KeyModifiers.None, "Esc"),
            ["X"] = (Key.F, KeyModifiers.None, "F"),
            ["Y"] = (Key.V, KeyModifiers.None, "V"),
            ["LB"] = (Key.Q, KeyModifiers.Shift, "⇧ Q"),
            ["RB"] = (Key.E, KeyModifiers.Shift, "⇧ E"),
            ["LT"] = (Key.Q, KeyModifiers.None, "Q"),
            ["RT"] = (Key.E, KeyModifiers.None, "E"),
            ["L3"] = (Key.C, KeyModifiers.None, "C"),
            ["R3"] = (Key.G, KeyModifiers.None, "G"),
            ["⧉"] = (Key.R, KeyModifiers.None, "R"),
            ["≡"] = (Key.Enter, KeyModifiers.None, "Enter"),
            ["D-Pad"] = (Key.W, KeyModifiers.None, "WASD"),
        };

    /// <summary>The key's label for <paramref name="button"/>; null for a button no key stands for (the sticks, Guide).</summary>
    public static string? Of(string button) => ByButton.TryGetValue(button, out var key) ? key.Label : null;
}
