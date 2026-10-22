using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class FullKeyboardTests
{
    public static TheoryData<string> LayoutIds => [.. KeyboardLayout.All.Select(l => l.Id)];

    [Theory]
    [MemberData(nameof(LayoutIds))]
    public void Every_row_is_15_keys_wide_without_gaps_or_overlaps_and_the_right_block_3(string id)
    {
        var board = FullKeyboard.For(KeyboardLayout.Find(id));

        for (var row = 0; row < 4; row++)
        {
            var keys = board.Rows[row].Where(k => k.Left < FullKeyboard.RightBlockLeft).ToList();
            var left = 0.0;
            foreach (var key in keys)
            {
                var (start, width) = key.Row == row ? (key.Left, key.Width) : (key.LowerLeft!.Value, key.Left + key.Width - key.LowerLeft.Value);
                Assert.Equal(left, start, 3);
                left = start + width;
            }

            Assert.Equal(15, left, 3);
        }

        Assert.All(board.Keys.Where(k => k.Left >= FullKeyboard.RightBlockLeft), k => Assert.True(k.Left + k.Width <= FullKeyboard.Width));
    }

    [Theory]
    [MemberData(nameof(LayoutIds))]
    public void Every_character_of_the_layout_has_one_key_and_the_special_keys_are_all_there(string id)
    {
        var layout = KeyboardLayout.Find(id);
        var board = FullKeyboard.For(layout);

        Assert.Equal(layout.Rows.SelectMany(r => r), board.Keys.Where(k => k.Kind == BoardKeyKind.Character).Select(k => k.Cap));
        Assert.Equal(2, board.Keys.Count(k => k.Kind == BoardKeyKind.Shift));
        foreach (var kind in new[]
        {
            BoardKeyKind.Backspace, BoardKeyKind.Tab, BoardKeyKind.Enter, BoardKeyKind.CapsLock, BoardKeyKind.Space, BoardKeyKind.AltGr,
            BoardKeyKind.Layout, BoardKeyKind.Dock, BoardKeyKind.Hide, BoardKeyKind.Up, BoardKeyKind.Down, BoardKeyKind.Left, BoardKeyKind.Right,
        })
        {
            Assert.Single(board.Keys, k => k.Kind == kind);
        }
    }

    [Fact]
    public void Iso_has_a_tall_enter_and_a_key_between_left_shift_and_the_first_letter()
    {
        var board = FullKeyboard.For(KeyboardLayout.German);
        var enter = Assert.Single(board.Keys, k => k.Kind == BoardKeyKind.Enter);

        Assert.Equal((1, 2), (enter.Row, enter.LastRow));
        Assert.Contains(enter, board.Rows[1]);
        Assert.Contains(enter, board.Rows[2]);
        Assert.Equal(1.25, board.Rows[3][0].Width);
        Assert.Equal('<', board.Rows[3][1].Cap?.Normal);
    }

    [Fact]
    public void Ansi_has_a_wide_enter_in_the_home_row_backslash_above_it_and_a_wide_left_shift()
    {
        var board = FullKeyboard.For(KeyboardLayout.Find("en-us"));
        var enter = Assert.Single(board.Keys, k => k.Kind == BoardKeyKind.Enter);

        Assert.Equal((2, 2, 2.25), (enter.Row, enter.LastRow, enter.Width));
        Assert.Equal('\\', board.Rows[1][13].Cap?.Normal);
        Assert.Equal(1.5, board.Rows[1][13].Width);
        Assert.Equal(2.25, board.Rows[3][0].Width);
        Assert.Equal('z', board.Rows[3][1].Cap?.Normal);
    }

    [Fact]
    public void A_layout_whose_rows_do_not_fit_its_form_fails_at_once()
    {
        var german = KeyboardLayout.German;

        Assert.Throws<ArgumentException>(() => FullKeyboard.For(german with { Form = KeyboardForm.Ansi }));
    }

    // The board as the focus sees it on a level: caps lock and keys without a character there are empty.
    private static List<IReadOnlyList<KeySpot>> Spots(FullKeyboard board, KeyLevel level) =>
        [.. board.Rows.Select((row, r) => (IReadOnlyList<KeySpot>)row
            .Select(k => new KeySpot(
                k.CentreIn(r),
                k.Kind == BoardKeyKind.CapsLock || (k.Kind == BoardKeyKind.Character && k.Cap!.On(level) is null),
                board.Keys.ToList().IndexOf(k),
                k.WidthIn(r)))
            .ToList())];

    private static (int Row, int Column) Find(FullKeyboard board, int row, Func<BoardKey, bool> match) =>
        (row, board.Rows[row].ToList().FindIndex(k => match(k)));

    [Fact]
    public void The_focus_leaves_the_tall_enter_up_and_down_instead_of_landing_on_it_again()
    {
        var board = FullKeyboard.For(KeyboardLayout.German);
        var rows = Spots(board, KeyLevel.Normal);
        var enterTop = Find(board, 1, k => k.Kind == BoardKeyKind.Enter);
        var enterBottom = Find(board, 2, k => k.Kind == BoardKeyKind.Enter);
        var backspace = Find(board, 0, k => k.Kind == BoardKeyKind.Backspace);
        var hash = Find(board, 2, k => k.Cap?.Normal == '#');
        var rightShift = Find(board, 3, k => k.Kind == BoardKeyKind.Shift && k.Left > 1);

        Assert.Equal(enterTop, KeyGrid.Vertical(rows, 0, backspace.Column, 1));
        Assert.Equal(rightShift, KeyGrid.Vertical(rows, 1, enterTop.Column, 1));
        Assert.Equal(backspace, KeyGrid.Vertical(rows, 2, enterBottom.Column, -1));
        Assert.Equal(enterBottom, KeyGrid.Sideways(rows, 2, hash.Column, 1));
        Assert.Equal(hash, KeyGrid.Sideways(rows, 2, enterBottom.Column, -1));
    }

    [Fact]
    public void On_the_german_altgr_level_the_focus_goes_where_the_keys_are_not_to_the_only_key_of_the_next_row()
    {
        // Owner's findings, 2026-10-22: the home row has only Enter on AltGr, the number row nothing left of ².
        var board = FullKeyboard.For(KeyboardLayout.German);
        var rows = Spots(board, KeyLevel.AltGr);
        var tab = Find(board, 1, k => k.Kind == BoardKeyKind.Tab);
        var leftShift = Find(board, 3, k => k.Kind == BoardKeyKind.Shift && k.Left == 0);
        var squared = Find(board, 0, k => k.Cap?.AltGr == '²');
        var tilde = Find(board, 1, k => k.Cap?.AltGr == '~');
        var enterBottom = Find(board, 2, k => k.Kind == BoardKeyKind.Enter);

        Assert.Equal(leftShift, KeyGrid.Vertical(rows, tab.Row, tab.Column, 1));
        Assert.Equal(tab, KeyGrid.Vertical(rows, leftShift.Row, leftShift.Column, -1));
        Assert.Equal(tab, KeyGrid.Sideways(rows, squared.Row, squared.Column, -1));
        Assert.Equal(tilde, KeyGrid.Sideways(rows, enterBottom.Row, enterBottom.Column, -1));
    }

    [Fact]
    public void Left_of_the_first_key_of_a_row_the_focus_goes_to_the_key_beside_it_in_a_neighbouring_row()
    {
        var board = FullKeyboard.For(KeyboardLayout.German);
        var rows = Spots(board, KeyLevel.Normal);
        var a = Find(board, 2, k => k.Cap?.Normal == 'a');
        var tab = Find(board, 1, k => k.Kind == BoardKeyKind.Tab);

        Assert.Equal(tab, KeyGrid.Sideways(rows, a.Row, a.Column, -1));
        Assert.Equal(tab, KeyGrid.Sideways(rows, tab.Row, tab.Column, -1));
    }
}
