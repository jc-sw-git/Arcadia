using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Only hits with an image wait (#506, scenario 17): a game left with one hit after the check takes it without a
/// question, one left with none doesn't wait and isn't skipped.
/// </summary>
public sealed class WaitingListImageCheckTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T506-93")]
    public async Task One_hit_with_an_image_is_taken_without_a_question_and_none_with_an_image_never_waits()
    {
        var (solo, bare) = (WaitingListTests.GameOf("solo", "Solo Pine"), WaitingListTests.GameOf("bare", "Bare Moss"));
        var images = new FakeImageServer();
        images.Serve("https://cdn.example/grid/801.png", TestImages.Png(600, 900));
        images.Serve("https://cdn.example/hero/801.png", TestImages.Png(1920, 620));
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(WaitingListTests.Other, solo, bare)], WaitingListTests.Settings, imageServer: images);

        // Two namesakes each; of Solo Pine's only 801 has images at SteamGridDB, of Bare Moss's none.
        arcadia.SteamGridDb
            .Finds("Solo Pine", (801, "Solo Pine", 2019), (802, "Solo Pine", 2021))
            .Finds("Bare Moss", (901, "Bare Moss", 2018), (902, "Bare Moss", 2020))
            .Cover("game", "801", "https://cdn.example/grid/801.png", 600, 900)
            .Hero("game", "801", "https://cdn.example/hero/801.png", 1920, 620);
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        arcadia.Artwork.SearchAgain(byPlayer: false);
        await WaitingListTests.SettledAsync(arcadia);

        Assert.Empty(arcadia.Artwork.Waiting);
        var cache = new ArtworkCache(Path.Combine(arcadia.Folder, "artwork"));
        Assert.NotNull(cache.Read(solo.Key)?.Cover);
        Assert.Null(cache.Read(bare.Key)?.Cover);

        var matches = new NameMatchStore(Path.Combine(arcadia.Folder, "artwork", NameMatchStore.FileName));
        Assert.Equal(801, matches.Of(solo.Key)?.GameId);
        Assert.NotEqual(NameMatchState.Skipped, matches.Of(bare.Key)?.State);
        Assert.NotEqual(NameMatchState.Waiting, matches.Of(bare.Key)?.State);
    }
}
