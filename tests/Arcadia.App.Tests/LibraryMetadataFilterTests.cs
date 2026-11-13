using Arcadia.App.Filter;
using Arcadia.App.Input;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// The library's filter by metadata (#466, part B of #44), with the controller alone: genre, tags, developer, publisher,
/// release year and age rating in the modal, from a made-up launcher's local metadata.
/// </summary>
public sealed class LibraryMetadataFilterTests
{
    private static readonly DateTimeOffset Day = ArcadiaDriver.Now.AddDays(-10);

    private sealed record Entry(string Title, GameMetadata Metadata, bool Installed = false);

    private static GameMetadata Meta(
        Genre[]? genres = null, string[]? tags = null, string? developer = null, string? publisher = null, int? year = null, params AgeRating[] ratings) => new()
    {
        Genres = genres ?? [],
        Tags = tags ?? [],
        Developers = developer is null ? [] : [developer],
        Publishers = publisher is null ? [] : [publisher],
        ReleaseDate = year is { } y ? new DateOnly(y, 6, 1) : null,
        AgeRatings = ratings,
    };

    /// <summary>Ids of digits only are store pages to load (<see cref="FakeStorePage"/>); others have none.</summary>
    private static Game GameOf(Entry entry, string id) => (entry.Installed
        ? TestGames.Installed(KnownLaunchers.Steam, entry.Title, Day)
        : TestGames.NotInstalled(KnownLaunchers.Steam, entry.Title)) with { Id = id };

    /// <summary>A Steam tab whose games bring <paramref name="entries"/>' metadata from the launcher's own files.</summary>
    private static (LauncherSpec Spec, FakeMetadataSource Source, List<Game> Games) SteamWith(params Entry[] entries) => SteamWith(withStorePages: false, entries);

    private static (LauncherSpec Spec, FakeMetadataSource Source, List<Game> Games) SteamWith(bool withStorePages, params Entry[] entries)
    {
        var source = new FakeMetadataSource();
        var games = new List<Game>();
        foreach (var entry in entries)
        {
            var game = GameOf(entry, withStorePages ? (4100000 + games.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) : "id-" + entry.Title.ToLowerInvariant());
            source.Local[game.Id] = entry.Metadata;
            games.Add(game);
        }

        return (LauncherSpec.With(KnownLaunchers.Steam, [.. games]) with { Metadata = source }, source, games);
    }

    /// <summary>Six games: genres, tags, developers in two spellings, years and USK / PEGI ratings; "Ohne" has nothing.</summary>
    private static Entry[] Library() =>
    [
        new("Asche", Meta([Genre.Shooter], ["Koop", "Horror"], "CD PROJEKT RED", "Nordlicht", 2024, new AgeRating(AgeRatingBoard.Usk, "16")), Installed: true),
        new("Brandung", Meta([Genre.RolePlaying], ["Koop", "Gemütlich"], "CD PROJEKT RED", "Nordlicht", 2015, new AgeRating(AgeRatingBoard.Usk, "12"))),
        new("Chrom", Meta([Genre.Shooter], ["Erkundung"], "CD Projekt Red ", "Seewind", 2008, new AgeRating(AgeRatingBoard.Pegi, "18"))),
        new("Dorf", Meta([Genre.Simulation], ["Gemütlich"], "Ubisoft Montreal", "Seewind", 1998, new AgeRating(AgeRatingBoard.Usk, "0")), Installed: true),
        new("Eiland", Meta([Genre.Puzzle], ["Gemütlich"], "Ubisoft", "Seewind", 2026)),
        new("Ohne", Meta()),
    ];

    /// <summary>Lets fake time pass until the metadata run is idle, the UI thread following.</summary>
    private static async Task RunToIdleAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 3000 && !arcadia.Metadata.WhenIdleAsync().IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromSeconds(1));
        }

        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<ArcadiaDriver> StartAsync(LauncherSpec spec, SavedFilter? saved = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync([spec], settings: new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Steam.Id],
            LibraryFilters = saved is null ? new Dictionary<string, SavedFilter>() : new Dictionary<string, SavedFilter> { [KnownLaunchers.Steam.Id] = saved },
        });
        await RunToIdleAsync(arcadia);
        return arcadia;
    }

    private static FilterModalViewModel Modal(ArcadiaDriver arcadia) =>
        arcadia.Main.FilterModal ?? throw new InvalidOperationException("Das Filter-Modal ist zu.");

    private static IReadOnlyList<string> Titles(ArcadiaDriver arcadia) => arcadia.PageGames.Select(tile => tile.Title).ToList();

    /// <summary>R3, down the categories to <paramref name="category"/>, ▶ into its values.</summary>
    private static FilterModalViewModel OpenAt(ArcadiaDriver arcadia, string category)
    {
        if (arcadia.Main.FilterModal is null)
        {
            arcadia.Press(UiAction.KeyboardLayout);
        }

        var modal = Modal(arcadia);
        for (var i = 0; i < 20 && !modal.Categories.Any(row => row.IsFocused && row.Label == category); i++)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.Equal(category, modal.Current.Label);
        arcadia.Press(UiAction.Right);
        Assert.True(modal.IsInValues);
        return modal;
    }

    private static FilterListPane List(FilterModalViewModel modal) => modal.ListPane ?? throw new InvalidOperationException("Keine Liste.");

    private static string? FocusedValue(FilterModalViewModel modal) =>
        List(modal).VisibleRows.SelectMany(row => row.Cells).SingleOrDefault(cell => cell.IsFocused)?.Label;

    /// <summary>The list's values in their order, "Label Count", headings as they are; only the rows in sight.</summary>
    private static IReadOnlyList<string> Shown(FilterModalViewModel modal) =>
        [.. List(modal).VisibleRows.SelectMany(row => row.IsHeading ? [row.HeadingText!] : row.Cells.Select(cell => $"{cell.Label} {cell.CountText}"))];

    /// <summary>Moves the focus with the D-pad to <paramref name="label"/> in the list, row by row, and presses A.</summary>
    private static void Choose(ArcadiaDriver arcadia, string label)
    {
        var modal = Modal(arcadia);
        for (var i = 0; i < 200; i++)
        {
            if (FocusedValue(modal) == label)
            {
                arcadia.Press(UiAction.Accept);
                return;
            }

            var row = List(modal).VisibleRows.FirstOrDefault(row => row.Cells.Any(cell => cell.IsFocused));
            arcadia.Press(row is not null && row.Cells.Any(cell => cell.Label == label) ? UiAction.Right : UiAction.Down);
        }

        throw new InvalidOperationException($"\"{label}\" ist nicht in der Liste.");
    }

    private static FilterCategoryViewModel Category(FilterModalViewModel modal, string label) => modal.Categories.Single(row => row.Label == label);

    [AvaloniaFact]
    [Trait("TestCase", "T466-1")]
    [Trait("TestCase", "T466-17")]
    public async Task Each_value_counts_its_hits_with_the_other_categories_and_values_without_hits_come_last_greyed()
    {
        var (spec, _, _) = SteamWith(Library());
        // Genre "Shooter" was chosen before: the tags count within the shooters.
        using var arcadia = await StartAsync(spec, new SavedFilter(Genres: new SavedNames(["Shooter"])));

        var modal = OpenAt(arcadia, "Tags");

        Assert.Equal(["Genre", "Tags", "Entwickler", "Publisher", "Erscheinungsjahr", "Altersfreigabe"], modal.Categories.Skip(1).Select(row => row.Label));
        // Equal hits: the value more entries have comes first, then A–Z.
        Assert.Equal(["Alle Tags 2", "Koop 1", "Erkundung 1", "Horror 1", "Gemütlich 0"], Shown(modal));
        var noHits = List(modal).VisibleRows.SelectMany(row => row.Cells).Single(cell => cell.Label == "Gemütlich");
        Assert.True(noHits.HasNoHits);

        // Still choosable: a value without hits gives the no-hits state.
        Choose(arcadia, "Gemütlich");
        Assert.Equal("0", modal.HitsText);
        Assert.True(Category(modal, "Tags").IsChosen);

        // "Alle Tags" clears the category again.
        arcadia.Press(UiAction.Up, times: 3);
        Choose(arcadia, "Alle Tags");
        Assert.False(Category(modal, "Tags").IsChosen);
        Assert.Equal("2", modal.HitsText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-17")]
    public async Task A_tab_offers_only_the_categories_its_entries_have()
    {
        var (spec, _, _) = SteamWith(
            new Entry("Asche", Meta([Genre.Shooter], year: 2024)),
            new Entry("Brandung", Meta([Genre.RolePlaying])));
        using var arcadia = await StartAsync(spec);

        arcadia.Press(UiAction.KeyboardLayout);

        // Neither installed nor types to tell apart: only the metadata the entries have.
        Assert.Equal(["Genre", "Erscheinungsjahr"], Modal(arcadia).Categories.Select(row => row.Label));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-5")]
    public async Task Spellings_of_one_developer_are_one_value_in_the_most_frequent_one_and_other_names_stay_apart()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);

        var modal = OpenAt(arcadia, "Entwickler");

        Assert.Equal(["Alle Entwickler 6", "CD PROJEKT RED 3", "Ubisoft 1", "Ubisoft Montreal 1"], Shown(modal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-2")]
    public async Task L3_switches_the_order_per_category_and_genre_starts_alphabetical_with_its_letters()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);

        var modal = OpenAt(arcadia, "Genre");
        Assert.True(List(modal).IsAlphabetical);
        Assert.Equal(["P", "R", "S"], List(modal).Letters.Select(letter => letter.Letter));
        Assert.Equal(["Alle Genres 6", "Puzzle 1", "Rollenspiel 1", "Shooter 2", "Simulation 1"], Shown(modal));

        arcadia.Press(UiAction.ViewOptions);
        Assert.False(List(modal).IsAlphabetical);
        Assert.Equal(["Alle Genres 6", "Shooter 2", "Puzzle 1", "Rollenspiel 1", "Simulation 1"], Shown(modal));

        // Tags start with "Häufigste zuerst"; the genre keeps its order while the modal is open.
        arcadia.Press(UiAction.Back);
        modal = OpenAt(arcadia, "Tags");
        Assert.False(List(modal).IsAlphabetical);
        arcadia.Press(UiAction.Back).Press(UiAction.Up);
        modal = OpenAt(arcadia, "Genre");
        Assert.False(List(modal).IsAlphabetical);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-2")]
    public async Task In_a_to_z_lt_and_rt_jump_by_letter()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Genre");

        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Puzzle", FocusedValue(modal));
        arcadia.Press(UiAction.NextPage, times: 2);
        Assert.Equal("Shooter", FocusedValue(modal));
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("Rollenspiel", FocusedValue(modal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-3")]
    [Trait("TestCase", "T466-22")]
    public async Task X_searches_the_values_r3_stays_the_keyboards_and_b_goes_back_a_level_at_a_time()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Tags");

        arcadia.Press(UiAction.Search);
        Assert.True(modal.IsWriting);
        var editor = modal.Editor!;
        editor.Type("ko");
        Assert.Equal(["Koop 2"], Shown(modal));

        // R3 is the keyboard's layout menu while it is open, not the modal's close; B closes the menu first.
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.Same(modal, arcadia.Main.FilterModal);
        arcadia.Press(UiAction.Back);
        Assert.True(modal.IsWriting);

        // B puts the keyboard away, the narrowed list stays; A on a hit chooses it.
        arcadia.Press(UiAction.Back);
        Assert.False(modal.IsWriting);
        Assert.Equal(["Koop 2"], Shown(modal));
        arcadia.Press(UiAction.Accept);
        Assert.Equal("2", modal.HitsText);

        // B to the categories clears the search; B again closes and applies.
        arcadia.Press(UiAction.Back);
        Assert.False(modal.IsInValues);
        Assert.Equal(string.Empty, List(modal).Search);
        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.FilterModal);
        Assert.Equal(["Asche", "Brandung"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-4")]
    public async Task Own_tags_come_first_as_their_own_group_and_count_apart_from_a_providers_tag_of_the_same_name()
    {
        var (spec, _, games) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        // "Horror" as an own tag on Dorf, the provider's "Horror" is on Asche.
        arcadia.Main.Tags.Create("Horror", games[3].Key);
        arcadia.Main.Tags.Create("Unbenutzt");

        var modal = OpenAt(arcadia, "Tags");

        Assert.Equal(["Alle Tags 6", AppTexts.FilterOwnTagsHeading, "Horror 1", AppTexts.FilterOtherTagsHeading, "Gemütlich 3", "Koop 2", "Erkundung 1"],
            Shown(modal).Take(7));
        Choose(arcadia, "Horror");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);

        Assert.Equal(["Dorf"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-7")]
    public async Task Values_of_one_category_are_ored_and_categories_anded_with_the_installed_state()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);

        var modal = OpenAt(arcadia, "Genre");
        Choose(arcadia, "Shooter");
        Choose(arcadia, "Rollenspiel");
        Assert.Equal("3", modal.HitsText);
        Assert.Equal("Shooter, Rollenspiel", Category(modal, "Genre").Summary);

        arcadia.Press(UiAction.Back);
        OpenAt(arcadia, "Entwickler");
        Choose(arcadia, "CD PROJEKT RED");
        Assert.Equal("3", modal.HitsText);

        arcadia.Press(UiAction.Back).Press(UiAction.Up, times: 3);
        OpenAt(arcadia, AppTexts.Installed);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);

        Assert.Equal(["Asche"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-8")]
    [Trait("TestCase", "T466-9")]
    public async Task The_switch_shows_once_a_value_is_chosen_and_adds_the_entries_without_one()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Genre");
        Assert.False(List(modal).ShowsWithUnknown);

        Choose(arcadia, "Shooter");
        Assert.True(List(modal).ShowsWithUnknown);
        Assert.False(List(modal).IsWithUnknown);
        Assert.Equal("Zeigt zusätzlich Einträge ohne Genre.", List(modal).WithUnknownInfo);

        // The switch is above the list: ▲ from the first row reaches it.
        while (!List(modal).IsWithUnknownFocused)
        {
            arcadia.Press(UiAction.Up);
        }

        arcadia.Press(UiAction.Accept);
        Assert.Equal("3", modal.HitsText);
        Assert.Equal("Shooter & o. Angabe", Category(modal, "Genre").Summary);
        Assert.Equal("Mehrere & o. Angabe", Category(modal, "Genre").SummaryFallback);

        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Asche", "Chrom", "Ohne"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-8")]
    public async Task The_switchs_explanation_names_the_category_and_the_chosen_rating_system()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Tags");
        Assert.Equal("Zeigt zusätzlich Einträge ohne Tags.", List(modal).WithUnknownInfo);
        arcadia.Press(UiAction.Back);
        modal = OpenAt(arcadia, "Erscheinungsjahr");
        Assert.Equal("Zeigt zusätzlich Einträge ohne Erscheinungsjahr.", modal.YearPane!.WithUnknownInfo);
        arcadia.Press(UiAction.Back);
        modal = OpenAt(arcadia, "Altersfreigabe");
        Assert.Equal("Zeigt zusätzlich Einträge ohne USK-Freigabe.", modal.RatingPane!.WithUnknownInfo);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-10")]
    public async Task The_choice_is_saved_genres_by_key_and_applies_again_after_a_restart()
    {
        var (spec, _, games) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        arcadia.Main.Tags.Create("Liebling", games[0].Key);
        var modal = OpenAt(arcadia, "Genre");
        Choose(arcadia, "Shooter");
        arcadia.Press(UiAction.Back);
        OpenAt(arcadia, "Tags");
        Choose(arcadia, "Liebling");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Asche"], Titles(arcadia));

        var saved = arcadia.Settings.Current.LibraryFilters[KnownLaunchers.Steam.Id];
        Assert.Equal(["Shooter"], saved.Genres!.Names);
        Assert.Equal(["Liebling"], saved.Tags!.Own);
        Assert.Contains("\"Shooter\"", File.ReadAllText(arcadia.SettingsPath));

        using var restarted = await arcadia.RestartAsync();
        await RunToIdleAsync(restarted);
        Assert.Equal(["Asche"], Titles(restarted));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-11")]
    public async Task The_years_step_by_one_and_ten_within_the_tabs_years_and_von_never_passes_bis()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Erscheinungsjahr");
        var year = modal.YearPane!;
        Assert.Equal(("1998", "2026"), (year.FromText, year.ToText));
        Assert.True(year.IsFromFocused);

        arcadia.Press(UiAction.Left);
        Assert.Equal("1998", year.FromText);
        arcadia.Press(UiAction.NextPage).Press(UiAction.Right, times: 2);
        Assert.Equal("2010", year.FromText);

        arcadia.Press(UiAction.Down).Press(UiAction.PreviousPage);
        Assert.Equal("2016", year.ToText);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("2010", year.ToText);
        Assert.Equal("2010", year.FromText);

        arcadia.Press(UiAction.Right, times: 5);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Brandung"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-12")]
    public async Task Quick_picks_cover_the_last_three_years_and_the_decades_and_one_without_hits_is_greyed()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Erscheinungsjahr");
        var year = modal.YearPane!;
        arcadia.Press(UiAction.Up);
        Assert.Equal(["Alle Jahre", "Letzte 3 Jahre", "2020er", "2010er", "2000er", "Vor 2000"], year.Picks.Select(chip => chip.Label));
        Assert.All(year.Picks, chip => Assert.False(chip.HasNoHits));

        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal(("2024", "2026"), (year.FromText, year.ToText));
        Assert.Equal("2", modal.HitsText);

        arcadia.Press(UiAction.Right, times: 4).Press(UiAction.Accept);
        Assert.Equal("Vor 2000", Category(modal, "Erscheinungsjahr").Summary);
        Assert.Equal("1", modal.HitsText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-12")]
    public async Task A_quick_pick_without_hits_is_greyed_and_still_choosable()
    {
        var (spec, _, _) = SteamWith(
            new Entry("Asche", Meta(year: 2024)),
            new Entry("Brandung", Meta(year: 1995)));
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Erscheinungsjahr");
        var tens = modal.YearPane!.Picks.Single(chip => chip.Label == "2010er");
        Assert.True(tens.HasNoHits);

        arcadia.Press(UiAction.Up).Press(UiAction.Right, times: 3).Press(UiAction.Accept);
        Assert.Equal("0", modal.HitsText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-13")]
    [Trait("TestCase", "T466-14")]
    public async Task Ratings_of_the_system_are_ored_and_switching_the_system_clears_them()
    {
        var (spec, _, _) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Altersfreigabe");
        var rating = modal.RatingPane!;
        Assert.Equal(["USK", "PEGI"], rating.Systems.Select(chip => chip.Label));
        Assert.Equal(AgeRatingBoard.Usk, rating.Board);
        Assert.Equal(["Alle", "0", "6", "12", "16", "18"], rating.Ratings.Select(tile => tile.Value));

        arcadia.Press(UiAction.Right, times: 3).Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal("2", modal.HitsText);
        Assert.Equal("USK 12, 16", Category(modal, "Altersfreigabe").Summary);

        // Under USK the PEGI-only "Chrom" is "ohne Angabe": hidden, shown with the switch (with "Eiland" and "Ohne").
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal("5", modal.HitsText);

        // PEGI: the USK choice is gone, PEGI starts empty.
        arcadia.Press(UiAction.Up, times: 2).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal(AgeRatingBoard.Pegi, rating.Board);
        Assert.Equal("6", modal.HitsText);
        Assert.False(Category(modal, "Altersfreigabe").IsChosen);
        Assert.Equal(["Alle", "3", "7", "12", "16", "18"], rating.Ratings.Select(tile => tile.Value));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-15")]
    public async Task A_tab_with_only_pegi_starts_on_pegi_and_one_without_ratings_has_no_age_rating()
    {
        var (spec, _, _) = SteamWith(
            new Entry("Asche", Meta([Genre.Shooter], ratings: new AgeRating(AgeRatingBoard.Pegi, "16"))),
            new Entry("Brandung", Meta([Genre.Shooter])));
        using var arcadia = await StartAsync(spec);
        var modal = OpenAt(arcadia, "Altersfreigabe");
        Assert.Equal(["PEGI"], modal.RatingPane!.Systems.Select(chip => chip.Label));
        Assert.Equal(AgeRatingBoard.Pegi, modal.RatingPane.Board);

        var (unrated, _, _) = SteamWith(new Entry("Chrom", Meta([Genre.Shooter])));
        using var other = await StartAsync(unrated);
        other.Press(UiAction.KeyboardLayout);
        Assert.DoesNotContain(Modal(other).Categories, row => row.Label == "Altersfreigabe");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-18")]
    public async Task Metadata_arriving_while_a_filter_applies_adds_its_hits_without_a_key_press()
    {
        var (spec, source, games) = SteamWith(Library());
        using var arcadia = await StartAsync(spec, new SavedFilter(Genres: new SavedNames(["Shooter"])));
        Assert.Equal(["Asche", "Chrom"], Titles(arcadia));

        // A later run gives "Eiland" the genre too.
        source.Local[games[4].Id] = Meta([Genre.Shooter], year: 2026);
        await arcadia.RescanAsync();
        await RunToIdleAsync(arcadia);

        Assert.Equal(["Asche", "Chrom", "Eiland"], Titles(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-18")]
    public async Task When_the_focused_game_no_longer_matches_the_focus_goes_to_the_next_hit()
    {
        var (spec, source, games) = SteamWith(Library());
        using var arcadia = await StartAsync(spec, new SavedFilter(Genres: new SavedNames(["Shooter"])));
        Assert.Equal("Asche", arcadia.FocusedGame!.Title);

        source.Local[games[0].Id] = Meta([Genre.Puzzle]);
        await arcadia.RescanAsync();
        await RunToIdleAsync(arcadia);

        Assert.Equal(["Chrom"], Titles(arcadia));
        Assert.Equal("Chrom", arcadia.FocusedGame!.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-19")]
    public async Task The_modal_opened_during_a_metadata_run_says_so_until_the_run_ends()
    {
        var (spec, _, _) = SteamWith(withStorePages: true, Library());
        using var arcadia = await ArcadiaDriver.StartAsync([spec], settings: new AppSettings { ActiveLaunchers = [KnownLaunchers.Steam.Id] },
            stores: store => store.ServeEverySlowly("Eine Beschreibung, die lang genug ist, um als Beschreibung zu gelten.", TimeSpan.FromMinutes(1)));
        // The local data is in, the store pages still load.
        for (var i = 0; i < 50 && !arcadia.Metadata.Progress.Running; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
        }

        arcadia.Press(UiAction.KeyboardLayout);
        var modal = Modal(arcadia);
        Assert.True(modal.IsMetadataLoading);

        await RunToIdleAsync(arcadia);
        Assert.False(modal.IsMetadataLoading);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-20")]
    public async Task A_chosen_own_tag_follows_a_rename_and_drops_out_when_deleted()
    {
        var (spec, _, games) = SteamWith(Library());
        using var arcadia = await StartAsync(spec);
        arcadia.Main.Tags.Create("Grusel", games[1].Key);
        OpenAt(arcadia, "Tags");
        Choose(arcadia, "Grusel");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Brandung"], Titles(arcadia));

        arcadia.Main.Tags.Rename("Grusel", "Horror");
        Assert.Equal(["Horror"], arcadia.Settings.Current.LibraryFilters[KnownLaunchers.Steam.Id].Tags!.Own);
        Assert.Equal(["Brandung"], Titles(arcadia));

        arcadia.Main.Tags.Delete("Horror");
        Assert.False(arcadia.Main.Library.IsFiltered);
        Assert.Equal(6, Titles(arcadia).Count);
    }
}
