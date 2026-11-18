using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// #685: every element built costs memory whether it is seen or not, and a page of 128 covers was most of Arcadia's
/// memory. A page builds only the covers in and near sight and lets the far ones go when the player is quiet; a cover
/// builds its rare badges only when they show.
/// </summary>
public sealed class CoversInSightTests
{
    // The grid shows a little over three rows of eight, built two rows ahead; the first five rows are always kept.
    private const int MostCoversBuiltAtTheTop = 6 * 8;

    private const int KeptPlaces = 40;

    [AvaloniaFact]
    [Trait("TestCase", "T685-1")]
    public async Task A_page_of_128_builds_only_the_covers_seen_and_lets_the_far_ones_go_when_quiet()
    {
        using var window = await ArcadiaWindow.ShowAsync(Library(300), ArcadiaWindow.WithPageSize(128, Library(300)));
        var arcadia = window.Arcadia;
        Assert.Equal(128, arcadia.PageGames.Count);
        Assert.InRange(Built(window), 8, MostCoversBuiltAtTheTop);
        Assert.True(Places(window)[0].IsBuilt, "Das erste Cover ist nicht gebaut.");
        Assert.False(Places(window)[^1].IsBuilt, "Das letzte Cover ist schon gebaut, obwohl es weit außer Sicht ist.");

        // Down to the last cover, as a controller goes: the covers there are built, nothing is let go on the way.
        window.Press(UiAction.Down, times: 15).WaitForTransitions();
        Assert.True(Places(window)[^1].IsBuilt, "Das letzte Cover ist nicht gebaut.");
        Assert.True(Places(window)[KeptPlaces].IsBuilt, "Ein Cover auf dem Weg nach unten wurde schon beim Scrollen freigegeben.");
        Assert.Equal(arcadia.PageGames[^8], arcadia.FocusedGame);

        // The player is quiet: what is far from sight goes, but the first rows, which a page turn shows, stay.
        Assert.True(BuiltInSight.LetGoOutOfSight() > 0, "Nichts wurde freigegeben.");
        window.Settle();
        Assert.False(Places(window)[KeptPlaces].IsBuilt, "Ein Cover weit außer Sicht bleibt gebaut.");
        Assert.True(Places(window)[KeptPlaces - 1].IsBuilt, "Ein Cover der ersten Reihen wurde freigegeben.");
        Assert.True(Places(window)[^1].IsBuilt, "Das Cover im Bild wurde freigegeben.");

        // Back up: the top shows its games at once.
        window.Press(UiAction.Up, times: 15).WaitForTransitions();
        Assert.Equal(arcadia.PageGames[0].Title, ToolTip.GetTip(window.Tile(arcadia.PageGames[0])));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T685-2")]
    public async Task A_cover_builds_its_update_badge_only_when_an_update_shows()
    {
        var games = Games(10).Select((game, i) => i == 3 ? game with { UpdateAvailable = true } : game).ToArray();
        using var window = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, games)]);
        var arcadia = window.Arcadia;

        var withUpdate = window.Tile(arcadia.PageGames.Single(tile => tile.UpdateAvailable));
        var without = window.Tile(arcadia.PageGames.First(tile => !tile.UpdateAvailable));

        Assert.Single(Badges(withUpdate), badge => badge.IsBuilt && badge.IsVisible);
        Assert.All(Badges(without), badge => Assert.False(badge.IsBuilt));
    }

    private static int Built(ArcadiaWindow window) => Places(window).Count(place => place.IsBuilt);

    private static List<BuiltInSight> Places(ArcadiaWindow window) =>
        window.Named<ItemsControl>("GameGrid").GetVisualDescendants().OfType<BuiltInSight>().ToList();

    private static List<BuiltWhenShown> Badges(Control cover) => cover.GetVisualDescendants().OfType<BuiltWhenShown>().ToList();

    private static IReadOnlyList<LauncherSpec> Library(int count) => [LauncherSpec.With(KnownLaunchers.Gog, Games(count))];

    private static Launchers.Abstractions.Game[] Games(int count) =>
        Enumerable.Range(1, count).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D3}", DateTimeOffset.UnixEpoch)).ToArray();
}
