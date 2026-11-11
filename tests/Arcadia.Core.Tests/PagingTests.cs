using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public class PagingTests
{
    /// <summary>T24-1: 24 is always there, 64 from 25 games on, 128 from 65 on.</summary>
    [Theory]
    [InlineData(0, new[] { 24 })]
    [InlineData(24, new[] { 24 })]
    [InlineData(25, new[] { 24, 64 })]
    [InlineData(47, new[] { 24, 64 })]
    [InlineData(64, new[] { 24, 64 })]
    [InlineData(65, new[] { 24, 64, 128 })]
    [InlineData(1000, new[] { 24, 64, 128 })]
    [Trait("TestCase", "T24-1")]
    public void A_size_is_allowed_once_the_next_smaller_size_no_longer_shows_everything(int gameCount, int[] expected)
    {
        Assert.Equal(expected, PageSizes.Allowed(gameCount));
    }

    /// <summary>The sizes are whole rows of the library's 8 columns, so a full page never ends in a half row (#24).</summary>
    [Fact]
    public void Every_size_fills_whole_rows_of_8_and_24_is_the_default()
    {
        Assert.Equal([24, 64, 128], PageSizes.Options);
        Assert.All(PageSizes.Options, size => Assert.Equal(0, size % 8));
        Assert.Equal(24, PageSizes.Default);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void Unknown_sizes_are_never_allowed(int size)
    {
        Assert.False(PageSizes.IsAllowed(size, 500));
    }

    /// <summary>T24-4: with 128 preferred, 30 games show on one page of 64, 200 games by 128.</summary>
    [Theory]
    [InlineData(64, 200, 64)]
    [InlineData(128, 30, 64)]
    [InlineData(128, 200, 128)]
    [InlineData(64, 9, 24)]
    [InlineData(24, 1000, 24)]
    [Trait("TestCase", "T24-4")]
    public void Effective_size_falls_back_to_the_largest_allowed_one(int preferred, int gameCount, int expected)
    {
        Assert.Equal(expected, PageSizes.Effective(preferred, gameCount));
    }

    [Fact]
    public void Last_page_holds_the_remainder()
    {
        var window = PageWindow.For(itemCount: 47, pageSize: 25, requestedPage: 1);

        Assert.Equal(new PageWindow(PageIndex: 1, PageCount: 2, Start: 25, Length: 22), window);
    }

    [Fact]
    public void Requested_page_is_clamped()
    {
        Assert.Equal(1, PageWindow.For(47, 25, 99).PageIndex);
        Assert.Equal(0, PageWindow.For(47, 25, -3).PageIndex);
    }

    [Fact]
    public void Empty_list_has_one_empty_page()
    {
        Assert.Equal(new PageWindow(0, 1, 0, 0), PageWindow.For(0, 25, 0));
    }
}
