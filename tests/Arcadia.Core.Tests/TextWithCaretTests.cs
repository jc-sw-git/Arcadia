using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class TextWithCaretTests
{
    [Fact]
    public void Typing_goes_in_at_the_caret_and_backspace_takes_the_character_before_it()
    {
        var text = TextWithCaret.AtEnd("halo").Left().Left();

        text = text.Insert("l");
        Assert.Equal(("hallo", 3), (text.Text, text.Caret));

        text = text.EraseBefore().EraseBefore();
        Assert.Equal(("hlo", 1), (text.Text, text.Caret));
    }

    [Fact]
    public void The_caret_stops_at_both_ends()
    {
        var text = TextWithCaret.AtEnd("ab");

        Assert.Equal(2, text.Right().Caret);
        Assert.Equal(0, text.Home().Left().Caret);
        Assert.Equal(text, text.Home().EraseBefore().End());
        Assert.Equal(("a", "b"), (text.Left().BeforeCaret, text.Left().AfterCaret));
    }

    [Theory]
    [InlineData("a😀b")]
    [InlineData("aéb")]
    public void A_character_made_of_several_units_counts_as_one(string word)
    {
        var text = TextWithCaret.AtEnd(word).Left();

        Assert.Equal(1, text.Left().Caret);
        Assert.Equal(1, text.Home().Right().Caret);
        Assert.Equal("ab", text.EraseBefore().Text);
    }

    [Fact]
    public void A_caret_beyond_the_text_is_put_at_its_end()
    {
        Assert.Equal(3, new TextWithCaret("abc", 9).Caret);
        Assert.Equal(0, new TextWithCaret("abc", -1).Caret);
    }
}
