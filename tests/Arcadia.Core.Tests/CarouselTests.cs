using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public class CarouselTests
{
    [Theory]
    [InlineData(0, -1, 5, 4)]
    [InlineData(4, 1, 5, 0)]
    [InlineData(2, 1, 5, 3)]
    [InlineData(1, -2, 5, 4)]
    [InlineData(3, 2, 5, 0)]
    public void Neighbours_wrap_around_both_ends(int index, int offset, int count, int expected)
    {
        Assert.Equal(expected, Carousel.At(index, offset, count));
    }

    [Fact]
    public void With_two_games_the_other_one_stands_on_both_sides()
    {
        Assert.Equal(1, Carousel.At(0, -1, 2));
        Assert.Equal(1, Carousel.At(0, 1, 2));
        Assert.Equal(0, Carousel.At(1, 1, 2));
    }

    [Fact]
    public void A_single_game_has_no_neighbours()
    {
        Assert.Equal(0, Carousel.At(0, 0, 1));
        Assert.Null(Carousel.At(0, -1, 1));
        Assert.Null(Carousel.At(0, 1, 1));
    }

    [Fact]
    public void An_empty_list_shows_nothing()
    {
        Assert.Null(Carousel.At(0, 0, 0));
    }

    [Fact]
    public void Find_follows_the_game_to_its_new_place()
    {
        var games = new[] { Game("c"), Game("a"), Game("b") };

        Assert.Equal(1, Carousel.Find(games, Game("a").Key, previousIndex: 0));
    }

    [Fact]
    public void Find_keeps_the_place_of_a_game_that_is_gone()
    {
        var games = new[] { Game("a"), Game("b") };

        Assert.Equal(1, Carousel.Find(games, Game("gone").Key, previousIndex: 1));
        Assert.Equal(1, Carousel.Find(games, Game("gone").Key, previousIndex: 7));
        Assert.Equal(-1, Carousel.Find([], Game("gone").Key, previousIndex: 0));
    }

    [Fact]
    public void FindOrNext_follows_the_shown_game_or_goes_to_the_next_one_still_there()
    {
        var before = new[] { Game("a"), Game("b"), Game("c"), Game("d") };

        Assert.Equal(0, Carousel.FindOrNext([Game("c"), Game("b")], before, shown: 1));
        Assert.Equal(1, Carousel.FindOrNext([Game("a"), Game("d")], before, shown: 1));
        // Past the end it goes round to the start.
        Assert.Equal(0, Carousel.FindOrNext([Game("b")], before, shown: 3));
        Assert.Equal(-1, Carousel.FindOrNext([], before, shown: 1));
        // None of the old games: the place, clamped.
        Assert.Equal(0, Carousel.FindOrNext([Game("x")], before, shown: 2));
    }

    private static Game Game(string id) => new() { Id = id, LauncherId = "steam", Title = id, Launch = new LaunchTarget.None() };
}
