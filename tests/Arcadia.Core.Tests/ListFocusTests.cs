using Arcadia.Core.Navigation;

namespace Arcadia.Core.Tests;

public sealed class ListFocusTests
{
    [Theory]
    [InlineData(0, -1, 3, 0)]
    [InlineData(0, 1, 3, 1)]
    [InlineData(2, 1, 3, 2)]
    [InlineData(1, 5, 3, 2)]
    [InlineData(0, 1, 0, -1)]
    public void Move_stops_at_both_ends(int index, int delta, int count, int expected) =>
        Assert.Equal(expected, ListFocus.Move(index, delta, count));

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
}
