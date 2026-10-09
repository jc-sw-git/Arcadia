using System.Globalization;
using Arcadia.Core.Library;
using Arcadia.Core.Models;

namespace Arcadia.Core.Tests;

public class SortingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 20, 0, 0, TimeSpan.Zero);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static Game G(string title, int? installedDaysAgo = null, int? playedDaysAgo = null) => new()
    {
        Id = title,
        LauncherId = "test",
        Title = title,
        Launch = new LaunchTarget.None(),
        InstalledAt = installedDaysAgo is { } i ? Now.AddDays(-i) : null,
        LastPlayedAt = playedDaysAgo is { } p ? Now.AddDays(-p) : null,
    };

    private static string[] Titles(IEnumerable<Game> games) => games.Select(g => g.Title).ToArray();

    [Fact]
    public void Name_sorting_ignores_case_and_accents()
    {
        var games = new[] { G("zeta"), G("Échos"), G("alpha"), G("Beta") };

        Assert.Equal(["alpha", "Beta", "Échos", "zeta"], Titles(GameSorter.Sort(games, new(SortField.Name, SortDirection.Ascending), German)));
        Assert.Equal(["zeta", "Échos", "Beta", "alpha"], Titles(GameSorter.Sort(games, new(SortField.Name, SortDirection.Descending), German)));
    }

    [Fact]
    public void Install_date_descending_shows_newest_first()
    {
        var games = new[] { G("old", installedDaysAgo: 300), G("new", installedDaysAgo: 2), G("mid", installedDaysAgo: 40) };

        Assert.Equal(["new", "mid", "old"], Titles(GameSorter.Sort(games, new(SortField.InstalledAt, SortDirection.Descending), German)));
        Assert.Equal(["old", "mid", "new"], Titles(GameSorter.Sort(games, new(SortField.InstalledAt, SortDirection.Ascending), German)));
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public void Never_played_games_always_go_last(SortDirection direction)
    {
        var games = new[] { G("never"), G("recent", playedDaysAgo: 1), G("long ago", playedDaysAgo: 200) };

        var sorted = Titles(GameSorter.Sort(games, new(SortField.LastPlayed, direction), German));

        Assert.Equal("never", sorted[^1]);
    }

    [Fact]
    public void Equal_dates_fall_back_to_title_order()
    {
        var games = new[] { G("b", installedDaysAgo: 5), G("a", installedDaysAgo: 5) };

        Assert.Equal(["a", "b"], Titles(GameSorter.Sort(games, new(SortField.InstalledAt, SortDirection.Descending), German)));
    }
}
