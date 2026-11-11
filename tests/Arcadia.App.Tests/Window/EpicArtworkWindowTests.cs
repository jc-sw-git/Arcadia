using Arcadia.App.Tests.Fixtures;
using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #488 in the real window: Epic's catalog images, read from a recreated <c>catcache.bin</c> with made-up ids and addresses
/// and downloaded from the test's image server, show at every place for their own game and never hold up the library.
/// The window checks themselves are #487's (<see cref="ArtworkWindowTests"/>).
/// </summary>
public sealed class EpicArtworkWindowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-epic-artwork-window-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string TallUrl(EpicApp app) => $"https://cdn1.epicgames.com/offer/{app.Namespace}/{app.AppName}-tall.png";

    private static string WideUrl(EpicApp app) => $"https://cdn1.epicgames.com/offer/{app.Namespace}/{app.AppName}-wide.png";

    /// <summary>Epic's catalog with a tall and a wide image per app, and the games as Epic's scanner names them.</summary>
    private (EpicArtwork Source, Game[] Games) Catalog(EpicApp[] apps)
    {
        var epic = new EpicFolder(_root);
        string[] game = ["games", "applications"];
        epic.WriteCatalog([.. apps.Select(app => (app with { KeyImages = [("DieselGameBoxTall", TallUrl(app)), ("DieselGameBox", WideUrl(app))] }, game, (EpicApp?)null))]);
        var games = apps.Select(app => new Game
        {
            Id = app.AppName,
            LauncherId = KnownLaunchers.Epic.Id,
            Title = app.Title,
            // Installed: the "not installed" shade would darken the colour the check reads; uninstalled ones in EpicArtworkTests.
            IsInstalled = true,
            Launch = new LaunchTarget.ProtocolUri($"com.epicgames.launcher://apps/{app.Namespace}%3A{app.CatalogItemId}%3A{app.AppName}?action=launch&silent=true"),
        }).ToArray();
        return (new EpicArtwork(epic.Folders.CatalogPath), games);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T488-15")]
    public async Task With_the_controller_alone_every_place_shows_the_epic_games_own_images()
    {
        var apps = Enumerable.Range(1, 30).Select(i => new EpicApp($"ep{i:D2}", $"Spiel {i:D2}")).ToArray();
        var (source, games) = Catalog(apps);
        var server = new FakeImageServer();
        foreach (var (app, i) in apps.Select((a, i) => (a, i)))
        {
            // Epic's formats, small: 3 : 4 and 16 : 9.
            server.Serve(TallUrl(app), ArtworkWindowTests.Solid(i, 300, 400));
            server.Serve(WideUrl(app), ArtworkWindowTests.Solid(i, 640, 360));
        }

        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Epic, games) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers, imageServer: server);
        window.Arcadia.Artwork.Update([new LibraryArtwork(new LauncherLibrary(KnownLaunchers.Epic, games), source)]);
        await window.Arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await ArtworkWindowTests.WaitShowingAsync(window);
        window.WaitForTransitions();

        Assert.All(games, game => Assert.NotNull(window.Arcadia.Artwork.Of(game.Key)?.Hero));
        ArtworkWindowTests.AssertEveryImageShowsItsGame(window, games, "Bibliothek");
        await ArtworkWindowTests.WalkWithTheControllerAsync(window, games);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T488-14")]
    public async Task While_500_epic_games_wait_two_seconds_each_for_their_images_the_library_and_search_stay_fast()
    {
        var (source, games) = Catalog([.. Enumerable.Range(1, 500).Select(i => new EpicApp($"ep{i:D3}", $"Spiel {i:D3}"))]);
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Epic, games)];
        using var server = new ArtworkWindowTests.SlowImageServer(TimeSpan.FromSeconds(2));
        using var window = await ArcadiaWindow.ShowAsync(launchers, imageServer: server);

        await ArtworkWindowTests.AssertFastWhileDownloadingAsync(window, server, new LibraryArtwork(new LauncherLibrary(KnownLaunchers.Epic, games), source));
    }
}
