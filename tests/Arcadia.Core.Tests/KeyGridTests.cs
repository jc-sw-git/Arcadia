using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class KeyGridTests
{
    // Built from strings: one character per key, '.' an empty key; keys one apart, rows centred.
    private static List<KeySpot> Row(string keys)
    {
        var left = -(keys.Length - 1) * 0.5;
        return keys.Select((k, i) => new KeySpot(left + i, k == '.')).ToList();
    }

    private static List<IReadOnlyList<KeySpot>> Rows(params string[] rows) => [.. rows.Select(Row)];

    [Fact]
    public void Sideways_passes_over_empty_keys()
    {
        var rows = Rows("a..b.c");

        Assert.Equal((0, 3), KeyGrid.Sideways(rows, 0, 0, 1));
        Assert.Equal((0, 5), KeyGrid.Sideways(rows, 0, 3, 1));
        Assert.Equal((0, 0), KeyGrid.Sideways(rows, 0, 3, -1));
    }

    [Fact]
    public void Sideways_stays_at_the_edge_and_before_only_empty_keys()
    {
        var rows = Rows("a.b..");

        Assert.Equal((0, 0), KeyGrid.Sideways(rows, 0, 0, -1));
        Assert.Equal((0, 2), KeyGrid.Sideways(rows, 0, 2, 1));
    }

    [Fact]
    public void Sideways_goes_on_in_another_row_when_its_own_has_nothing_more_that_way()
    {
        // The nearest row wins; c, half below b, is below it, not beside it.
        var rows = Rows("..b..", "a.c...", ".....", "e....");

        Assert.Equal((1, 0), KeyGrid.Sideways(rows, 0, 2, -1));
        Assert.Equal((1, 2), KeyGrid.Sideways(rows, 1, 2, 1));
        Assert.Equal((3, 0), KeyGrid.Sideways(rows, 3, 0, -1));
    }

    [Fact]
    public void Up_and_down_go_to_the_nearest_key_that_isnt_empty()
    {
        // German AltGr level: from € left to @, up to ³, down to |, right to ~.
        var rows = Rows("..²³...{[]}\\.", "@.€........~", "............", "|......µ...");

        Assert.Equal((1, 0), KeyGrid.Sideways(rows, 1, 2, -1));
        Assert.Equal((1, 11), KeyGrid.Sideways(rows, 1, 2, 1));
        Assert.Equal((0, 3), KeyGrid.Vertical(rows, 1, 2, -1));
        Assert.Equal((3, 0), KeyGrid.Vertical(rows, 1, 2, 1));
    }

    [Fact]
    public void A_far_key_in_the_next_row_loses_to_one_right_below_in_the_row_after()
    {
        var rows = Rows("a.......", ".......b", "c.......");

        Assert.Equal((2, 0), KeyGrid.Vertical(rows, 0, 0, 1));
        Assert.Equal((0, 0), KeyGrid.Vertical(rows, 2, 0, -1));
        Assert.Equal((0, 0), KeyGrid.Vertical(rows, 1, 7, -1));
    }

    [Fact]
    public void Between_two_keys_equally_near_up_goes_right_and_down_goes_left_so_the_way_back_leads_home()
    {
        var rows = Rows("1234567890abc", "qwertzuiopüd");

        Assert.Equal((0, 3), KeyGrid.Vertical(rows, 1, 2, -1));
        Assert.Equal((1, 2), KeyGrid.Vertical(rows, 0, 3, 1));
    }

    [Fact]
    public void A_row_with_nothing_to_go_to_is_passed_over_and_the_last_row_keeps_the_focus()
    {
        var rows = Rows("ab", "....", "cd");

        Assert.Equal((2, 0), KeyGrid.Vertical(rows, 0, 0, 1));
        Assert.Equal((0, 1), KeyGrid.Vertical(rows, 2, 1, -1));
        Assert.Equal((2, 1), KeyGrid.Vertical(rows, 2, 1, 1));
    }

    [Fact]
    public void The_closest_key_may_lie_in_another_row_and_without_any_there_is_none()
    {
        var rows = Rows("a....", ".....", "....b");

        Assert.Equal((0, 0), KeyGrid.Closest(rows, 1, -2));
        Assert.Equal((2, 4), KeyGrid.Closest(rows, 1, 2));
        Assert.Null(KeyGrid.Closest(Rows("..."), 0, 0));
    }
}
