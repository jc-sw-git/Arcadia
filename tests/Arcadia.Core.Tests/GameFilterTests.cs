using System.Diagnostics;
using System.Text.Json;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

/// <summary>The library's filter (#44 part A, #465): installed state and type, AND between them, OR within a category.</summary>
public sealed class GameFilterTests
{
    private static Game Entry(string id, GameType type = GameType.Game, bool installed = true) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = id,
        Type = type,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    private static readonly IReadOnlyList<Game> Tab =
    [
        Entry("game-installed"),
        Entry("game-missing", installed: false),
        Entry("tool-installed", GameType.Tool),
        Entry("tool-missing", GameType.Tool, installed: false),
        Entry("software-missing", GameType.Software, installed: false),
        Entry("soundtrack-installed", GameType.Soundtrack),
    ];

    private static IEnumerable<string> Ids(IEnumerable<Game> games) => games.Select(game => game.Id);

    [Fact]
    public void No_filter_lets_every_entry_through_as_the_same_list()
    {
        Assert.False(GameFilter.None.IsActive);
        Assert.Same(Tab, GameFilter.None.Apply(Tab));
    }

    [Fact]
    public void Installed_choices_let_through_only_their_entries()
    {
        var installed = GameFilter.None.WithInstalled(InstalledChoice.Installed);
        var missing = GameFilter.None.WithInstalled(InstalledChoice.NotInstalled);

        Assert.Equal(["game-installed", "tool-installed", "soundtrack-installed"], Ids(installed.Apply(Tab)));
        Assert.Equal(["game-missing", "tool-missing", "software-missing"], Ids(missing.Apply(Tab)));
    }

    [Fact]
    public void Types_within_the_category_are_ored_and_the_categories_anded()
    {
        var toolsOrSoftware = GameFilter.None.WithType(GameType.Tool, true).WithType(GameType.Software, true);
        var missingTools = GameFilter.None.WithInstalled(InstalledChoice.NotInstalled).WithType(GameType.Tool, true);

        Assert.Equal(["tool-installed", "tool-missing", "software-missing"], Ids(toolsOrSoftware.Apply(Tab)));
        Assert.Equal(["tool-missing"], Ids(missingTools.Apply(Tab)));
    }

    [Fact]
    public void Choosing_a_type_twice_or_in_another_order_gives_the_same_filter()
    {
        var a = GameFilter.None.WithType(GameType.Soundtrack, true).WithType(GameType.Tool, true).WithType(GameType.Tool, true);
        var b = GameFilter.Of(InstalledChoice.All, [GameType.Tool, GameType.Soundtrack]);

        Assert.Equal(b, a);
        Assert.Equal(b.GetHashCode(), a.GetHashCode());
        Assert.Equal([GameType.Tool, GameType.Soundtrack], a.Types);
        Assert.Equal(GameFilter.None, a.WithType(GameType.Tool, false).WithType(GameType.Soundtrack, false));
        Assert.Equal(GameFilter.None, a.WithAllTypes());
    }

    [Fact]
    public void Counts_leave_out_their_own_category()
    {
        var filter = GameFilter.None.WithInstalled(InstalledChoice.NotInstalled).WithType(GameType.Tool, true);

        var counts = filter.Count(Tab);

        Assert.Equal(1, counts.Hits);
        Assert.Equal(6, counts.Total);
        // "Installiert" counts among the tools: 2 tools, 1 of them installed.
        Assert.Equal(2, counts.Of(InstalledChoice.All));
        Assert.Equal(1, counts.Of(InstalledChoice.Installed));
        Assert.Equal(1, counts.Of(InstalledChoice.NotInstalled));
        // "Typ" counts among the uninstalled entries.
        Assert.Equal(3, counts.WithInstalled);
        Assert.Equal(1, counts.Of(GameType.Game));
        Assert.Equal(1, counts.Of(GameType.Tool));
        Assert.Equal(1, counts.Of(GameType.Software));
        Assert.Equal(0, counts.Of(GameType.Soundtrack));
    }

    [Fact]
    public void A_tab_offers_only_what_tells_its_entries_apart()
    {
        var mixed = FilterOffer.Of(Tab);
        var onlyGames = FilterOffer.Of([Entry("a"), Entry("b", installed: false)]);
        var allInstalledTools = FilterOffer.Of([Entry("a", GameType.Tool), Entry("b", GameType.Tool)]);

        Assert.True(mixed.HasInstalled);
        Assert.Equal([GameType.Game, GameType.Software, GameType.Tool, GameType.Soundtrack], mixed.Types);
        Assert.True(mixed.HasTypes);
        Assert.True(onlyGames.HasInstalled);
        Assert.False(onlyGames.HasTypes);
        Assert.False(allInstalledTools.HasAny);
        Assert.False(FilterOffer.Of([]).HasAny);
    }

    [Fact]
    public void What_the_tab_no_longer_offers_drops_out_and_nothing_left_filters_nothing()
    {
        var saved = GameFilter.Of(InstalledChoice.Installed, [GameType.Soundtrack, GameType.Tool]);
        var withoutSoundtracks = new FilterOffer(true, [GameType.Game, GameType.Tool]);
        var onlyGames = new FilterOffer(false, [GameType.Game]);

        Assert.Equal(GameFilter.Of(InstalledChoice.Installed, [GameType.Tool]), saved.Within(withoutSoundtracks));
        Assert.Equal(GameFilter.None, saved.Within(onlyGames));
    }

    [Fact]
    public void A_choice_keeps_what_the_tab_did_not_show()
    {
        var saved = GameFilter.Of(InstalledChoice.Installed, [GameType.Soundtrack, GameType.Tool]);
        var withoutSoundtracks = new FilterOffer(true, [GameType.Game, GameType.Tool]);
        var onlyGames = new FilterOffer(false, [GameType.Game]);

        // The tool unticked, the hidden soundtrack stays; a hidden category stays as saved.
        Assert.Equal(GameFilter.Of(InstalledChoice.All, [GameType.Soundtrack]),
            GameFilter.None.Over(saved, withoutSoundtracks));
        Assert.Equal(saved, GameFilter.None.Over(saved, onlyGames));
        Assert.Equal(saved, saved.Within(withoutSoundtracks).Over(saved, withoutSoundtracks));
    }

    [Fact]
    public void A_saved_filter_comes_back_from_settings_json()
    {
        var filter = GameFilter.Of(InstalledChoice.NotInstalled, [GameType.Tool, GameType.Software]);
        var settings = new AppSettings().WithFilter("epic", filter);

        var json = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Contains("\"NotInstalled\"", json);
        Assert.Equal(filter, loaded.FilterOf("epic"));
        Assert.Equal(GameFilter.None, loaded.FilterOf("steam"));
    }

    [Fact]
    public void No_filter_removes_the_tabs_entry()
    {
        var settings = new AppSettings().WithFilter("epic", GameFilter.None.WithType(GameType.Tool, true)).WithFilter("epic", GameFilter.None);

        Assert.Empty(settings.LibraryFilters);
    }

    [Theory]
    [Trait("TestCase", "T465-11")]
    [InlineData("""{"Installed":"vielleicht","Types":["Tool","Spaceship","7","Game, Tool",null]}""")]
    [InlineData("""{"Installed":"1","Types":[]}""")]
    [InlineData("""{"Installed":null,"Types":null}""")]
    public void Values_this_version_does_not_know_drop_out_silently(string saved)
    {
        var json = """{"LibraryFilters":{"steam":""" + saved + ""","epic":null}}""";

        var settings = JsonSerializer.Deserialize<AppSettings>(json)!;

        var filter = settings.FilterOf("steam");
        Assert.Equal(InstalledChoice.All, filter.Installed);
        Assert.All(filter.Types, type => Assert.Equal(GameType.Tool, type));
        Assert.Equal(GameFilter.None, settings.FilterOf("epic"));
    }

    [Fact]
    public void A_hand_edited_null_holds_no_filters()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"LibraryFilters":null}""")!;

        Assert.Equal(GameFilter.None, settings.FilterOf("steam"));
        Assert.Single(settings.WithFilter("steam", GameFilter.None.WithType(GameType.Tool, true)).LibraryFilters);
    }

    /// <summary>
    /// The modal counts on every press, on the UI thread (#44, budget: focus move under 50 ms). 5,000 entries, the budget's
    /// largest library, stay far below; the bound is loose so a slow CI runner doesn't fail it.
    /// </summary>
    [Fact]
    public void Counting_five_thousand_entries_takes_well_under_a_frame()
    {
        var games = Enumerable.Range(0, 5000)
            .Select(i => Entry($"e{i}", LibraryTypes.Order[i % 4], installed: i % 3 == 0))
            .ToList();
        var filter = GameFilter.Of(InstalledChoice.Installed, [GameType.Tool, GameType.Soundtrack]);
        filter.Count(games);

        // The fastest of five tries: slower code slows every try, a runner busy with other tests only some (#589).
        var fastest = TimeSpan.MaxValue;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 10; i++)
            {
                filter.Count(games);
                filter.Apply(games);
            }

            fastest = watch.Elapsed < fastest ? watch.Elapsed : fastest;
        }

        Assert.True(fastest < TimeSpan.FromMilliseconds(160), $"10 × count and apply took {fastest.TotalMilliseconds:0.0} ms at best");
    }
}
