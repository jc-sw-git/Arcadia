namespace Arcadia.Input;

/// <summary>Which controller's printing the button hints show (#215).</summary>
public enum ButtonSymbols
{
    Xbox,
    PlayStation5,
    PlayStation4,
    PlayStation3,
    Nintendo,
    GameCube,
    SteamController,
}

/// <summary>A symbol drawn as a vector icon (the theme's <c>PadIcon…</c> geometries), not as a font character.</summary>
public enum ButtonIcon
{
    None,
    Cross,
    Circle,
    Square,
    Triangle,
    Create,
    Options,
}

/// <summary>How one button looks on a controller: its <see cref="Text"/> or its <see cref="Icon"/>, round (face buttons) or a pill.</summary>
public readonly record struct ButtonSymbol(string Text, ButtonIcon Icon, bool IsRound);

/// <summary>
/// Turns the neutral names in texts and hints (A, B, X, Y, LB, …, ⧉, ≡, Xbox's printing) into the printing of the
/// controller in use (#215, owner 2026-10-28). The mapping stays by position (#72): A is the bottom face button on every
/// controller, so Nintendo shows B there. Only the buttons Arcadia uses have a symbol; anything else is shown as given.
/// </summary>
public static class ButtonSymbolSets
{
    /// <summary>SDL's type of a controller to its printing; generic and unknown pads show Xbox's.</summary>
    public static ButtonSymbols For(PadType type) => type switch
    {
        PadType.PS5 => ButtonSymbols.PlayStation5,
        PadType.PS4 => ButtonSymbols.PlayStation4,
        PadType.PS3 => ButtonSymbols.PlayStation3,
        PadType.SwitchPro or PadType.JoyConPair or PadType.JoyConLeft or PadType.JoyConRight => ButtonSymbols.Nintendo,
        PadType.GameCube => ButtonSymbols.GameCube,
        PadType.Steam => ButtonSymbols.SteamController,
        _ => ButtonSymbols.Xbox,
    };

    public static ButtonSymbol Symbol(string button, ButtonSymbols symbols) => symbols switch
    {
        ButtonSymbols.PlayStation5 or ButtonSymbols.PlayStation4 or ButtonSymbols.PlayStation3 => PlayStation(button, symbols),
        ButtonSymbols.Nintendo => Nintendo(button),
        ButtonSymbols.GameCube => GameCube(button),
        ButtonSymbols.SteamController => SteamController(button),
        _ => Xbox(button),
    };

    private static ButtonSymbol Xbox(string button) => new(button, ButtonIcon.None, ButtonText.IsRound(button));

    private static ButtonSymbol PlayStation(string button, ButtonSymbols generation) => button switch
    {
        "A" => Face(ButtonIcon.Cross),
        "B" => Face(ButtonIcon.Circle),
        "X" => Face(ButtonIcon.Square),
        "Y" => Face(ButtonIcon.Triangle),
        "LB" => Pill("L1"),
        "RB" => Pill("R1"),
        "LT" => Pill("L2"),
        "RT" => Pill("R2"),
        "⧉" => generation switch
        {
            ButtonSymbols.PlayStation5 => new(string.Empty, ButtonIcon.Create, IsRound: false),
            ButtonSymbols.PlayStation4 => Pill("Share"),
            _ => Pill("Select"),
        },
        "≡" => generation switch
        {
            ButtonSymbols.PlayStation5 => new(string.Empty, ButtonIcon.Options, IsRound: false),
            ButtonSymbols.PlayStation4 => Pill("Options"),
            _ => Pill("Start"),
        },
        _ => Xbox(button),
    };

    // Nintendo prints A on the right and B at the bottom; by position the bottom one still confirms (owner, 2026-10-28).
    private static ButtonSymbol Nintendo(string button) => button switch
    {
        "A" => Face("B"),
        "B" => Face("A"),
        "X" => Face("Y"),
        "Y" => Face("X"),
        "LB" => Pill("L"),
        "RB" => Pill("R"),
        "LT" => Pill("ZL"),
        "RT" => Pill("ZR"),
        "L3" => Pill("LS"),
        "R3" => Pill("RS"),
        "⧉" => Pill("−"),
        "≡" => Pill("+"),
        _ => Xbox(button),
    };

    // The GameCube pad prints A at the bottom, X right, B left (SDL's face style "axby"); it has one Z and no back
    // button or stick clicks, which keep Xbox's names.
    private static ButtonSymbol GameCube(string button) => button switch
    {
        "B" => Face("X"),
        "X" => Face("B"),
        "RB" => Pill("Z"),
        "LT" => Pill("L"),
        "RT" => Pill("R"),
        "≡" => Pill("Start"),
        _ => Xbox(button),
    };

    private static ButtonSymbol SteamController(string button) => button switch
    {
        "LB" => Pill("L1"),
        "RB" => Pill("R1"),
        "LT" => Pill("L2"),
        "RT" => Pill("R2"),
        _ => Xbox(button),
    };

    private static ButtonSymbol Face(ButtonIcon icon) => new(string.Empty, icon, IsRound: true);

    private static ButtonSymbol Face(string text) => new(text, ButtonIcon.None, IsRound: true);

    private static ButtonSymbol Pill(string text) => new(text, ButtonIcon.None, IsRound: false);
}
