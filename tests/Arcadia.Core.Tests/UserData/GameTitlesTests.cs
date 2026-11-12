using System.Diagnostics;
using System.Globalization;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.UserData;

/// <summary>
/// #496: the shown title (own above automatic above the launcher's), what a typed title keeps, and how the shown title
/// sorts and searches (T496-4, T496-6, T496-9, T496-10).
/// </summary>
public sealed class GameTitlesTests : IDisposable
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-titles-").FullName;
    private readonly UserDataStore _store;

    public GameTitlesTests()
    {
        _store = new UserDataStore(_folder);
        _store.Open();
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData("  Aal  ", "Zebra", "Aal")]
    [InlineData("", "Zebra", null)]
    [InlineData("   ", "Zebra", null)]
    [InlineData(" Zebra ", "Zebra", null)]
    [InlineData("zebra", "Zebra", "zebra")]
    public void A_typed_title_without_spaces_and_not_the_automatic_one_is_kept(string typed, string automatic, string? kept) =>
        Assert.Equal(kept, GameTitles.OwnTitleFrom(typed, automatic));

    [Fact]
    public void The_own_title_shows_above_the_automatic_one_and_a_reset_brings_it_back()
    {
        var titles = new GameTitles(_store);
        var game = Game("Zebra");

        Assert.True(titles.Change(game, "Aal"));
        Assert.Equal("Aal", titles.Shown(game).Title);
        Assert.Equal("Zebra", titles.AutomaticTitleOf(game));

        Assert.True(titles.Reset(game.Key));
        Assert.Same(game, titles.Shown(game));
        Assert.Null(titles.OwnTitleOf(game.Key));
    }

    [Fact]
    public void Typing_the_automatic_title_or_nothing_clears_the_own_one()
    {
        var titles = new GameTitles(_store);
        var game = Game("Zebra");
        titles.Change(game, "Aal");

        titles.Change(game, " Zebra ");
        Assert.Null(titles.OwnTitleOf(game.Key));

        titles.Change(game, "Aal");
        titles.Change(game, "   ");
        Assert.Null(titles.OwnTitleOf(game.Key));
    }

    [Fact]
    public void An_own_title_stays_when_the_automatic_one_changes_later()
    {
        string? corrected = null;
        var titles = new GameTitles(_store, _ => corrected);
        var game = Game("K?ln");
        titles.Change(game, "Mein Köln");
        var changed = new List<string>();
        titles.Changed += (_, keys) => changed.AddRange(keys);

        corrected = "Köln";
        titles.AutomaticTitlesChanged([game.Key]);

        Assert.Equal("Mein Köln", titles.Shown(game).Title);
        Assert.Equal("Köln", titles.AutomaticTitleOf(game));
        Assert.Equal([game.Key], changed);
    }

    [Fact]
    public void A_corrected_title_shows_without_an_own_one()
    {
        var titles = new GameTitles(_store, key => key == "epic:K?ln" ? "Köln" : null);

        Assert.Equal("Köln", titles.Shown(Game("K?ln", "epic")).Title);
    }

    [Fact]
    public async Task Without_a_readable_file_every_game_shows_its_automatic_title_and_nothing_changes()
    {
        var game = Game("Zebra");
        new GameTitles(_store).Change(game, "Aal");
        await _store.WhenWrittenAsync();
        File.WriteAllText(Path.Combine(_folder, UserDataStore.FileName), "{ broken");
        var unreadable = new UserDataStore(_folder);
        unreadable.Open();
        var titles = new GameTitles(unreadable);

        Assert.False(titles.CanChange);
        Assert.Equal("Zebra", titles.Shown(game).Title);
        Assert.False(titles.Change(game, "Hecht"));
        Assert.False(titles.Reset(game.Key));
        Assert.False(new GameTitles().CanChange);
    }

    [Fact]
    public void Sorting_by_name_uses_the_shown_title_also_with_case_and_umlauts()
    {
        var titles = new GameTitles(_store);
        var zebra = Game("Zebra");
        titles.Change(zebra, "aal");
        var shown = new[] { zebra, Game("Äpfel"), Game("Birne"), Game("Apfel") }.Select(titles.Shown);

        var plain = new[] { Game("aal"), Game("Äpfel"), Game("Birne"), Game("Apfel") };

        // As the name sort sorts games that never had an own title, both ways.
        foreach (var order in new[] { SortOrder.Default, SortOrder.Default with { Direction = SortDirection.Descending } })
        {
            Assert.Equal(GameSorter.Sort(plain, order, German).Select(g => g.Title), GameSorter.Sort(shown, order, German).Select(g => g.Title));
        }

        Assert.Equal("aal", GameSorter.Sort(shown, SortOrder.Default, German)[0].Title);
    }

    [Fact]
    public void Sorting_by_date_is_not_touched_by_own_titles()
    {
        var titles = new GameTitles(_store);
        var older = Game("Aal") with { InstalledAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var newer = Game("Zebra") with { InstalledAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        titles.Change(newer, "Aaa");

        var sorted = GameSorter.Sort([titles.Shown(older), titles.Shown(newer)], new SortOrder(SortField.InstalledAt, SortDirection.Ascending), German);

        Assert.Equal(["Aal", "Aaa"], sorted.Select(g => g.Title));
    }

    [Fact]
    public void The_search_finds_a_renamed_game_by_both_titles_and_shows_the_own_one()
    {
        var titles = new GameTitles(_store);
        var renamed = Game("Zebra Racing");
        titles.Change(renamed, "Aal Abenteuer");
        var games = new[] { titles.Shown(renamed), Game("Zander") };
        string? Other(Game game) => titles.OwnTitleOf(game.Key) is null ? null : titles.AutomaticTitleOf(renamed);

        var search = new GameSearch(games, German, Other);

        Assert.Equal(["Aal Abenteuer"], search.Find("aal").Select(g => g.Title));
        Assert.Equal(["Aal Abenteuer"], search.Find("racing").Select(g => g.Title));
        Assert.Equal(["Aal Abenteuer", "Zander"], search.Find("z", order: SortOrder.Default).Select(g => g.Title));
    }

    [Fact]
    public void Five_thousand_games_with_a_hundred_own_titles_sort_within_the_budget()
    {
        var titles = new GameTitles(_store);
        var games = Enumerable.Range(0, 5000).Select(i => Game($"Spiel {i:D4}")).ToList();
        foreach (var game in games.Take(100))
        {
            titles.Change(game, $"Eigen {game.Id}");
        }

        var watch = Stopwatch.StartNew();
        var sorted = GameSorter.Sort(games.Select(titles.Shown), SortOrder.Default, German);
        watch.Stop();

        Assert.StartsWith("Eigen", sorted[0].Title);
        // The start budget is 2 s for the whole library; laying titles over it and sorting takes a small part of it.
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(500), $"{watch.ElapsedMilliseconds} ms");
    }

    private static Game Game(string title, string launcherId = "steam") =>
        new() { Id = title, LauncherId = launcherId, Title = title, Launch = new LaunchTarget.None() };
}
