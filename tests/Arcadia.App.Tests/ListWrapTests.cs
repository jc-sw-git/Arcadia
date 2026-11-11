using Arcadia.App.Input;
using Arcadia.App.Status;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The lists, menus and dialog buttons beside the library and the search go round at their ends (#476, part 3): a step
/// past the last item goes to the first and back; a held D-pad stops at the end, only a fresh press goes round.
/// </summary>
public sealed class ListWrapTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T476-5")]
    public async Task The_settings_rows_go_round_and_a_held_D_pad_stops_at_the_last_row()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Settings);
        var settings = arcadia.Main.Settings!;
        var rows = settings.Selected.Rows;
        Assert.True(rows.Count > 1);
        Assert.Same(rows[0], settings.FocusedRow);

        arcadia.Press(UiAction.Up);
        Assert.Same(rows[^1], settings.FocusedRow);
        arcadia.Press(UiAction.Down);
        Assert.Same(rows[0], settings.FocusedRow);

        // The press, then a repeat every 90 ms after 400 ms: far more steps than rows, yet it stays on the last one.
        arcadia.HoldPad(TimeSpan.FromSeconds(3), PadButtons.DPadDown);
        Assert.Same(rows[^1], settings.FocusedRow);
        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.DPadDown);
        Assert.Same(rows[0], settings.FocusedRow);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-5")]
    public async Task The_context_menu_goes_round_over_its_entries()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!;
        var enabled = menu.Actions.Where(action => action.IsEnabled).ToList();
        Assert.True(enabled.Count > 1);
        Assert.Same(enabled[0], Focused(menu));

        arcadia.Press(UiAction.Up);
        Assert.Same(enabled[^1], Focused(menu));
        arcadia.Press(UiAction.Down);
        Assert.Same(enabled[0], Focused(menu));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-5")]
    public async Task Ansicht_goes_round_in_its_rows_and_its_columns()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.ViewOptions);
        var options = arcadia.Main.ViewOptions!;
        Assert.Equal((0, 0), options.FocusPosition);

        // Up from "Name A → Z" to the page sizes at the bottom, down back; left round to "Z → A".
        arcadia.Press(UiAction.Up);
        Assert.True(options.PageSizeOptions[0].IsFocused);
        arcadia.Press(UiAction.Down);
        Assert.Equal((0, 0), options.FocusPosition);
        arcadia.Press(UiAction.Left);
        Assert.Equal((0, 1), options.FocusPosition);
        arcadia.Press(UiAction.Right);
        Assert.Equal((0, 0), options.FocusPosition);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-5")]
    public async Task The_detail_pages_buttons_go_round_in_their_row_and_between_the_rows()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var top = page.TopRow.Where(action => action.IsEnabled).ToList();
        Assert.True(top.Count > 1);
        Assert.Equal(top[0].Kind, page.FocusedKind);

        arcadia.Press(UiAction.Left);
        Assert.Equal(top[^1].Kind, page.FocusedKind);
        arcadia.Press(UiAction.Right);
        Assert.Equal(top[0].Kind, page.FocusedKind);

        // Up from the top row goes round to the bottom row, down back to the top.
        arcadia.Press(UiAction.Up);
        Assert.DoesNotContain(top, action => action.Kind == page.FocusedKind);
        arcadia.Press(UiAction.Down);
        Assert.Contains(top, action => action.Kind == page.FocusedKind);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-5")]
    public async Task The_keyboard_layout_menu_goes_round()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Search).Press(UiAction.KeyboardLayout);
        var menu = arcadia.Search!.Keyboard.LayoutMenu;
        Assert.True(menu.IsOpen);
        var focused = menu.Items.ToList().FindIndex(item => item.IsFocused);

        arcadia.Press(UiAction.Down, times: menu.Items.Count);
        Assert.True(menu.Items[focused].IsFocused);
        arcadia.Press(UiAction.Up, times: focused + 1);
        Assert.True(menu.Items[^1].IsFocused);
    }

    [Fact]
    [Trait("TestCase", "T476-5")]
    public void The_status_messages_and_their_two_buttons_go_round()
    {
        var now = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(1));
        StatusEntry Entry(string id, string? fix = null) => new(id, StatusLevel.Warning, id, $"{id} kurz", now, StatusTarget.ControllerIssue, fix);
        var modal = new StatusModalViewModel([Entry("first", fix: "Beheben"), Entry("second"), Entry("third")], newestFirst: true, new NoHost());
        var first = modal.Focused!;

        modal.Handle(UiAction.Up);
        Assert.Same(modal.Entries[^1], modal.Focused);
        modal.Handle(UiAction.Down);
        Assert.Same(first, modal.Focused);
        HeldPress.Run(repeat: true, () => modal.Handle(UiAction.Up));
        Assert.Same(first, modal.Focused);

        // The fix and the settings button side by side: past either one round to the other, a held one stays.
        Assert.True(first.FixChosen);
        modal.Handle(UiAction.Left);
        Assert.False(first.FixChosen);
        modal.Handle(UiAction.Right);
        Assert.True(first.FixChosen);
        HeldPress.Run(repeat: true, () => modal.Handle(UiAction.Left));
        Assert.True(first.FixChosen);
    }

    [Fact]
    [Trait("TestCase", "T476-5")]
    public void A_questions_two_buttons_go_round_and_a_held_arrow_stays()
    {
        var question = new ConfirmationViewModel("Abmelden?", "Wirklich?", "Abmelden", () => { });
        Assert.False(question.IsConfirmFocused);

        question.Answer(UiAction.Right);
        Assert.True(question.IsConfirmFocused);
        question.Answer(UiAction.Left);
        Assert.False(question.IsConfirmFocused);
        HeldPress.Run(repeat: true, () => question.Answer(UiAction.Right));
        Assert.False(question.IsConfirmFocused);
    }

    private static GameActionViewModel? Focused(GameMenuViewModel menu) => menu.Actions.SingleOrDefault(action => action.IsFocused);

    private sealed class NoHost : IStatusModalHost
    {
        public void Close(StatusModalViewModel modal)
        {
        }

        public void OpenSettingsOf(StatusEntry entry, bool startFix)
        {
        }

        public void InstallUpdate()
        {
        }

        public void OrderChanged(bool newestFirst)
        {
        }
    }
}
