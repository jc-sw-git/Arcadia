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
}
