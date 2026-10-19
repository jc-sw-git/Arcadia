using System.Globalization;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public class GameSearchTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Titles_that_start_with_the_term_come_first_then_word_starts_then_the_rest()
    {
        var games = new[] { Game("The Hollow Tides"), Game("Unhollowed Waters"), Game("Hollow Knight"), Game("Silent Hill") };

        var hits = GameSearch.Find(games, "hollow", German);

        Assert.Equal(["Hollow Knight", "The Hollow Tides"], hits.Select(g => g.Title).Take(2));
        Assert.Equal("Unhollowed Waters", hits[^1].Title);
        Assert.Equal(3, hits.Count);
    }

    [Fact]
    public void A_later_word_start_counts_even_if_the_term_also_appears_inside_a_word_before()
    {
        var hits = GameSearch.Find([Game("Unhollowed Hollow"), Game("Unhollowness")], "hollow", German);

        Assert.Equal(["Unhollowed Hollow", "Unhollowness"], hits.Select(g => g.Title));
    }

    [Fact]
    public void Ties_sort_by_title_and_keep_the_launcher_order()
    {
        var games = new[] { Game("Hollow Wilds", "epic"), Game("Hollow Drift", "steam"), Game("Hollow Drift", "epic") };

        var hits = GameSearch.Find(games, "hollow", German);

        Assert.Equal(["Hollow Drift/steam", "Hollow Drift/epic", "Hollow Wilds/epic"], hits.Select(g => $"{g.Title}/{g.LauncherId}"));
    }

    [Fact]
    public void Case_and_accents_are_ignored()
    {
        var hits = GameSearch.Find([Game("Pokémon Legends"), Game("HOLLOW")], "POKEMON", German);

        Assert.Equal(["Pokémon Legends"], hits.Select(g => g.Title));
        Assert.Single(GameSearch.Find([Game("HOLLOW")], "hollow", German));
    }

    [Theory]
    [InlineData("Straße der Helden", "strasse")]
    [InlineData("Strasse", "straße")]
    [InlineData("Søren's Quest", "soren")]
    [InlineData("Łódź Racer", "lodz")]
    [InlineData("Æon Flux", "aeon")]
    [InlineData("Œuvre", "oeuvre")]
    [InlineData("ﬁnal", "final")]
    [InlineData("x²", "x2")]
    [InlineData("Dragon’s Dogma", "dragon's")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("Ñandú", "nandu")]
    public void Letter_variants_match_their_plain_letters(string title, string term)
    {
        Assert.Single(GameSearch.Find([Game(title)], term, German));
    }

    [Fact]
    public void A_term_after_a_colon_or_dash_is_a_word_start()
    {
        var hits = GameSearch.Find([Game("Xhollow"), Game("Hades: Hollow-Edition")], "edition", German);

        Assert.Equal(["Hades: Hollow-Edition"], hits.Select(g => g.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_term_finds_nothing(string term)
    {
        Assert.Empty(GameSearch.Find([Game("Hollow Knight")], term, German));
    }

    [Fact]
    public void Trademark_signs_count_for_nothing_in_the_term_or_the_title()
    {
        var games = new[] { Game("Batman™: Arkham Knight"), Game("HITMAN 3"), Game("F1® 24") };

        Assert.Empty(GameSearch.Find(games, "™", German));
        Assert.Equal(["Batman™: Arkham Knight"], GameSearch.Find(games, "batman: arkham", German).Select(g => g.Title));
        Assert.Equal(["F1® 24"], GameSearch.Find(games, "F1 24", German).Select(g => g.Title));
        Assert.Equal(["F1® 24"], GameSearch.Find(games, "f1®", German).Select(g => g.Title));
    }

    [Fact]
    public void The_term_is_trimmed()
    {
        Assert.Single(GameSearch.Find([Game("Hollow Knight")], " knight ", German));
    }

    [Fact]
    public void Without_a_sort_order_the_hits_stay_by_relevance()
    {
        var games = new[] { Game("The Hollow Tides"), Game("Hollow Knight") };

        Assert.Equal(["Hollow Knight", "The Hollow Tides"], GameSearch.Find(games, "hollow", German, null).Select(g => g.Title));
    }

    [Fact]
    public void A_sort_order_sorts_the_hits_like_the_library()
    {
        var games = new[] { Game("Hollow Knight"), Game("The Hollow Tides"), Game("Silent Hill"), Game("Abyss Hollow") };

        var hits = GameSearch.Find(games, "hollow", German, new SortOrder(SortField.Name, SortDirection.Descending));

        Assert.Equal(["The Hollow Tides", "Hollow Knight", "Abyss Hollow"], hits.Select(g => g.Title));
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public void Sorting_by_name_gives_the_library_order(SortDirection direction)
    {
        var games = new[] { Game("Hollow Knight"), Game("The Hollow Tides"), Game("Ábyss Hollow"), Game("abyss hollow II"), Game("Unhollowed") };
        var order = new SortOrder(SortField.Name, direction);

        var hits = GameSearch.Find(games, "hollow", German, order);

        Assert.Equal(GameSorter.Sort(games, order, German).Select(g => g.Title), hits.Select(g => g.Title));
    }

    [Fact]
    public void A_prepared_search_finds_only_in_the_chosen_launchers()
    {
        var search = new GameSearch([Game("Hollow Knight", "steam"), Game("Hollow Drift", "epic"), Game("Hollow Wilds", "gog")], German);

        var hits = search.Find("hollow", new HashSet<string> { "epic", "gog" });

        Assert.Equal(["Hollow Drift/epic", "Hollow Wilds/gog"], hits.Select(g => $"{g.Title}/{g.LauncherId}"));
        Assert.Equal(3, search.Find("hollow").Count);
    }

    [Fact]
    public void Games_without_a_date_go_last_in_the_hits_too()
    {
        var played = Game("Hollow Knight") with { LastPlayedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) };
        var games = new[] { Game("Abyss Hollow"), played };

        var hits = GameSearch.Find(games, "hollow", German, new SortOrder(SortField.LastPlayed, SortDirection.Ascending));

        Assert.Equal(["Hollow Knight", "Abyss Hollow"], hits.Select(g => g.Title));
    }

    private static Game Game(string title, string launcherId = "steam") =>
        new() { Id = title, LauncherId = launcherId, Title = title, Launch = new LaunchTarget.None() };
}
