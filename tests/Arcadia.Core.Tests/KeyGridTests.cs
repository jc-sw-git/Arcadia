using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class KeyGridTests
{
    // Built from strings: one character per key, '.' an empty key; keys 10 apart, rows centred.
    private static List<KeySpot> Row(string keys)
    {
        var left = -(keys.Length - 1) * 5.0;
        return keys.Select((k, i) => new KeySpot(left + (i * 10), k == '.')).ToList();
    }

    [Fact]
    public void Sideways_passes_over_empty_keys()
    {
        var row = Row("a..b.c");

        Assert.Equal(3, KeyGrid.Sideways(row, 0, 1));
        Assert.Equal(5, KeyGrid.Sideways(row, 3, 1));
        Assert.Equal(0, KeyGrid.Sideways(row, 3, -1));
    }

    [Fact]
    public void Sideways_stays_at_the_edge_and_before_only_empty_keys()
    {
        var row = Row("a.b..");

        Assert.Equal(0, KeyGrid.Sideways(row, 0, -1));
        Assert.Equal(2, KeyGrid.Sideways(row, 2, 1));
    }

    [Fact]
    public void Up_and_down_go_to_the_nearest_key_that_isnt_empty()
    {
        // German AltGr level: from € left to @, up to ³, down to |, right to ~.
        List<List<KeySpot>> rows = [Row("..²³...{[]}\\."), Row("@.€........~"), Row("............"), Row("|......µ...")];

        Assert.Equal(0, KeyGrid.Sideways(rows[1], 2, -1));
        Assert.Equal(11, KeyGrid.Sideways(rows[1], 2, 1));
        Assert.Equal((0, 3), KeyGrid.Vertical(rows, 1, 2, -1));
        Assert.Equal((3, 0), KeyGrid.Vertical(rows, 1, 2, 1));
    }

    [Fact]
    public void Between_two_keys_equally_near_up_goes_right_and_down_goes_left_so_the_way_back_leads_home()
    {
        List<List<KeySpot>> rows = [Row("1234567890abc"), Row("qwertzuiopüd")];

        Assert.Equal((0, 3), KeyGrid.Vertical(rows, 1, 2, -1));
        Assert.Equal((1, 2), KeyGrid.Vertical(rows, 0, 3, 1));
    }

    [Fact]
    public void A_row_with_nothing_to_go_to_is_passed_over_and_the_last_row_keeps_the_focus()
    {
        List<List<KeySpot>> rows = [Row("ab"), Row("...."), Row("cd")];

        Assert.Equal((2, 0), KeyGrid.Vertical(rows, 0, 0, 1));
        Assert.Equal((0, 1), KeyGrid.Vertical(rows, 2, 1, -1));
        Assert.Equal((2, 1), KeyGrid.Vertical(rows, 2, 1, 1));
    }
}
