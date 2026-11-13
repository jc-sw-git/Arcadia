using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>A game seeking cover and background takes each from the SteamGridDB game the player picked it from (#539).</summary>
public sealed partial class ArtworkServiceByNameTests
{
    [Fact]
    [Trait("TestCase", "T539-5")]
    public async Task Cover_and_background_chosen_from_two_games_each_come_from_their_own()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();

        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[0], heroHit: _service.Waiting[0].Hits[1]);

        Assert.Empty(_service.Waiting);
        Assert.Equal((Cover501, Hero502), (Cached()!.Cover!.Stamp, Cached()!.Hero!.Stamp));
        Assert.Contains(_log, line => line.Contains("Hintergrund von 502", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T539-6")]
    public async Task Two_chosen_games_last_across_a_restart_and_a_reload()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();
        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[1], heroHit: _service.Waiting[0].Hits[0]);

        await RestartAsync();
        _service.Update([new LibraryArtwork(new LauncherLibrary(Other, [Echo]), _source)]);
        await _service.WhenIdleAsync();
        _service.ReloadAll(overwriteSetByHand: false);
        await _service.WhenIdleAsync();

        Assert.Empty(Searches());
        Assert.Contains("grids/game/502", _sgdb.Asked());
        Assert.Contains("heroes/game/501", _sgdb.Asked());
        Assert.Equal((Cover502, Hero501), (Cached()!.Cover!.Stamp, Cached()!.Hero!.Stamp));
    }

    [Fact]
    [Trait("TestCase", "T539-6")]
    public async Task Artwork_aendern_offers_each_kind_from_its_chosen_game()
    {
        await WithKeyAsync();
        ThreeEchoes();
        await ScanAsync();

        await _service.ChooseAsync(Echo, _service.Waiting[0].Hits[0], heroHit: _service.Waiting[0].Hits[1]);

        Assert.Equal([Cover501], (await _service.Suggestions!.FindAsync(Echo, ArtworkKind.Cover, default)).Select(s => s.Download.AbsoluteUri));
        Assert.Equal([Hero502], (await _service.Suggestions!.FindAsync(Echo, ArtworkKind.Hero, default)).Select(s => s.Download.AbsoluteUri));
    }

    [Fact]
    [Trait("TestCase", "T539-6")]
    public void A_stored_background_game_that_cant_be_read_falls_back_to_the_cover_game()
    {
        var path = Path.Combine(_root, "matches.json");
        File.WriteAllText(path, """
            {
              "a": { "state": "Chosen", "gameId": 501, "heroGameId": 502 },
              "b": { "state": "Chosen", "gameId": 501, "heroGameId": -4 },
              "c": { "state": "Chosen", "gameId": 501, "heroGameId": "x" }
            }
            """);

        var store = new Core.Artwork.SteamGridDb.NameMatchStore(path);

        Assert.Equal((501, 502), (store.Of("a")!.GameIdFor(ArtworkKind.Cover), store.Of("a")!.GameIdFor(ArtworkKind.Hero)));
        Assert.Equal((501, 501), (store.Of("b")!.GameIdFor(ArtworkKind.Cover), store.Of("b")!.GameIdFor(ArtworkKind.Hero)));
        Assert.Equal((501, 501), (store.Of("c")!.GameIdFor(ArtworkKind.Cover), store.Of("c")!.GameIdFor(ArtworkKind.Hero)));
    }
}
