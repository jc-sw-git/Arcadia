using Arcadia.Input;

namespace Arcadia.Core.Tests;

/// <summary>The printing of each controller for the neutral names in texts and hints (#215, owner 2026-10-28).</summary>
public sealed class ButtonSymbolSetsTests
{
    private static readonly string[] Buttons = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "⧉", "≡", "D-Pad"];

    [Theory]
    [InlineData(PadType.Xbox360, ButtonSymbols.Xbox)]
    [InlineData(PadType.XboxOne, ButtonSymbols.Xbox)]
    [InlineData(PadType.Standard, ButtonSymbols.Xbox)]
    [InlineData(PadType.Unknown, ButtonSymbols.Xbox)]
    [InlineData(PadType.PS5, ButtonSymbols.PlayStation5)]
    [InlineData(PadType.PS4, ButtonSymbols.PlayStation4)]
    [InlineData(PadType.PS3, ButtonSymbols.PlayStation3)]
    [InlineData(PadType.SwitchPro, ButtonSymbols.Nintendo)]
    [InlineData(PadType.JoyConPair, ButtonSymbols.Nintendo)]
    [InlineData(PadType.GameCube, ButtonSymbols.Nintendo)]
    [InlineData(PadType.Steam, ButtonSymbols.SteamController)]
    public void Each_type_of_controller_shows_its_own_printing(PadType type, ButtonSymbols symbols) =>
        Assert.Equal(symbols, ButtonSymbolSets.For(type));

    [Theory]
    [InlineData(ButtonSymbols.Xbox, "A B X Y LB RB LT RT L3 R3 ⧉ ≡ D-Pad")]
    [InlineData(ButtonSymbols.PlayStation5, "Cross Circle Square Triangle L1 R1 L2 R2 L3 R3 Create Options D-Pad")]
    [InlineData(ButtonSymbols.PlayStation4, "Cross Circle Square Triangle L1 R1 L2 R2 L3 R3 Share Options D-Pad")]
    [InlineData(ButtonSymbols.PlayStation3, "Cross Circle Square Triangle L1 R1 L2 R2 L3 R3 Select Start D-Pad")]
    [InlineData(ButtonSymbols.Nintendo, "B A Y X L R ZL ZR LS RS − + D-Pad")]
    [InlineData(ButtonSymbols.SteamController, "A B X Y L1 R1 L2 R2 L3 R3 ⧉ ≡ D-Pad")]
    public void The_buttons_Arcadia_uses_get_the_printing_of_the_controller(ButtonSymbols symbols, string printed) =>
        Assert.Equal(printed, string.Join(' ', Buttons.Select(button => Shown(ButtonSymbolSets.Symbol(button, symbols)))));

    [Theory]
    [InlineData(ButtonSymbols.Xbox)]
    [InlineData(ButtonSymbols.PlayStation5)]
    [InlineData(ButtonSymbols.PlayStation3)]
    [InlineData(ButtonSymbols.Nintendo)]
    [InlineData(ButtonSymbols.SteamController)]
    public void Face_buttons_are_round_everything_else_a_pill(ButtonSymbols symbols)
    {
        foreach (var button in Buttons)
        {
            Assert.Equal(button is "A" or "B" or "X" or "Y", ButtonSymbolSets.Symbol(button, symbols).IsRound);
        }
    }

    /// <summary>Confirm stays the bottom button: on a Nintendo pad it is printed B (owner, 2026-10-28).</summary>
    [Fact]
    public void Confirm_shows_B_on_a_Nintendo_controller_and_back_shows_A() =>
        Assert.Equal(("B", "A"), (ButtonSymbolSets.Symbol("A", ButtonSymbols.Nintendo).Text, ButtonSymbolSets.Symbol("B", ButtonSymbols.Nintendo).Text));

    [Theory]
    [InlineData("▲▼")]
    [InlineData("LS")]
    [InlineData("Klick")]
    public void A_name_without_a_symbol_of_its_own_is_shown_as_given(string button)
    {
        foreach (var symbols in Enum.GetValues<ButtonSymbols>())
        {
            Assert.Equal(new ButtonSymbol(button, ButtonIcon.None, IsRound: false), ButtonSymbolSets.Symbol(button, symbols));
        }
    }

    private static string Shown(ButtonSymbol symbol) => symbol.Icon == ButtonIcon.None ? symbol.Text : symbol.Icon.ToString();
}
