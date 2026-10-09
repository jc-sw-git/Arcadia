using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(0, new[] { 10 })]
    [InlineData(9, new[] { 10 })]
    [InlineData(10, new[] { 10 })]
    [InlineData(11, new[] { 10, 25 })]
    [InlineData(25, new[] { 10, 25 })]
    [InlineData(26, new[] { 10, 25, 50 })]
    [InlineData(47, new[] { 10, 25, 50 })]
    [InlineData(51, new[] { 10, 25, 50, 100 })]
    [InlineData(128, new[] { 10, 25, 50, 100 })]
    public void A_size_is_allowed_once_the_next_smaller_size_no_longer_shows_everything(int gameCount, int[] expected)
    {
        Assert.Equal(expected, PageSizes.Allowed(gameCount));
    }

    [Fact]
    public void Unknown_sizes_are_never_allowed()
    {
        Assert.False(PageSizes.IsAllowed(30, 500));
    }

    [Theory]
    [InlineData(25, 128, 25)]
    [InlineData(100, 47, 50)]
    [InlineData(25, 9, 10)]
    [InlineData(100, 62, 100)]
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
