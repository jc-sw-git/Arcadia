using Arcadia.App.Filter;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The library's filter by installed state and type (#44 part A, #465), with the controller alone: R3 opens the modal,
/// choices apply when it closes, per tab and saved.
/// </summary>
public sealed class LibraryFilterTests
{
    private static readonly DateTimeOffset Day = ArcadiaDriver.Now.AddDays(-10);

    private static Game Installed(LauncherInfo launcher, string title, GameType type = GameType.Game) =>
        TestGames.Installed(launcher, title, Day) with { Type = type };

    private static Game Missing(LauncherInfo launcher, string title, GameType type = GameType.Game) =>
        TestGames.NotInstalled(launcher, title) with { Type = type };

    /// <summary>Gog: 2 installed games, 2 uninstalled, a tool of each kind and an uninstalled software. Xbox: games only.</summary>
    private static (List<Game> Gog, LauncherSpec[] Specs) Mixed()
    {
        var gog = KnownLaunchers.Gog;
        var games = new List<Game>
        {
            Installed(gog, "Alpha"),
            Installed(gog, "Bravo"),
            Missing(gog, "Charlie"),
            Missing(gog, "Delta"),
            Installed(gog, "Echo Werkzeug", GameType.Tool),
            Missing(gog, "Foxtrot Werkzeug", GameType.Tool),
            Missing(gog, "Golf Software", GameType.Software),
        };
        var xbox = KnownLaunchers.Xbox;
        return (games, [
            new LauncherSpec(gog, games.Count, Library: games),
            new LauncherSpec(xbox, 3, Library: [Installed(xbox, "Kilo"), Missing(xbox, "Lima"), Installed(xbox, "Mike")]),
        ]);
    }

    private static FilterModalViewModel Modal(ArcadiaDriver arcadia) =>
        arcadia.Main.FilterModal ?? throw new InvalidOperationException("Das Filter-Modal ist zu.");

    private static IReadOnlyList<string> Titles(ArcadiaDriver arcadia) => arcadia.PageGames.Select(tile => tile.Title).ToList();

    private static string Focused(FilterModalViewModel modal) =>
        modal.IsResetFocused ? FilterModalViewModel.ResetText
        : modal.IsInValues ? modal.Values.Single(value => value.IsFocused).Label
        : modal.Categories.Single(category => category.IsFocused).Label;

    [AvaloniaFact]
    [Trait("TestCase", "T465-1")]
    public async Task R3_opens_the_modal_on_the_tabs_categories_with_every_entry_counted()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);

        Assert.True(arcadia.Main.Library.CanFilter);
        Assert.False(arcadia.Main.Library.IsFiltered);
        arcadia.Press(UiAction.KeyboardLayout);

        var modal = Modal(arcadia);
        Assert.Equal("GOG Galaxy", modal.LauncherName);
        Assert.Equal([AppTexts.Installed, "Typ"], modal.Categories.Select(category => category.Label));
        Assert.Equal(("7", "von 7 Einträgen"), (modal.HitsText, modal.TotalText));
        // Without a filter "Alle Filter zurücksetzen" is skipped: the focus starts on the first category, its values show.
        Assert.Equal(AppTexts.Installed, Focused(modal));
        Assert.Equal(["Alle 7", "Nur installierte 3", "Nur nicht installierte 4"], modal.Values.Select(value => $"{value.Label} {value.CountText}"));
        Assert.Equal([AppTexts.FilterAll, AppTexts.FilterAllTypes], modal.Categories.Select(category => category.Summary));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-2")]
    [Trait("TestCase", "T465-5")]
    public async Task A_choice_counts_at_once_and_applies_only_when_B_closes_the_modal_on_page_one_with_the_first_hit_focused()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.Right, times: 3);
        arcadia.Press(UiAction.KeyboardLayout);
        var modal = Modal(arcadia);

        arcadia.Press(UiAction.Right).Press(UiAction.Down).Press(UiAction.Accept);

        Assert.Equal("3", modal.HitsText);
        Assert.Equal(AppTexts.FilterOnlyInstalled, modal.Categories[0].Summary);
        Assert.True(modal.Categories[0].IsChosen);
        Assert.Equal(7, arcadia.PageGames.Count);

        // B goes back to the categories, B again closes and applies.
        arcadia.Press(UiAction.Back);
        Assert.Same(modal, arcadia.Main.FilterModal);
        Assert.Equal(AppTexts.FilterHintCloseAndApply, modal.BackText);
        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.FilterModal);
        Assert.Equal(["Alpha", "Bravo", "Echo Werkzeug"], Titles(arcadia));
        Assert.Equal("Alpha", arcadia.FocusedGame?.Title);
        Assert.Equal("1", arcadia.CurrentPage);
        Assert.True(arcadia.Main.Library.IsFiltered);
        Assert.Equal("3 von 7", arcadia.Main.Library.FilterHitsText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-2")]
    public async Task R3_closes_and_applies_from_the_values_too()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down, times: 2).Press(UiAction.Accept);

        arcadia.Press(UiAction.KeyboardLayout);

        Assert.Null(arcadia.Main.FilterModal);
        Assert.Equal(["Charlie", "Delta", "Foxtrot Werkzeug", "Golf Software"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-3")]
    public async Task Categories_are_anded_and_types_ored()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout);
        var modal = Modal(arcadia);

        // "Nur nicht installierte", then into "Typ": "Werkzeuge".
        arcadia.Press(UiAction.Right).Press(UiAction.Down, times: 2).Press(UiAction.Accept).Press(UiAction.Left).Press(UiAction.Down);
        Assert.Equal("Typ", Focused(modal));
        Assert.Equal(["Alle Typen 4", "Spiele 2", "Software 1", "Werkzeuge 1"], modal.Values.Select(value => $"{value.Label} {value.CountText}"));
        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.FilterAllTypes, Focused(modal));
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal("Werkzeuge", Focused(modal));
        Assert.Equal("1", modal.HitsText);
        Assert.Equal("Werkzeuge", modal.Categories[1].Summary);

        // "Software" too: up to the first row, two to the right.
        arcadia.Press(UiAction.Up).Press(UiAction.Right, times: 2).Press(UiAction.Accept);
        Assert.Equal("Software", Focused(modal));
        Assert.Equal("2", modal.HitsText);
        Assert.Equal(("Software, Werkzeuge", "Mehrere"), (modal.Categories[1].Summary, modal.Categories[1].SummaryFallback));
        Assert.False(modal.Values[0].IsSelected);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.Equal(["Foxtrot Werkzeug", "Golf Software"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-7")]
    public async Task Alle_Filter_zuruecksetzen_is_focusable_only_with_a_filter_and_clears_it()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout);
        var modal = Modal(arcadia);
        // Without a filter there is no reset row to go up to: up goes round to the last category (#476).
        arcadia.Press(UiAction.Up);
        Assert.Equal("Typ", Focused(modal));
        arcadia.Press(UiAction.Down);
        Assert.Equal(AppTexts.Installed, Focused(modal));

        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Back).Press(UiAction.Up);
        Assert.True(modal.IsActive);
        Assert.Equal(FilterModalViewModel.ResetText, Focused(modal));
        Assert.Equal(AppTexts.FilterHintReset, modal.AcceptText);
        // The row has no values to go into: ▶ leaves the focus on it, so no second ring shows.
        arcadia.Press(UiAction.Right);
        Assert.False(modal.IsInValues);
        Assert.Equal(FilterModalViewModel.ResetText, Focused(modal));

        arcadia.Press(UiAction.Accept);
        Assert.False(modal.IsActive);
        Assert.Equal(AppTexts.Installed, Focused(modal));
        Assert.Equal("7", modal.HitsText);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.Library.IsFiltered);
        Assert.Equal(7, arcadia.PageGames.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-4")]
    public async Task A_tab_offers_only_what_tells_its_entries_apart_and_without_any_R3_does_nothing()
    {
        var gog = KnownLaunchers.Gog;
        var xbox = KnownLaunchers.Xbox;
        var amazon = KnownLaunchers.Amazon;
        using var arcadia = await ArcadiaDriver.StartAsync([
            new LauncherSpec(gog, 2, Library: [Installed(gog, "Alpha"), Missing(gog, "Bravo")]),
            new LauncherSpec(xbox, 2, Library: [Installed(xbox, "Kilo"), Installed(xbox, "Lima", GameType.Tool)]),
            new LauncherSpec(amazon, 2, Library: [Installed(amazon, "Oscar"), Installed(amazon, "Papa")]),
        ]);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.Equal([AppTexts.Installed], Modal(arcadia).Categories.Select(category => category.Label));
        arcadia.Press(UiAction.Back).Press(UiAction.NextTab).Press(UiAction.KeyboardLayout);
        Assert.Equal(["Typ"], Modal(arcadia).Categories.Select(category => category.Label));
        arcadia.Press(UiAction.Back).Press(UiAction.NextTab);

        Assert.False(arcadia.Main.Library.CanFilter);
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.Null(arcadia.Main.FilterModal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-6")]
    [Trait("TestCase", "T465-5")]
    public async Task A_filter_without_hits_says_so_and_Filter_aendern_opens_the_modal_again()
    {
        var gog = KnownLaunchers.Gog;
        using var arcadia = await ArcadiaDriver.StartAsync([
            new LauncherSpec(gog, 3, Library: [Installed(gog, "Alpha"), Missing(gog, "Bravo"), Installed(gog, "Charlie", GameType.Soundtrack)]),
        ]);
        // Uninstalled soundtracks: there are none.
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down, times: 2).Press(UiAction.Accept)
            .Press(UiAction.Back).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Right, times: 2).Press(UiAction.Accept);
        Assert.Equal("0", Modal(arcadia).HitsText);
        arcadia.Press(UiAction.KeyboardLayout);

        var library = arcadia.Main.Library;
        Assert.True(arcadia.Main.IsNoHitsShown);
        Assert.False(arcadia.Main.AreGamesShown);
        Assert.True(arcadia.Main.IsViewLineShown);
        Assert.Equal("0 von 3", library.FilterHitsText);
        Assert.Equal("GOG Galaxy hat 3 Einträge, der Filter blendet gerade alle aus.", library.FilterHidesAllText);
        Assert.Empty(arcadia.PageGames);
        // The count line keeps counting the whole tab.
        Assert.Equal(3, library.Counts.Parts.Sum(part => int.Parse(part.TotalText, Services.Formatting.Culture)));

        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.FilterModal);
        arcadia.Press(UiAction.Up).Press(UiAction.Accept).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsNoHitsShown);
        Assert.Equal(3, arcadia.PageGames.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-8")]
    public async Task Each_tab_has_its_own_filter_and_gets_it_back_after_a_restart()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);
        Assert.Equal(3, arcadia.PageGames.Count);

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.False(arcadia.Main.Library.IsFiltered);
        Assert.Equal(3, arcadia.PageGames.Count);
        arcadia.Press(UiAction.PreviousTab);

        using var restarted = await arcadia.RestartAsync();
        Assert.Equal("GOG Galaxy", restarted.SelectedTab?.Name);
        Assert.True(restarted.Main.Library.IsFiltered);
        Assert.Equal(["Alpha", "Bravo", "Echo Werkzeug"], Titles(restarted));
        Assert.Equal(InstalledChoice.Installed, restarted.Settings.Current.FilterOf(KnownLaunchers.Gog.Id).Installed);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-9")]
    public async Task The_detail_page_turns_through_the_hits_and_the_search_ignores_the_filter()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);

        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        var detail = arcadia.Detail!;
        Assert.Equal(("Alpha", "1 von 3"), (detail.Current.Tile.Title, detail.PositionText));
        arcadia.Press(UiAction.NextPage, times: 2);
        Assert.Equal("Echo Werkzeug", detail.Current.Tile.Title);
        arcadia.Press(UiAction.Back);

        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;
        search.Type("Charlie");
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Contains(search.Hits, hit => hit.Title == "Charlie");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-12")]
    public async Task A_rescan_while_the_modal_is_open_keeps_its_counts_and_closing_filters_the_new_library()
    {
        var (games, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept);
        var modal = Modal(arcadia);
        Assert.Equal("3", modal.HitsText);

        games[2] = games[2] with { IsInstalled = true };
        await arcadia.RescanAsync();
        Assert.Same(modal, arcadia.Main.FilterModal);
        Assert.Equal("3", modal.HitsText);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.Equal(["Alpha", "Bravo", "Charlie", "Echo Werkzeug"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-11")]
    public async Task A_kind_the_tab_no_longer_holds_drops_out_and_an_emptied_category_filters_nothing()
    {
        var (games, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs, settings: new AppSettings
        {
            PanEnabled = true,
            ActiveLaunchers = specs.Select(spec => spec.Launcher.Id).ToList(),
        }.WithFilter(KnownLaunchers.Gog.Id, GameFilter.Of(InstalledChoice.All, [GameType.Software])));
        Assert.Equal(["Golf Software"], Titles(arcadia));

        games.RemoveAll(game => game.Type == GameType.Software);
        await arcadia.RescanAsync();

        Assert.False(arcadia.Main.Library.IsFiltered);
        Assert.Equal(6, arcadia.PageGames.Count);
        // The saved choice stays, also when the modal is opened and closed meanwhile; with the software back, the filter is back.
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.KeyboardLayout);
        Assert.Equal([GameType.Software], arcadia.Settings.Current.FilterOf(KnownLaunchers.Gog.Id).Types);
        games.Add(Missing(KnownLaunchers.Gog, "Golf Software", GameType.Software));
        await arcadia.RescanAsync();
        Assert.True(arcadia.Main.Library.IsFiltered);
        Assert.Equal(["Golf Software"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-11")]
    public async Task Alle_Filter_zuruecksetzen_clears_what_the_tab_no_longer_holds_too()
    {
        var (games, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs, settings: new AppSettings
        {
            PanEnabled = true,
            ActiveLaunchers = specs.Select(spec => spec.Launcher.Id).ToList(),
        }.WithFilter(KnownLaunchers.Gog.Id, GameFilter.Of(InstalledChoice.Installed, [GameType.Software])));
        games.RemoveAll(game => game.Type == GameType.Software);
        await arcadia.RescanAsync();

        // Still "Nur installierte": the reset row is first, A clears it and the hidden software.
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Up).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);

        Assert.Equal(GameFilter.None, arcadia.Settings.Current.FilterOf(KnownLaunchers.Gog.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-2")]
    public async Task Pages_count_only_the_hits()
    {
        var gog = KnownLaunchers.Gog;
        var library = Enumerable.Range(0, 60)
            .Select(i => i % 2 == 0 ? Installed(gog, $"Spiel {i:00}") : Missing(gog, $"Spiel {i:00}"))
            .ToList();
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(gog, library.Count, Library: library)]);
        Assert.Equal("1–24 von 60", arcadia.Main.Library.RangeText);

        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);

        Assert.Equal("1–24 von 30", arcadia.Main.Library.RangeText);
        Assert.Equal(["1", "2"], arcadia.Main.Library.PageButtons.Select(button => button.Label));
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("25–30 von 30", arcadia.Main.Library.RangeText);
        Assert.All(arcadia.PageGames, tile => Assert.True(tile.Game.IsInstalled));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-13")]
    public async Task A_launcher_switched_off_keeps_its_filter_and_it_applies_again_once_switched_on()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);

        arcadia.Settings.Update(settings => settings with { ActiveLaunchers = [KnownLaunchers.Xbox.Id] });
        using (var withoutGog = await arcadia.RestartAsync())
        {
            Assert.Equal(["Xbox"], withoutGog.Main.LauncherTabs.Items.Select(tab => tab.Name));
            Assert.Equal(InstalledChoice.Installed, withoutGog.Settings.Current.FilterOf(KnownLaunchers.Gog.Id).Installed);
            withoutGog.Settings.Update(settings => settings with { ActiveLaunchers = [KnownLaunchers.Gog.Id, KnownLaunchers.Xbox.Id] });
        }

        using var withGog = await arcadia.RestartAsync();
        Assert.Equal("GOG Galaxy", withGog.SelectedTab?.Name);
        Assert.Equal(["Alpha", "Bravo", "Echo Werkzeug"], Titles(withGog));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T465-10")]
    public async Task The_mouse_picks_categories_and_values_and_the_close_button_applies()
    {
        var (_, specs) = Mixed();
        using var arcadia = await ArcadiaDriver.StartAsync(specs);
        arcadia.Click(arcadia.Main.Library.OpenFilterCommand);
        var modal = Modal(arcadia);

        arcadia.Click(modal.Categories[1].PickCommand);
        Assert.True(modal.ShowsTypes);
        arcadia.Click(modal.Values.Single(value => value.Label == "Werkzeuge").PickCommand);
        Assert.True(modal.Values.Single(value => value.Label == "Werkzeuge").IsSelected);

        // "Alle Filter zurücksetzen" clicked from the values: the focus goes back to the categories, not to hidden values.
        arcadia.Click(modal.ResetCommand);
        Assert.False(modal.IsActive);
        Assert.False(modal.IsInValues);
        Assert.Equal(AppTexts.Installed, Focused(modal));
        arcadia.Click(modal.Categories[1].PickCommand);
        arcadia.Click(modal.Values.Single(value => value.Label == "Werkzeuge").PickCommand);
        arcadia.Click(modal.CloseCommand);

        Assert.Null(arcadia.Main.FilterModal);
        Assert.Equal(["Echo Werkzeug", "Foxtrot Werkzeug"], Titles(arcadia));
    }
}
