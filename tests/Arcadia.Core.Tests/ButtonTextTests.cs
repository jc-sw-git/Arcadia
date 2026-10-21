using Arcadia.Input;

namespace Arcadia.Core.Tests;

public sealed class ButtonTextTests
{
    [Fact]
    public void In_prose_only_the_symbols_are_buttons()
    {
        var parts = ButtonText.Split("⧉ + ≡ gedrückt halten, z. B. A oder X", namesToo: false);

        Assert.Equal(
            [new("⧉", true), new(" + ", false), new("≡", true), new(" gedrückt halten, z. B. A oder X", false)],
            parts);
    }

    [Fact]
    public void In_a_list_of_buttons_the_names_are_buttons_too_but_not_inside_words()
    {
        var parts = ButtonText.Split("RT oder A", namesToo: true);
        Assert.Equal([new("RT", true), new(" oder ", false), new("A", true)], parts);

        Assert.Equal([new("Linker Stick", false)], ButtonText.Split("Linker Stick", namesToo: true));
        Assert.Equal([new("LB", true), new(" / ", false), new("RB", true)], ButtonText.Split("LB / RB", namesToo: true));
    }

    [Fact]
    public void A_text_without_buttons_stays_one_piece_and_an_empty_one_has_none()
    {
        Assert.Equal([new("Mauszeiger", false)], ButtonText.Split("Mauszeiger", namesToo: false));
        Assert.Empty(ButtonText.Split(string.Empty, namesToo: true));
    }

    [Fact]
    public void In_prose_a_name_in_brackets_is_a_button_without_its_brackets()
    {
        var parts = ButtonText.Split("[LS] bewegt den Zeiger, [L3] macht ihn langsamer, A bleibt ein Wort", namesToo: false);

        Assert.Equal(
            [new("LS", true), new(" bewegt den Zeiger, ", false), new("L3", true), new(" macht ihn langsamer, A bleibt ein Wort", false)],
            parts);
        Assert.Equal([new("[Q] ist keine Taste", false)], ButtonText.Split("[Q] ist keine Taste", namesToo: false));
    }

    [Fact]
    public void In_a_list_of_buttons_the_sticks_and_the_d_pad_are_buttons()
    {
        Assert.Equal([new("LS", true)], ButtonText.Split("LS", namesToo: true));
        Assert.Equal([new("D-Pad", true)], ButtonText.Split("D-Pad", namesToo: true));
        Assert.Equal([new("RS", true), new(" halten", false)], ButtonText.Split("RS halten", namesToo: true));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("Y", true)]
    [InlineData("LB", false)]
    [InlineData("≡", false)]
    public void Only_the_face_buttons_are_round(string button, bool round) => Assert.Equal(round, ButtonText.IsRound(button));
}
