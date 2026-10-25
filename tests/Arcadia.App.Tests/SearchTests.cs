using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>The search over all launchers (#12).</summary>
public sealed class SearchTests
{
    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    /// <summary>"hollow" at a title's start, at a word's start and inside a word, with an accent and a trademark sign.</summary>
    private static readonly IReadOnlyList<LauncherSpec> HollowLibrary =
    [
        LauncherSpec.With(
            KnownLaunchers.Gog,
            Installed(KnownLaunchers.Gog, "Unhollowed Lands", Installed),
            Installed(KnownLaunchers.Gog, "The Hollow Crown", Installed),
            Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
            Installed(KnownLaunchers.Gog, "Iron Harbor", Installed)),
        LauncherSpec.With(
            KnownLaunchers.Xbox,
            Installed(KnownLaunchers.Xbox, "Ember™ Hollow", Installed),
            Installed(KnownLaunchers.Xbox, "Höllow Drift", Installed),
            Installed(KnownLaunchers.Xbox, "Silent Tides", Installed)),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T12-3")]
    public async Task Hits_come_from_all_launchers_by_relevance_whatever_the_case_accents_and_trademark_signs()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(HollowLibrary);
        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;

        arcadia.Main.TypeInSearch("hOLLow");

        // Title start, then word start, then inside a word; each group by name.
        Assert.Equal(["Höllow Drift", "Hollow Knight", "Ember™ Hollow", "The Hollow Crown", "Unhollowed Lands"], search.Hits.Select(game => game.Title));
        Assert.Equal("5 Treffer in 2 Launchern", search.ResultsTitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-3")]
    public async Task An_accent_or_trademark_sign_in_the_title_or_the_term_does_not_matter_and_no_hit_shows_the_empty_state()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(HollowLibrary);
        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;

        arcadia.Main.TypeInSearch("émber hollow");
        Assert.Equal(["Ember™ Hollow"], search.Hits.Select(game => game.Title));

        Erase(arcadia);
        arcadia.Main.TypeInSearch("hollow drift");
        Assert.Equal(["Höllow Drift"], search.Hits.Select(game => game.Title));

        Erase(arcadia);
        arcadia.Main.TypeInSearch("xyz");
        Assert.Empty(search.Hits);
        Assert.False(search.HasHits);
        Assert.True(search.Empty?.IsNoHits);
        Assert.Equal("Keine Treffer", search.Empty?.Title);
        Assert.Equal("Kein Spiel passt zu „xyz“. Prüfe die Schreibweise oder nimm weitere Launcher dazu.", search.Empty?.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-3")]
    public async Task Trademark_registered_and_copyright_signs_count_for_nothing_and_each_hit_names_its_launcher()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(
        [
            LauncherSpec.With(
                KnownLaunchers.Gog,
                Installed(KnownLaunchers.Gog, "F1® 24", Installed),
                Installed(KnownLaunchers.Gog, "Batman™ Arkham Knight", Installed)),
            LauncherSpec.With(
                KnownLaunchers.Xbox,
                Installed(KnownLaunchers.Xbox, "Hitman© World of Assassination", Installed),
                Installed(KnownLaunchers.Xbox, "Silent Tides", Installed)),
        ]);
        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;

        arcadia.Main.TypeInSearch("f1 24");
        Assert.Equal(["F1® 24"], search.Hits.Select(game => game.Title));
        Assert.Equal("GOG Galaxy", search.FocusedResult?.LauncherName);

        foreach (var sign in new[] { "™", "®", "©" })
        {
            Erase(arcadia);
            arcadia.Main.TypeInSearch(sign);
            Assert.Empty(search.Hits);
        }

        Erase(arcadia);
        arcadia.Main.TypeInSearch("hitman world");
        Assert.Equal(["Hitman© World of Assassination"], search.Hits.Select(game => game.Title));
        Assert.Equal("Xbox", search.FocusedResult?.LauncherName);
        Assert.Equal("1 Treffer in 1 Launcher", search.ResultsTitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-9")]
    public async Task B_first_puts_the_keyboard_away_then_goes_back_to_the_library_as_it_was_without_erasing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 3);
        var focused = arcadia.FocusedGame!.Game.Key;

        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.True(search.IsKeyboardOut);
        arcadia.Main.TypeInSearch("iron");

        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.False(search.IsKeyboardOut);
        Assert.Equal("iron", search.Text);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSearchShown);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal("2", arcadia.CurrentPage);
        Assert.Equal(focused, arcadia.FocusedGame?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-9")]
    public async Task B_twice_in_a_row_goes_straight_back_to_the_library_as_it_was()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 3);
        var focused = arcadia.FocusedGame!.Game.Key;

        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("iron");
        var search = arcadia.Search!;

        // As fast as the controller reports them: no time passes between the two presses.
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        Assert.Equal("iron", search.Text);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal("2", arcadia.CurrentPage);
        Assert.Equal(focused, arcadia.FocusedGame?.Game.Key);
    }

    /// <summary>Empties the search field, as the user would with the backspace key.</summary>
    private static void Erase(ArcadiaDriver arcadia)
    {
        var search = arcadia.Search!;
        while (search.Text.Length > 0)
        {
            search.Erase();
        }
    }
}
