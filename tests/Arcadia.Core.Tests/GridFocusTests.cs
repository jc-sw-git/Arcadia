using Arcadia.Core.Navigation;

namespace Arcadia.Core.Tests;

/// <summary>
/// #476: a step past the end of a paged grid goes round, up/down past a page's edge onto the neighbouring page in the
/// same column; a held step stops at the end. Here: 8 columns, 24 per page (three rows), 60 items, so pages of 24, 24
/// and 12 (one full row and a half one).
/// </summary>
public class GridFocusTests
{
    private const int Count = 60;
    private const int PageSize = 24;
    private const int Columns = 8;

    private static int Step(int index, int columnStep, int rowStep, bool held = false) =>
        GridFocus.Step(index, Count, PageSize, Columns, columnStep, rowStep, held);

    [Theory]
    [Trait("TestCase", "T476-2")]
    [InlineData(5, 1, 6)]
    [InlineData(23, 1, 24)]
    [InlineData(24, -1, 23)]
    [InlineData(59, 1, 0)]
    [InlineData(0, -1, 59)]
    public void Left_and_right_cross_pages_and_go_round_at_the_ends(int index, int columnStep, int expected) =>
        Assert.Equal(expected, Step(index, columnStep, 0));

    [Theory]
    [Trait("TestCase", "T476-4")]
    [InlineData(3, 1, 11)]
    [InlineData(19, 1, 27)] // Bottom row of page 1, column 3 → top row of page 2, column 3.
    [InlineData(27, -1, 19)]
    [InlineData(43, 1, 51)] // Bottom row of page 2 → top row of the last page.
    [InlineData(51, 1, 59)] // Into the last page's half row, column 3.
    [InlineData(54, 1, 59)] // … where column 6 is empty: its last item.
    [InlineData(59, 1, 3)] // Bottom (half) row of the last page → the first page's top row, round.
    [InlineData(1, -1, 57)] // Top row of the first page → the last page's bottom row.
    [InlineData(6, -1, 59)] // … where column 6 is empty: its last item.
    public void Up_and_down_cross_page_edges_in_the_same_column(int index, int rowStep, int expected) =>
        Assert.Equal(expected, Step(index, 0, rowStep));

    [Fact]
    [Trait("TestCase", "T476-4")]
    public void Down_onto_a_partial_last_row_lands_on_the_last_item()
    {
        // 20 items on one page: rows 0, 1 and a row of four; column 6 of row 1 has nothing below it.
        Assert.Equal(19, GridFocus.Step(14, 20, 20, Columns, 0, 1, held: false));
    }

    [Theory]
    [Trait("TestCase", "T476-9")]
    [InlineData(59, 1, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(3, 0, -1)]
    [InlineData(59, 0, 1)]
    public void A_held_step_stops_at_the_end(int index, int columnStep, int rowStep) =>
        Assert.Equal(index, Step(index, columnStep, rowStep, held: true));

    [Fact]
    [Trait("TestCase", "T476-9")]
    public void A_held_step_still_crosses_page_edges_inside_the_list()
    {
        Assert.Equal(24, Step(23, 1, 0, held: true));
        Assert.Equal(27, Step(19, 0, 1, held: true));
    }

    [Fact]
    [Trait("TestCase", "T476-3")]
    public void One_page_goes_round_from_its_last_row_to_its_first_and_back()
    {
        // The search grid: all 17 hits on one page of rows of 7.
        Assert.Equal(3, GridFocus.Step(10, 17, 17, 7, 0, -1, held: false));
        Assert.Equal(16, GridFocus.Step(5, 17, 17, 7, 0, -1, held: false));
        Assert.Equal(2, GridFocus.Step(16, 17, 17, 7, 0, 1, held: false));
    }

    [Fact]
    public void A_single_row_on_a_single_page_has_no_up_or_down()
    {
        Assert.Equal(2, GridFocus.Step(2, 5, 24, Columns, 0, 1, held: false));
        Assert.Equal(2, GridFocus.Step(2, 5, 24, Columns, 0, -1, held: false));
        Assert.Equal(0, GridFocus.Step(4, 5, 24, Columns, 1, 0, held: false));
    }

    [Fact]
    public void An_empty_grid_stays_empty() => Assert.Equal(-1, GridFocus.Step(-1, 0, PageSize, Columns, 1, 0, held: false));
}
