using Arcadia.Core.Navigation;

namespace Arcadia.Core.Tests;

public sealed class ListFocusTests
{

    [Fact]
    public void Keep_follows_the_focused_row_to_its_new_place()
    {
        // A problem appeared above the focused row.
        Assert.Equal(2, ListFocus.Keep("mouse", 1, ["sisr", "issue", "mouse"]));
    }

    [Fact]
    public void Keep_stays_at_the_same_place_when_the_focused_row_is_gone()
    {
        // The fixed problem disappears; the row below it takes its place.
        Assert.Equal(1, ListFocus.Keep("issue", 1, ["sisr", "mouse", "help"]));
    }

    [Fact]
    public void Keep_clamps_to_the_last_row_when_the_list_got_shorter()
    {
        Assert.Equal(1, ListFocus.Keep("update", 3, ["version", "check"]));
    }

    [Fact]
    public void Keep_without_rows_or_focus()
    {
        Assert.Equal(-1, ListFocus.Keep("sisr", 0, []));
        Assert.Equal(0, ListFocus.Keep(null, -1, ["sisr"]));
    }

    [Theory]
    [InlineData(0, -1, 3, 2)]
    [InlineData(2, 1, 3, 0)]
    [InlineData(1, 1, 3, 2)]
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, 1, 0, -1)]
    public void Cycle_wraps_round_like_the_launcher_tabs(int index, int delta, int count, int expected) =>
        Assert.Equal(expected, ListFocus.Cycle(index, delta, count));

    /// <summary>#476: a held LB/RB or LT/RT stops at the end once; only a fresh press goes round.</summary>
    [Theory]
    [Trait("TestCase", "T476-9")]
    [InlineData(2, 1, 3, 2)]
    [InlineData(0, -1, 3, 0)]
    [InlineData(1, 1, 3, 2)]
    [InlineData(1, -1, 3, 0)]
    [InlineData(0, 1, 0, -1)]
    public void Cycle_held_stops_at_the_end(int index, int delta, int count, int expected) =>
        Assert.Equal(expected, ListFocus.Cycle(index, delta, count, held: true));

    [Theory]
    [InlineData(-1, 1, 3, 0)]
    [InlineData(-1, -1, 3, 2)]
    public void Cycle_without_a_focus_starts_at_the_first_or_the_last(int index, int delta, int count, int expected) =>
        Assert.Equal(expected, ListFocus.Cycle(index, delta, count));

    /// <summary>#476: a menu with greyed-out entries (here 1 and 3) goes round over them.</summary>
    [Theory]
    [Trait("TestCase", "T476-5")]
    [InlineData(0, 1, false, 2)]
    [InlineData(2, 1, false, 0)]
    [InlineData(0, -1, false, 2)]
    [InlineData(2, 1, true, 2)]
    [InlineData(0, -1, true, 0)]
    [InlineData(-1, 1, false, 0)]
    public void Next_passes_over_items_that_cant_take_the_focus(int index, int delta, bool held, int expected) =>
        Assert.Equal(expected, ListFocus.Next(index, delta, 4, i => i is 0 or 2, held));

    [Fact]
    public void Next_stays_when_nothing_else_can_take_the_focus()
    {
        Assert.Equal(1, ListFocus.Next(1, 1, 3, i => i == 1));
        Assert.Equal(-1, ListFocus.Next(-1, 1, 3, _ => false));
        Assert.Equal(-1, ListFocus.Next(-1, 1, 0, _ => true));
    }
}
