using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class SearchQueryTests
{
    [Fact]
    public void Plain_input_searches_all_launchers()
    {
        var query = SearchQuery.Parse("  Hollow Knight ");

        Assert.False(query.HasLauncherFilter);
        Assert.Empty(query.LauncherKeywords);
        Assert.Equal("Hollow Knight", query.Term);
    }

    [Fact]
    public void Prefix_with_one_launcher()
    {
        var query = SearchQuery.Parse("/steam Hollow Knight");

        Assert.True(query.HasLauncherFilter);
        Assert.Equal(["steam"], query.LauncherKeywords);
        Assert.Equal("Hollow Knight", query.Term);
    }

    [Fact]
    public void Prefix_with_several_launchers_is_lowercased_and_deduplicated()
    {
        var query = SearchQuery.Parse("/Steam-EPIC-ea-steam hollow");

        Assert.Equal(["steam", "epic", "ea"], query.LauncherKeywords);
        Assert.Equal("hollow", query.Term);
    }

    [Fact]
    public void Prefix_without_term()
    {
        var query = SearchQuery.Parse("/gog");

        Assert.Equal(["gog"], query.LauncherKeywords);
        Assert.Equal(string.Empty, query.Term);
    }

    [Fact]
    public void Slash_later_in_the_text_is_part_of_the_term()
    {
        var query = SearchQuery.Parse("AC/DC Live");

        Assert.False(query.HasLauncherFilter);
        Assert.Equal("AC/DC Live", query.Term);
    }
}
