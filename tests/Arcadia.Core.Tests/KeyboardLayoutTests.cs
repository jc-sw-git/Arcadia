using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class KeyboardLayoutTests
{
    public static TheoryData<string> LayoutIds => [.. KeyboardLayout.All.Select(l => l.Id)];

    [Theory]
    [MemberData(nameof(LayoutIds))]
    public void Every_layout_has_four_rows_of_at_most_13_keys_that_all_type_something_with_shift_or_without(string id)
    {
        var layout = KeyboardLayout.Find(id);

        Assert.Equal(id, layout.Id);
        Assert.Equal(4, layout.Rows.Count);
        Assert.All(layout.Rows, row => Assert.InRange(row.Count, 10, 13));
        Assert.All(layout.Rows.SelectMany(r => r), key => Assert.NotEqual(' ', key.Normal));
    }

    [Theory]
    [InlineData("de", "^1234567890ß´", "°!\"§$%&/()=?`")]
    [InlineData("en-us", "`1234567890-=", "~!@#$%^&*()_+")]
    [InlineData("en-gb", "`1234567890-=", "¬!\"£$%^&*()_+")]
    [InlineData("es", "º1234567890'¡", "ª!\"·$%&/()=?¿")]
    public void The_number_row_is_the_real_keyboards(string id, string normal, string shift)
    {
        var row = KeyboardLayout.Find(id).Rows[0];

        Assert.Equal(normal, string.Concat(row.Select(k => k.Normal)));
        Assert.Equal(shift, string.Concat(row.Select(k => k.Shift)));
    }

    [Theory]
    [InlineData("de", 'q', '@')]
    [InlineData("de", 'e', '€')]
    [InlineData("de", '+', '~')]
    [InlineData("de", '<', '|')]
    [InlineData("de", 'm', 'µ')]
    [InlineData("de", '7', '{')]
    [InlineData("de", 'ß', '\\')]
    [InlineData("en-gb", '4', '€')]
    [InlineData("en-gb", 'u', 'ú')]
    [InlineData("en-gb", 'a', 'á')]
    [InlineData("fr", 'à', '@')]
    [InlineData("fr", '$', '¤')]
    [InlineData("fr", '=', '}')]
    [InlineData("es", '2', '@')]
    [InlineData("es", '`', '[')]
    [InlineData("es", '+', ']')]
    [InlineData("es", 'ç', '}')]
    public void AltGr_types_what_the_real_key_has_at_its_bottom_right(string id, char normal, char altGr)
    {
        var key = KeyboardLayout.Find(id).Rows.SelectMany(r => r).Single(k => k.Normal == normal);

        Assert.Equal(altGr, key.On(KeyLevel.AltGr));
    }

    [Fact]
    public void A_key_without_a_character_on_a_level_types_nothing_there()
    {
        var w = KeyboardLayout.German.Rows[1][1];
        var frSquared = KeyboardLayout.Find("fr").Rows[0][0];

        Assert.Equal('w', w.On(KeyLevel.Normal));
        Assert.Equal('W', w.On(KeyLevel.Shift));
        Assert.Null(w.On(KeyLevel.AltGr));
        Assert.Null(frSquared.On(KeyLevel.Shift));
    }

    [Fact]
    public void Dead_keys_type_their_own_character()
    {
        var circumflex = KeyboardLayout.German.Rows[0][0];
        var acute = KeyboardLayout.German.Rows[0][12];

        Assert.Equal('^', circumflex.On(KeyLevel.Normal));
        Assert.Equal('´', acute.On(KeyLevel.Normal));
        Assert.Equal('`', acute.On(KeyLevel.Shift));
    }

    [Fact]
    public void The_shift_hint_leaves_out_plain_capital_letters()
    {
        var row = KeyboardLayout.German.Rows[0];

        Assert.Equal('!', row[1].ShiftHint);
        Assert.Equal('?', row[11].ShiftHint);
        Assert.Null(KeyboardLayout.German.Rows[1][0].ShiftHint);
        Assert.Null(KeyboardLayout.German.Rows[1][10].ShiftHint);
    }

    [Fact]
    public void Only_us_has_no_altgr_level()
    {
        Assert.Equal(["en-us"], KeyboardLayout.All.Where(l => !l.HasAltGr).Select(l => l.Id));
    }

    [Fact]
    public void An_unknown_layout_falls_back_to_german()
    {
        Assert.Same(KeyboardLayout.German, KeyboardLayout.Find("xx"));
        Assert.Same(KeyboardLayout.German, KeyboardLayout.Find(null));
    }
}
