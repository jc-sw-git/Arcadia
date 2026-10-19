using Arcadia.Core.Launchers;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public class LauncherScopeTests
{
    private static readonly IReadOnlyList<LauncherInfo> Launchers = [KnownLaunchers.Steam, KnownLaunchers.Epic, KnownLaunchers.Ea];
    private static readonly string[] All = ["steam", "epic", "ea"];

    [Fact]
    public void Without_a_prefix_the_chips_decide()
    {
        var scope = LauncherScope.Of(SearchQuery.Parse("hollow"), Launchers, ["ea", "steam"]);

        Assert.Equal(["steam", "ea"], scope.LauncherIds);
        Assert.False(scope.IsAll);
        Assert.Empty(scope.UnknownKeywords);
    }

    [Fact]
    public void A_prefix_overrides_the_chips_and_reports_unknown_keywords()
    {
        var scope = LauncherScope.Of(SearchQuery.Parse("/EA-xyz-steam hollow"), Launchers, All);

        Assert.Equal(["steam", "ea"], scope.LauncherIds);
        Assert.Equal(["xyz"], scope.UnknownKeywords);
        Assert.False(scope.IsAll);
    }

    [Fact]
    public void A_prefix_naming_every_launcher_covers_all()
    {
        Assert.True(LauncherScope.Of(SearchQuery.Parse("/steam-epic-ea x"), Launchers, ["steam"]).IsAll);
    }

    [Fact]
    public void A_prefix_with_only_unknown_keywords_covers_none()
    {
        var scope = LauncherScope.Of(SearchQuery.Parse("/xyz hollow"), Launchers, All);

        Assert.Empty(scope.LauncherIds);
        Assert.Equal(["xyz"], scope.UnknownKeywords);
    }

    [Fact]
    public void Toggling_a_chip_keeps_the_tab_order_and_never_leaves_none()
    {
        var scope = new LauncherScope(["ea"], [], false);

        Assert.Equal(["steam", "ea"], scope.Toggle("steam", Launchers));
        Assert.Null(scope.Toggle("ea", Launchers));
    }

    [Fact]
    public void Changing_the_chips_rewrites_a_prefix()
    {
        Assert.Equal("/steam-ea hollow", LauncherScope.Rewrite("/epic-xyz hollow", ["ea", "steam"], Launchers));
        Assert.Equal("/epic ", LauncherScope.Rewrite("/steam", ["epic"], Launchers));
    }

    [Fact]
    public void All_chips_drop_the_prefix()
    {
        Assert.Equal("hollow", LauncherScope.Rewrite("/epic hollow", All, Launchers));
    }

    [Fact]
    public void An_input_without_prefix_stays_when_the_chips_change()
    {
        Assert.Equal("hollow ", LauncherScope.Rewrite("hollow ", ["epic"], Launchers));
    }

    [Fact]
    public void An_input_without_prefix_stays_when_all_chips_go_back_on()
    {
        Assert.Equal("hollow ", LauncherScope.Rewrite("hollow ", All, Launchers));
    }
}
