using Arcadia.App.Input;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// #532: a cover that comes to the front of the detail carousel from a stack is drawn from an image decoded for its front
/// size, not for the smaller stack it came from (the owner saw it blurry after LT/RT, sharp when opened directly).
/// </summary>
public sealed class SharpCoverTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T532-2")]
    public async Task A_cover_turned_to_the_front_is_decoded_at_its_front_size()
    {
        var games = Enumerable.Range(0, 4)
            .Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i}", DateTimeOffset.UnixEpoch.AddDays(i)))
            .ToList();
        using var images = new LauncherImages();
        foreach (var game in games)
        {
            images.Set(game, ArtworkKind.Cover, TestImages.Png(600, 900));
        }

        using var arcadia = await ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games, Artwork: images)]);
        var driver = arcadia.Arcadia;
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await driver.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }

        var widest = new Dictionary<string, int>();
        driver.Main.ArtworkLoader.Loaded += (file, size) =>
        {
            if (file.Kind == ArtworkKind.Cover)
            {
                lock (widest)
                {
                    widest[file.GameKey] = Math.Max(widest.GetValueOrDefault(file.GameKey), size.Width);
                }
            }
        };

        driver.Hold(UiAction.Accept, PastHoldTime);
        await DecodedAsync(arcadia);
        // Two turns: the cover comes from the outer stack, where it was decoded smallest.
        var next = driver.Detail!.Covers.Single(cover => cover.Offset == 2).Tile.Game.Key;
        driver.Press(UiAction.NextPage);
        await DecodedAsync(arcadia);
        driver.Press(UiAction.NextPage);
        await DecodedAsync(arcadia);

        Assert.Equal(next, driver.Detail.Current.Tile.Game.Key);
        lock (widest)
        {
            // The front cover is 180 layout pixels wide in a 1920 window.
            Assert.True(widest.GetValueOrDefault(next) >= 180, $"Cover vorn mit {widest.GetValueOrDefault(next)} px dekodiert, gezeigt mit 180 px");
        }
    }

    private static async Task DecodedAsync(ArcadiaWindow arcadia)
    {
        for (var round = 0; round < 4; round++)
        {
            arcadia.WaitForTransitions();
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            arcadia.Settle();
        }
    }
}
