using System.Net;
using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// #487 in the real window (Epic's source in the same window: <see cref="EpicArtworkWindowTests"/>): the images fill their frames cropped centred, swap in for the placeholder without moving
/// anything, show the right game wherever a slot is reused, and never hold up the library. Every game's images are made up
/// in one colour of its own, so a pixel tells which game's image is drawn.
/// </summary>
public sealed class ArtworkWindowTests : IDisposable
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private readonly string _images = Directory.CreateTempSubdirectory("arcadia-artwork-window-").FullName;

    public void Dispose() => Directory.Delete(_images, recursive: true);

    [AvaloniaFact]
    [Trait("TestCase", "T487-11")]
    public async Task Cover_and_background_fill_their_frames_cropped_centred_under_the_badges_shade_and_ring()
    {
        var installed = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Beta");
        var source = new FixedArtwork(_images);
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, installed, notInstalled) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers);
        var arcadia = window.Arcadia;
        var green = Color.FromRgb(40, 200, 60);
        var blue = Color.FromRgb(40, 60, 220);
        var red = Color.FromRgb(230, 30, 30);

        // A cover taller than 2 : 3 and a background wider than 3.1 : 1. The background's outer 200 columns on each side
        // are red: a centred crop to the header (3.1 : 1 or wider keeps at most 3,440 of 3,840) cuts them away, a stretched
        // or fitted one shows them.
        foreach (var game in new[] { installed, notInstalled })
        {
            source.Set(game, ArtworkKind.Cover, DemoArtwork.Png(600, 1000, _ => (green.R, green.G, green.B)));
            source.Set(game, ArtworkKind.Hero, DemoArtwork.Png(3840, 1000, x => x is < 200 or >= 3640 ? (red.R, red.G, red.B) : (blue.R, blue.G, blue.B)));
        }

        await ShowArtworkAsync(window, source, installed, notInstalled);

        var tile = window.Tile(arcadia.PageGames[0]);
        var art = ArtOf(tile);
        using (var frame = window.Capture())
        {
            var bounds = window.Bounds(art);
            // No bars: the image reaches all four sides (just inside the rounded corners' straight part).
            foreach (var point in new[]
            {
                new Point(bounds.Left + 1, bounds.Center.Y), new Point(bounds.Right - 2, bounds.Center.Y),
                new Point(bounds.Center.X, bounds.Top + 1), new Point(bounds.Center.X, bounds.Bottom - 2),
            })
            {
                AssertColour(frame.At(point), green, $"Cover bei {point}");
            }

            // Rounded: the very corner isn't the image.
            Assert.True(ArcadiaWindow.Distance(frame.At(new Point(bounds.Left + 0.5, bounds.Top + 0.5)), green) > 30, "Die Ecke des Covers ist nicht abgerundet.");

            // The shade of the uninstalled game lies over its image.
            var shaded = window.Bounds(ArtOf(window.Tile(arcadia.PageGames[1])));
            Assert.True(ArcadiaWindow.Distance(frame.At(shaded.Center), green) > 60, "Der Schatten für „Nicht installiert“ liegt nicht über dem Cover.");
        }

        // Badges, the shade and the title lie above the image: it is the first child of every tile's panel.
        foreach (var image in window.Window.GetVisualDescendants().OfType<ArtworkImage>().Where(i => i.Name is "CoverArt" or "HitArt" or "DetailCoverArt"))
        {
            Assert.Equal(0, ((Panel)image.GetVisualParent()!).Children.IndexOf(image));
        }

        window.AssertRingAround(tile, "Cover mit Bild", ring: "FocusRingLifted");

        // The background on the detail page fills the screen under the side scrim: blue up to its left and right edges,
        // no red band. Looked at above the stacks and below the top scrim.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        window.Settle().WaitForTransitions();
        await WaitShowingAsync(window);
        var hero = ShownArt(window).Single(i => i.Name == "HeroArt");
        using (var frame = window.Capture())
        {
            var bounds = window.Bounds(hero);
            var y = bounds.Top + (bounds.Height * 0.3);
            foreach (var point in new[] { new Point(bounds.Left + 2, y), new Point(bounds.Right - 2, y) })
            {
                AssertColour(frame.At(point), UnderSideScrim(blue, (point.X - bounds.Left) / bounds.Width), $"Hintergrund bei {point}");
            }
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T487-18")]
    public async Task The_image_takes_the_placeholders_place_without_moving_the_grid_or_the_focus()
    {
        var games = Enumerable.Range(1, 30).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D2}", DateTimeOffset.UnixEpoch)).ToArray();
        var source = new FixedArtwork(_images);
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers);
        var arcadia = window.Arcadia;
        window.Press(UiAction.Right);
        var focused = arcadia.Main.Library.FocusedGame!;
        var tile = window.Tile(focused);
        var grid = window.Named<ItemsControl>("GameGrid");
        var (gridBounds, tileBounds) = (window.LayoutBounds(grid), window.LayoutBounds(tile));
        Assert.True(Monogram(tile).IsEffectivelyVisible, "Ohne Bild fehlt der Platzhalter.");

        foreach (var (game, i) in games.Select((g, i) => (g, i)))
        {
            source.Set(game, ArtworkKind.Cover, Solid(i, 300, 450));
            source.Set(game, ArtworkKind.Hero, Solid(i, 620, 200));
        }

        await ShowArtworkAsync(window, source, games);

        Assert.Same(focused, arcadia.Main.Library.FocusedGame);
        Assert.True(focused.IsFocused);
        Assert.Equal(gridBounds, window.LayoutBounds(grid));
        Assert.Equal(tileBounds, window.LayoutBounds(tile));
        Assert.False(Monogram(tile).IsEffectivelyVisible, "Das Monogramm liegt noch über dem Bild.");
        Assert.True(ShownArt(window).Single(i => i.Name == "PreviewArt").IsShowing, "Die Vorschau zeigt keinen Hintergrund.");
        AssertEveryImageShowsItsGame(window, games);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T487-18")]
    public async Task On_the_open_detail_page_and_search_the_image_swaps_in_where_the_player_is()
    {
        var games = Enumerable.Range(1, 30).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D2}", DateTimeOffset.UnixEpoch)).ToArray();
        var source = new FixedArtwork(_images);
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers);
        var arcadia = window.Arcadia;
        foreach (var (game, i) in games.Select((g, i) => (g, i)))
        {
            source.Set(game, ArtworkKind.Cover, Solid(i, 300, 450));
            source.Set(game, ArtworkKind.Hero, Solid(i, 620, 200));
        }

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        window.Settle().WaitForTransitions();
        var detail = arcadia.Detail!.Current;
        await ShowArtworkAsync(window, source, games);
        Assert.Same(detail, arcadia.Detail!.Current);
        Assert.Contains(ShownArt(window), i => i.Name == "HeroArt");
        Assert.Contains(ShownArt(window), i => i.Name == "DetailCoverArt");
        AssertEveryImageShowsItsGame(window, games);

        window.Press(UiAction.Back).Press(UiAction.Search);
        arcadia.Main.TypeInSearch("Spiel 1");
        window.Press(UiAction.Back).Settle().WaitForTransitions();
        var hit = arcadia.Search!.FocusedResult;
        await WaitShowingAsync(window);
        Assert.Same(hit, arcadia.Search.FocusedResult);
        Assert.Contains(ShownArt(window), i => i.Name == "HitArt");
        AssertEveryImageShowsItsGame(window, games);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T487-22")]
    public async Task With_the_controller_alone_every_place_shows_its_own_games_images()
    {
        var games = Enumerable.Range(1, 30).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D2}", DateTimeOffset.UnixEpoch)).ToArray();
        var source = new FixedArtwork(_images);
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games) with { Artwork = source }];
        using var window = await ArcadiaWindow.ShowAsync(launchers);
        var arcadia = window.Arcadia;
        foreach (var (game, i) in games.Select((g, i) => (g, i)))
        {
            source.Set(game, ArtworkKind.Cover, Solid(i, 300, 450));
            source.Set(game, ArtworkKind.Hero, Solid(i, 620, 200));
        }

        await ShowArtworkAsync(window, source, games);
        AssertEveryImageShowsItsGame(window, games, "Bibliothek");
        await WalkWithTheControllerAsync(window, games);
    }

    /// <summary>
    /// T487-22's walk over 30 games named "Spiel 01" to "Spiel 30", 24 per page: library, detail page, the next page, the
    /// search and a hit's detail page, back with B; every place shows its own game's images.
    /// </summary>
    internal static async Task WalkWithTheControllerAsync(ArcadiaWindow window, Game[] games)
    {
        var arcadia = window.Arcadia;
        window.Press(UiAction.Right, times: 3);
        await Step(window, games, "Fokus auf dem vierten Cover");
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        await Step(window, games, "Detailseite");
        window.Press(UiAction.NextPage);
        await Step(window, games, "Detailseite, nächstes Spiel");
        window.Press(UiAction.Back);
        await Step(window, games, "zurück in der Bibliothek");
        window.Press(UiAction.NextPage);
        await Step(window, games, "Seite 2");
        Assert.Equal(6, arcadia.PageGames.Count);

        window.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("Spiel 2");
        window.Press(UiAction.Back);
        await Step(window, games, "Suche");
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        await Step(window, games, "Detailseite aus der Suche");
        Assert.True(arcadia.Detail!.IsFromSearch);
        window.Press(UiAction.Back);
        await Step(window, games, "zurück in der Suche");
        window.Press(UiAction.Back);
        await Step(window, games, "zurück in der Bibliothek");
        Assert.Null(arcadia.Search);
        Assert.Null(arcadia.Detail);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T487-20")]
    public async Task While_every_image_waits_two_seconds_for_the_server_the_library_and_search_stay_fast()
    {
        var games = Enumerable.Range(1, 1000).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D4}", DateTimeOffset.UnixEpoch)).ToArray();
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, games)];
        using var server = new SlowImageServer(TimeSpan.FromSeconds(2));
        using var window = await ArcadiaWindow.ShowAsync(launchers, imageServer: server);

        await AssertFastWhileDownloadingAsync(window, server, new LibraryArtwork(new LauncherLibrary(KnownLaunchers.Gog, games), new DownloadArtwork()));
    }

    /// <summary>
    /// While a run downloads <paramref name="library"/>'s images from <paramref name="server"/>, a focus move stays under
    /// 50 ms and the search opens as fast as without a run.
    /// </summary>
    internal static async Task AssertFastWhileDownloadingAsync(ArcadiaWindow window, SlowImageServer server, LibraryArtwork library)
    {
        var arcadia = window.Arcadia;

        // The second opening of the search, without a run going: what it must stay at (#477).
        var withoutImages = OpenSearchTwice(window);

        // The games are known from the start: a search, not the scan, downloads their images (#506).
        arcadia.Artwork.Update([library]);
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: false));
        await server.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(arcadia.Artwork.WhenIdleAsync().IsCompleted, "Der Lauf ist schon fertig; er soll während der Messung laden.");

        arcadia.LatencyLines.Clear();
        for (var i = 0; i < 10; i++)
        {
            Tap(window, i % 2 == 0 ? GamepadButton.DPadRight : GamepadButton.DPadLeft);
        }

        var moves = arcadia.LatencyLines.Select(TotalOf).Order().ToList();
        Assert.Equal(10, moves.Count);
        Assert.True(moves[moves.Count / 2] < 50, $"Fokuswechsel im Mittel {moves[moves.Count / 2]:0.0} ms, während die Bilder laden.");
        var whileLoading = OpenSearchTwice(window);
        Assert.True(whileLoading <= (withoutImages * 1.5) + 20, $"Suche öffnet in {whileLoading:0.0} ms statt {withoutImages:0.0} ms ohne Bilder.");
        Assert.False(arcadia.Artwork.WhenIdleAsync().IsCompleted);
    }

    private static double OpenSearchTwice(ArcadiaWindow window)
    {
        var lines = window.Arcadia.LatencyLines;
        Tap(window, GamepadButton.X);
        window.Press(UiAction.Back, times: 2);
        Tap(window, GamepadButton.X);
        Assert.NotNull(window.Arcadia.Search);
        var opened = TotalOf(lines.Last(line => line.Contains("Bild: Search", StringComparison.Ordinal)));
        window.Press(UiAction.Back, times: 2);
        Assert.Null(window.Arcadia.Search);
        return opened;
    }

    /// <summary>A button of the controller, timed from its press to the frame that shows it (<c>Bild:</c> lines).</summary>
    private static void Tap(ArcadiaWindow window, GamepadButton button)
    {
        window.Arcadia.PressButton(button).ReleaseButton(button);
        window.Settle();
    }

    private static double TotalOf(string line) =>
        double.Parse(System.Text.RegularExpressions.Regex.Match(line, @"nach (?<total>\d+\.\d) ms").Groups["total"].Value, System.Globalization.CultureInfo.InvariantCulture);

    internal static async Task Step(ArcadiaWindow window, Game[] games, string where)
    {
        window.Settle().WaitForTransitions();
        await WaitShowingAsync(window);
        AssertEveryImageShowsItsGame(window, games, where);
    }

    private async Task ShowArtworkAsync(ArcadiaWindow window, FixedArtwork source, params Game[] games)
    {
        // The launcher's own source too: a rescan meanwhile (the window's activation) takes the run's place with it.
        window.Arcadia.Artwork.Update([new LibraryArtwork(new LauncherLibrary(KnownLaunchers.Gog, games), source)]);
        await window.Arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await WaitShowingAsync(window);
        // The focused tile's lift starts when the window shows; a capture during it and the bounds read a moment later
        // would disagree at the tile's edges.
        window.WaitForTransitions();
    }

    /// <summary>Lets the window draw until every visible image with a file is decoded and shown.</summary>
    internal static async Task WaitShowingAsync(ArcadiaWindow window)
    {
        for (var round = 0; round < 500; round++)
        {
            window.Settle();
            if (VisibleArt(window).All(image => image.File is null || image.IsShowing))
            {
                window.Settle();
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Die sichtbaren Bilder wurden nicht fertig geladen.");
    }

    /// <summary>Every image drawn on the screen is its own game's: the pixel in it has that game's colour, no other's.</summary>
    internal static void AssertEveryImageShowsItsGame(ArcadiaWindow window, Game[] games, string where = "")
    {
        var colours = games.Select((game, i) => (game.Key, Colour: ColourOf(i))).ToDictionary(g => g.Key, g => g.Colour);
        var shown = ShownArt(window).ToList();
        Assert.NotEmpty(shown);
        using var frame = window.Capture();
        foreach (var image in shown)
        {
            var bounds = window.Bounds(image);
            // Tiles and the preview are one colour: their top band. The detail page's background lies under the side
            // scrim and, at the top, the header's: its right edge above the stack.
            var isBackground = image.Name == "HeroArt";
            var point = isBackground
                ? new Point(bounds.Right - 2, bounds.Top + (bounds.Height * 0.3))
                : new Point(bounds.Center.X, bounds.Top + Math.Min(12, bounds.Height / 4));
            if (IsCovered(window, image, point))
            {
                // Under another page, e.g. the library's preview under the search.
                continue;
            }

            var own = isBackground ? UnderSideScrim(colours[image.File!.GameKey], (point.X - bounds.Left) / bounds.Width) : colours[image.File!.GameKey];
            var drawn = frame.At(point);
            Assert.True(ArcadiaWindow.Distance(drawn, own) <= 12, $"{where}: {image.Name} von {image.File.GameKey} zeigt {drawn} statt {own}.");
        }
    }

    private static IEnumerable<ArtworkImage> VisibleArt(ArcadiaWindow window)
    {
        var client = new Rect(window.Window.ClientSize);
        return window.Window.GetVisualDescendants().OfType<ArtworkImage>()
            .Where(image => image.IsEffectivelyVisible && image.Bounds.Width > 0 && client.Contains(window.Bounds(image).Center)
                && image.GetSelfAndVisualAncestors().All(visual => visual.Opacity >= 0.99));
    }

    /// <summary>Something else (another page) is hit at <paramref name="point"/>, not the image's own tile or panel.</summary>
    private static bool IsCovered(ArcadiaWindow window, ArtworkImage image, Point point)
    {
        var own = image.GetVisualAncestors().Take(4).ToHashSet();
        return window.Window.InputHitTest(point) is Visual hit && !hit.GetSelfAndVisualAncestors().Any(own.Contains);
    }

    private static IEnumerable<ArtworkImage> ShownArt(ArcadiaWindow window) => VisibleArt(window).Where(image => image.IsShowing);

    private static ArtworkImage ArtOf(Control tile) => tile.GetVisualDescendants().OfType<ArtworkImage>().Single();

    private static TextBlock Monogram(Control tile) => tile.GetVisualDescendants().OfType<TextBlock>().First(text => text.FontSize == 56);

    /// <summary>What <paramref name="own"/> looks like under the detail page's side scrim at <paramref name="fraction"/> of its width.</summary>
    private static Color UnderSideScrim(Color own, double fraction)
    {
        var stops = ArcadiaWindow.ThemeResource<LinearGradientBrush>("DetailScrimSide").GradientStops.OrderBy(stop => stop.Offset).ToList();
        var before = stops.Last(stop => stop.Offset <= fraction);
        var after = stops.First(stop => stop.Offset >= fraction);
        var t = after.Offset > before.Offset ? (fraction - before.Offset) / (after.Offset - before.Offset) : 0;
        var alpha = (before.Color.A + ((after.Color.A - before.Color.A) * t)) / 255;
        byte Over(byte image, byte scrim) => (byte)Math.Round((image * (1 - alpha)) + (scrim * alpha));
        return Color.FromRgb(Over(own.R, after.Color.R), Over(own.G, after.Color.G), Over(own.B, after.Color.B));
    }

    private static Color ColourOf(int i) => Color.FromRgb((byte)(30 + (i * 37 % 190)), (byte)(30 + (i * 71 % 190)), (byte)(30 + (i * 113 % 190)));

    internal static byte[] Solid(int i, int width, int height)
    {
        var colour = ColourOf(i);
        return DemoArtwork.Png(width, height, _ => (colour.R, colour.G, colour.B));
    }

    private static void AssertColour(Color drawn, Color expected, string where) =>
        Assert.True(ArcadiaWindow.Distance(drawn, expected) <= 30, $"{where}: {drawn} statt {expected}.");

    /// <summary>A launcher's own images, one file per game and kind, as a test sets them.</summary>
    private sealed class FixedArtwork(string folder) : ILauncherArtwork, IArtworkLookup
    {
        private readonly Dictionary<(string Key, ArtworkKind Kind), string> _files = [];

        public void Set(Game game, ArtworkKind kind, byte[] content)
        {
            var path = Path.Combine(folder, $"{game.Id}-{kind}.png");
            File.WriteAllBytes(path, content);
            _files[(game.Key, kind)] = path;
        }

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) =>
            _files.TryGetValue((game.Key, kind), out var path) ? [new ArtworkCandidate("test", File: path)] : [];
    }

    /// <summary>Every image only from the image server.</summary>
    private sealed class DownloadArtwork : ILauncherArtwork, IArtworkLookup
    {
        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) =>
            [new ArtworkCandidate("test-server", Download: new Uri($"https://images.example/{Uri.EscapeDataString(game.Id)}/{kind}.png"))];
    }

    /// <summary>An image server that answers every request after <paramref name="delay"/> of real time, with no image.</summary>
    internal sealed class SlowImageServer(TimeSpan delay) : HttpMessageHandler
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.TrySetResult();
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.TrySetResult();
            cancellationToken.WaitHandle.WaitOne(delay);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
