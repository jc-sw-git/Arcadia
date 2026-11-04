using Arcadia.App.Input;
using Arcadia.App.Status;

namespace Arcadia.App.Tests;

/// <summary>The status modal's order and focus (#166) as unit tests: the variants beside the acceptance tests.</summary>
public sealed class StatusModalOrderTests
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(1));

    private static StatusEntry Entry(string id, StatusLevel level, int minute, string? fix = null) =>
        new(id, level, id, $"{id} kurz", Ten.AddMinutes(minute), StatusTarget.ControllerIssue, fix);

    private sealed class Host : IStatusModalHost
    {
        public List<string> Calls { get; } = [];

        public void Close(StatusModalViewModel modal) => Calls.Add("close");

        public void OpenSettingsOf(StatusEntry entry, bool startFix) => Calls.Add($"{(startFix ? "fix" : "settings")}:{entry.Id}");

        public void InstallUpdate() => Calls.Add("update");

        public void OrderChanged(bool newestFirst) => Calls.Add($"order:{newestFirst}");
    }

    private static string[] Ids(StatusModalViewModel modal) => [.. modal.Entries.Select(entry => entry.Id)];

    [Fact]
    [Trait("TestCase", "T166-10")]
    public void Dringlichkeit_puts_error_warning_update_info_and_the_newest_first_within_a_level()
    {
        var modal = new StatusModalViewModel(
            [Entry("info", StatusLevel.Info, 9), Entry("update", StatusLevel.Update, 0), Entry("old-warning", StatusLevel.Warning, 1),
             Entry("new-warning", StatusLevel.Warning, 5), Entry("error", StatusLevel.Error, 2)],
            newestFirst: false, new Host());

        Assert.Equal(["error", "new-warning", "old-warning", "update", "info"], Ids(modal));
        Assert.Equal("error", modal.Focused?.Id);
        Assert.Equal("5 Meldungen", modal.CountText);
    }

    [Fact]
    [Trait("TestCase", "T166-10")]
    public void Neueste_zuerst_goes_by_time_alone_and_L3_tells_the_host()
    {
        var host = new Host();
        var modal = new StatusModalViewModel([Entry("warning", StatusLevel.Warning, 0), Entry("info", StatusLevel.Info, 5)], newestFirst: true, host);
        Assert.Equal(["info", "warning"], Ids(modal));
        Assert.Equal(StatusModalViewModel.NewestText, modal.OrderText);

        modal.Handle(UiAction.ViewOptions);
        Assert.Equal(["warning", "info"], Ids(modal));
        Assert.Equal("info", modal.Focused?.Id);
        Assert.Equal(["order:False"], host.Calls);
    }

    [Fact]
    [Trait("TestCase", "T166-11")]
    public void A_new_entry_never_moves_the_focus_and_a_gone_one_hands_it_to_the_one_now_in_its_place()
    {
        var a = Entry("a", StatusLevel.Warning, 3);
        var b = Entry("b", StatusLevel.Warning, 2);
        var c = Entry("c", StatusLevel.Warning, 1);
        var modal = new StatusModalViewModel([a, b, c], newestFirst: false, new Host());
        modal.Handle(UiAction.Down);
        Assert.Equal("b", modal.Focused?.Id);

        modal.Show([Entry("new", StatusLevel.Error, 9), a, b, c]);
        Assert.Equal("b", modal.Focused?.Id);

        // The focused one goes: the next one takes the focus.
        modal.Show([Entry("new", StatusLevel.Error, 9), a, c]);
        Assert.Equal("c", modal.Focused?.Id);

        // The last of the list goes while focused: the one above it takes the focus.
        modal.Show([Entry("new", StatusLevel.Error, 9), a]);
        Assert.Equal("a", modal.Focused?.Id);

        modal.Show([]);
        Assert.True(modal.IsEmpty);
        Assert.Null(modal.Focused);
        modal.Handle(UiAction.Up);
        modal.Handle(UiAction.Accept);
        Assert.Null(modal.Focused);
    }

    [Fact]
    [Trait("TestCase", "T166-12")]
    public void A_presses_the_chosen_button_and_the_update_s_fix_stays_in_the_modal()
    {
        var host = new Host();
        var update = new StatusEntry("arcadia-update", StatusLevel.Update, "Arcadia 0.3.0 verfügbar", "kurz", Ten, StatusTarget.ArcadiaUpdate, "Jetzt aktualisieren");
        var modal = new StatusModalViewModel([Entry("hidhide", StatusLevel.Warning, 0, "Installieren"), update], newestFirst: false, host);

        modal.Handle(UiAction.Accept);
        modal.Handle(UiAction.Right);
        modal.Handle(UiAction.Accept);
        modal.Handle(UiAction.Down);
        modal.Handle(UiAction.Accept);
        modal.Handle(UiAction.Back);

        Assert.Equal(["fix:hidhide", "settings:hidhide", "update", "close"], host.Calls);
    }

    [Fact]
    [Trait("TestCase", "T166-4")]
    public void A_changed_text_or_level_updates_its_entry_in_place()
    {
        var modal = new StatusModalViewModel([Entry("a", StatusLevel.Warning, 0)], newestFirst: false, new Host());
        var shown = modal.Entries[0];

        modal.Show([Entry("a", StatusLevel.Error, 4) with { Title = "neu" }]);

        Assert.Same(shown, modal.Entries[0]);
        Assert.True(shown.IsError);
        Assert.Equal("neu", shown.Title);
        Assert.Equal("seit 10:04", shown.SinceText);
    }
}
