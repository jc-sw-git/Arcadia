using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// #487: covers and backgrounds are decoded only where they are drawn, at the size they are drawn (T487-19). The images are
/// made up (<see cref="DemoArtwork"/>: covers 600 × 900 or 1200 × 1600, backgrounds 1920 × 620 or 2560 × 1440) and reach the cache as a launcher's.
/// </summary>
public sealed class ArtworkViewTests : IDisposable
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private readonly string _images = Directory.CreateTempSubdirectory("arcadia-artwork-view-").FullName;
    private readonly List<(ArtworkFile File, PixelSize Size)> _decoded = [];

    public void Dispose() => Directory.Delete(_images, recursive: true);

    [AvaloniaFact]
    [Trait("TestCase", "T487-19")]
    public async Task Only_what_is_drawn_is_decoded_and_at_the_size_it_is_drawn()
    {
        var games = Enumerable.Range(1, 1000).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D4}", DateTimeOffset.UnixEpoch)).ToArray();
        // Switched on once the test counts the decodes, so the start's own run brings no images before that.
        var source = new SwitchedArtwork(new DemoArtwork(_images));
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers);
        var arcadia = window.Arcadia;
        arcadia.Main.ArtworkLoader.Loaded += (file, size) =>
        {
            lock (_decoded)
            {
                _decoded.Add((file, size));
            }
        };

        // The launcher's own source too: a rescan meanwhile (the window's activation) takes the run's place with it.
        source.IsOn = true;
        arcadia.Artwork.Update([new LibraryArtwork(new LauncherLibrary(KnownLaunchers.Gog, games), source)]);
        await arcadia.Artwork.WhenIdleAsync();
        var firstPage = arcadia.PageGames.Select(tile => tile.Game.Key).ToHashSet();
        var shown = await DecodedAsync(window, expected: 25);

        // The page's 24 covers filling 146 × 219 (the focused one lifted by 7 %) and the focused game's background in the
        // preview at 400 × 180; nothing else.
        var focused = arcadia.PageGames[0].Game.Key;
        Assert.Equal(firstPage, shown.Where(d => d.File.Kind == ArtworkKind.Cover).Select(d => d.File.GameKey).ToHashSet());
        Assert.All(shown.Where(d => d.File.Kind == ArtworkKind.Cover && d.File.GameKey != focused), d => AssertFills(d.Size, 146, 219));
        AssertFills(Assert.Single(shown, d => d.File.Kind == ArtworkKind.Cover && d.File.GameKey == focused).Size, 157, 235);
        var preview = Assert.Single(shown, d => d.File.Kind == ArtworkKind.Hero);
        Assert.Equal(focused, preview.File.GameKey);
        AssertFills(preview.Size, 400, 180);

        window.Press(UiAction.NextPage);
        var secondPage = arcadia.PageGames.Select(tile => tile.Game.Key).ToHashSet();
        var turned = await DecodedAsync(window, expected: 25);
        Assert.Equal(secondPage, turned.Where(d => d.File.Kind == ArtworkKind.Cover).Select(d => d.File.GameKey).ToHashSet());

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        window.Settle().WaitForTransitions();
        var detail = await DecodedAsync(window, expected: 2);
        var shownDetail = arcadia.Detail!.Current.Tile.Game.Key;
        Assert.Contains(detail, d => d.File.Kind == ArtworkKind.Cover && Fills(d.Size, 210, 315) && d.File.GameKey == shownDetail);
        Assert.Contains(detail, d => d.File.Kind == ArtworkKind.Hero && Fills(d.Size, 1152, 372) && d.File.GameKey == shownDetail);
        // The carousel's pages beside it are ready for the next turn; nothing of the library's games behind it.
        var carousel = arcadia.Detail.Pages.Select(page => page.Tile.Game.Key).ToHashSet();
        Assert.All(detail, d => Assert.Contains(d.File.GameKey, carousel));

        window.Press(UiAction.Back).Press(UiAction.Search).Type("Spiel 07").Settle();
        var hits = await DecodedAsync(window, expected: 1);
        Assert.Contains(hits, d => d.File.Kind == ArtworkKind.Cover && Fills(d.Size, 190, 285));
        Assert.All(window.Window.GetVisualDescendants().OfType<ArtworkImage>().Where(image => image.IsShowing),
            image => Assert.True(image.IsEffectivelyVisible, "Ein verstecktes Bild ist geladen."));
    }

    /// <summary>
    /// Decoded to fill a <paramref name="width"/> × <paramref name="height"/> frame cropped centred: one side as the frame,
    /// the other as long as the image's format needs (the demo's images come in two formats each, #488); a pixel either way
    /// where the scale rounds.
    /// </summary>
    private static bool Fills(PixelSize size, int width, int height) =>
        (Math.Abs(size.Width - width) <= 1 && size.Height >= height - 1) || (Math.Abs(size.Height - height) <= 1 && size.Width >= width - 1);

    private static void AssertFills(PixelSize size, int width, int height) =>
        Assert.True(Fills(size, width, height), $"{size.Width} × {size.Height} füllt nicht {width} × {height}.");

    /// <summary>Lets the window draw until at least <paramref name="expected"/> images were decoded and no more follow; those since the last call.</summary>
    private async Task<List<(ArtworkFile File, PixelSize Size)>> DecodedAsync(ArcadiaWindow window, int expected)
    {
        var quiet = 0;
        var count = -1;
        for (var round = 0; round < 500 && quiet < 10; round++)
        {
            window.Settle();
            await Task.Delay(10);
            int now;
            lock (_decoded)
            {
                now = _decoded.Count;
            }

            quiet = now == count && now >= expected ? quiet + 1 : 0;
            count = now;
        }

        lock (_decoded)
        {
            var since = _decoded.ToList();
            _decoded.Clear();
            return since;
        }
    }

    private sealed class SwitchedArtwork(ILauncherArtwork inner) : ILauncherArtwork, IArtworkLookup
    {
        public volatile bool IsOn;

        public IArtworkLookup Look() => IsOn ? inner.Look() : this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => [];
    }
}
