using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class HitWindowTests
{
    [Fact]
    public void Everything_that_fits_has_no_turning_and_no_neighbours()
    {
        var window = HitWindow.Of(5, rows: 1, rowLength: 7);

        Assert.False(window.CanTurn);
        Assert.Null(window.Before);
        Assert.Null(window.After);
        Assert.Equal((window, 3), window.Turn(1, 3));
    }

    [Fact]
    public void Turning_forward_goes_a_whole_window_on_and_wraps_from_the_last_to_the_first()
    {
        var window = HitWindow.Of(16, rows: 1, rowLength: 7);

        var (second, focus) = window.Turn(1, 2);
        Assert.Equal((7, 9), (second.Start, focus));

        // The last window shows the last seven hits in full.
        var (last, lastFocus) = second.Turn(1, 9);
        Assert.Equal((9, 11), (last.Start, lastFocus));

        var (first, firstFocus) = last.Turn(1, 11);
        Assert.Equal((0, 2), (first.Start, firstFocus));
    }

    [Fact]
    public void Turning_back_from_the_first_window_wraps_to_the_last()
    {
        var (last, focus) = HitWindow.Of(16, rows: 1, rowLength: 7).Turn(-1, 0);

        Assert.Equal((9, 9), (last.Start, focus));
        Assert.Equal(2, last.Turn(-1, 9).Window.Start);
    }

    [Fact]
    public void A_grid_turns_by_whole_rows_and_its_last_window_may_end_in_a_partial_row()
    {
        var window = HitWindow.Of(30, rows: 2, rowLength: 7);

        Assert.Equal(21, window.LastStart);
        var (second, focus) = window.Turn(1, 13);
        Assert.Equal((14, 27), (second.Start, focus));

        // The slot of the focus is empty in the last, partial window: the focus goes to the last hit.
        var (last, lastFocus) = second.Turn(1, 27);
        Assert.Equal((21, 29), (last.Start, lastFocus));
    }

    [Fact]
    public void The_neighbours_beyond_the_edges_wrap_around()
    {
        var first = HitWindow.Of(16, rows: 1, rowLength: 7);
        Assert.Equal((15, 7), (first.Before, first.After));

        var last = first with { Start = 9 };
        Assert.Equal((8, 0), (last.Before, last.After));
    }

    [Fact]
    public void Steps_stop_at_the_first_and_last_hit_and_drag_a_single_row_along_by_one()
    {
        var window = HitWindow.Of(10, rows: 1, rowLength: 7);

        Assert.Equal((window, 0), window.Move(0, -1, 0));

        var (moved, focus) = window.Move(6, 1, 0);
        Assert.Equal((1, 7), (moved.Start, focus));

        var (end, endFocus) = (moved with { Start = 3 }).Move(9, 1, 0);
        Assert.Equal((3, 9), (end.Start, endFocus));
    }

    [Fact]
    public void Up_and_down_in_a_grid_move_by_a_row_and_only_where_there_is_one()
    {
        var window = HitWindow.Of(17, rows: 2, rowLength: 7);

        Assert.Equal(10, window.Move(3, 0, 1).Focus);
        Assert.Equal(3, window.Move(3, 0, -1).Focus);

        // Row three exists with three hits: down from column five lands on the last hit, a row further on.
        var (moved, focus) = window.Move(12, 0, 1);
        Assert.Equal((16, 7), (focus, moved.Start));
        Assert.Equal(16, moved.Move(16, 0, 1).Focus);
    }

    [Fact]
    public void The_wheel_moves_without_wrapping()
    {
        var window = HitWindow.Of(16, rows: 1, rowLength: 7);

        Assert.Equal(0, window.Scroll(-1).Start);
        Assert.Equal(3, window.Scroll(3).Start);
        Assert.Equal(9, window.Scroll(20).Start);
        Assert.Equal(14, HitWindow.Of(30, rows: 2, rowLength: 7).Scroll(2).Start);
    }

    [Fact]
    public void Fewer_hits_pull_the_window_back_into_range()
    {
        var window = HitWindow.Of(30, rows: 1, rowLength: 7) with { Start = 20 };

        Assert.Equal(3, window.WithCount(10).Start);
        Assert.Equal(0, window.WithCount(4).Start);
    }

    [Fact]
    public void A_new_layout_keeps_the_focus_in_view_and_a_grid_on_whole_rows()
    {
        var row = HitWindow.Of(30, rows: 1, rowLength: 7) with { Start = 10 };

        var grid = row.WithLayout(rows: 2, rowLength: 7, focus: 16);
        Assert.Equal(7, grid.Start);
        Assert.True(grid.Contains(16));

        var back = grid.WithLayout(rows: 1, rowLength: 7, focus: 16);
        Assert.Equal(10, back.Start);
        Assert.True(back.Contains(16));
    }
}
