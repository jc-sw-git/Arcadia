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

    [Fact]
    public void The_focus_leaves_the_tall_enter_up_and_down_instead_of_landing_on_it_again()
    {
        var board = FullKeyboard.For(KeyboardLayout.German);
        var rows = board.Rows.Select((row, r) => (IReadOnlyList<KeySpot>)row
            .Select(k => new KeySpot(k.CentreIn(r), k.Kind == BoardKeyKind.CapsLock, board.Keys.ToList().IndexOf(k))).ToList()).ToList();
        int Column(int row, Func<BoardKey, bool> match) => board.Rows[row].ToList().FindIndex(k => match(k));
        var enterTop = Column(1, k => k.Kind == BoardKeyKind.Enter);
        var enterBottom = Column(2, k => k.Kind == BoardKeyKind.Enter);
        var backspace = Column(0, k => k.Kind == BoardKeyKind.Backspace);
        var hash = Column(2, k => k.Cap?.Normal == '#');
        var rightShift = Column(3, k => k.Kind == BoardKeyKind.Shift && k.Left > 1);

        Assert.Equal((1, enterTop), KeyGrid.Vertical(rows, 0, backspace, 1));
        Assert.Equal((3, rightShift), KeyGrid.Vertical(rows, 1, enterTop, 1));
        Assert.Equal((0, backspace), KeyGrid.Vertical(rows, 2, enterBottom, -1));
        Assert.Equal(enterBottom, KeyGrid.Sideways(rows[2], hash, 1));
        Assert.Equal(1, KeyGrid.Sideways(rows[2], 1, -1));
    }
}
