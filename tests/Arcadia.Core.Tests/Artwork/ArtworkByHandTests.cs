using Arcadia.Core.Artwork;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #490: what the player chooses in "Artwork ändern": offers, own files, set by hand, "Standard wiederherstellen" and
/// "Alle Artworks neu laden" with and without "Von Hand gesetzte Artworks überschreiben".
/// </summary>
public sealed class ArtworkByHandTests : IDisposable
{
    private const string Key = "steam:4000001";

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-hand-").FullName;
    private readonly FakeImageServer _server = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly Source _source = new();
    private readonly FakeSuggestions _suggestions = new();
    private ArtworkService _service;

    public ArtworkByHandTests() => _service = NewService();

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private static readonly Game Game = new()
    {
        Id = "4000001",
        LauncherId = "steam",
        Title = "Made-up Game",
        Launch = new LaunchTarget.ProtocolUri("steam://rungameid/4000001"),
    };

    private static readonly LauncherInfo Steam = new("steam", "Steam", "steam", null);

    private ArtworkCache Cache => new(Path.Combine(_root, "artwork"));

    private ArtworkService NewService() => new(Cache, new HttpClient(_server), _time)
    {
        Suggestions = _suggestions,
        Log = line =>
        {
            lock (_log)
            {
                _log.Add(line);
            }
        },
    };

    private void Restart()
    {
        _service.Dispose();
        _service = NewService();
    }

    private async Task ScanAsync()
    {
        _service.Update([new LibraryArtwork(new LauncherLibrary(Steam, [Game]), _source)]);
        await _service.WhenIdleAsync();
    }

    private string File(string name, byte[] content)
    {
        var path = Path.Combine(_root, "files", name);
        TestImages.Write(path, content);
        return path;
    }

    /// <summary>Steam's cover (600 × 900) and background in the launcher's folder.</summary>
    private void LauncherImages()
    {
        _source.Cover = [new("steam-cache", File: File("steam-cover.jpg", TestImages.Jpeg(600, 900)))];
        _source.Hero = [new("steam-cache", File: File("steam-hero.jpg", TestImages.Jpeg(1920, 620)))];
    }

    private CachedArtwork Stored => Cache.Read(Key)!;

    [Fact]
    [Trait("TestCase", "T490-11")]
    public async Task The_launchers_image_in_use_is_offered_ticked_and_automatic()
    {
        LauncherImages();
        await ScanAsync();

        var choices = await _service.ChoicesAsync(Game, ArtworkKind.Cover);

        var offer = Assert.Single(choices.Offers);
        Assert.True(offer.Is(choices.CurrentImage));
        Assert.Equal((ArtworkSource.Launcher, "steam-cache"), (offer.Source, offer.Origin));
        Assert.False(choices.CurrentImage!.SetByHand);
        Assert.Null(choices.Own);
        Assert.Equal(SuggestionKeyState.Missing, choices.KeyState);
    }

    [Fact]
    [Trait("TestCase", "T490-27")]
    public async Task Without_a_key_the_source_of_more_images_is_never_asked()
    {
        LauncherImages();
        await ScanAsync();

        var choices = await _service.ChoicesAsync(Game, ArtworkKind.Hero);

        Assert.Single(choices.Offers);
        Assert.Equal(0, _suggestions.Asked);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    [Trait("TestCase", "T490-6")]
    [Trait("TestCase", "T490-25")]
    public async Task An_offer_taken_is_set_by_hand_and_survives_a_changed_launcher_image_a_rescan_and_a_restart()
    {
        LauncherImages();
        await ScanAsync();
        _suggestions.KeyState = SuggestionKeyState.Valid;
        _suggestions.Images = [new("steamgriddb", new Uri("https://grid.example/cover-a.png"), 12)];
        _server.Serve("https://grid.example/cover-a.png", TestImages.Png(300, 450));
        var offer = (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers.Single(o => o.Source == ArtworkSource.SteamGridDb);

        Assert.True(await _service.UseAsync(Game, ArtworkKind.Cover, offer));

        Assert.Equal(new ImageSize(300, 450), _service.Of(Key)!.Cover!.Size);
        Assert.Equal((ArtworkSource.SteamGridDb, true), (Stored.Cover!.Source, Stored.Cover.SetByHand));
        _source.Cover = [new("steam-cache", File: File("steam-cover-new.jpg", TestImages.Jpeg(400, 600)))];
        await ScanAsync();
        Restart();
        await ScanAsync();
        Assert.Equal(new ImageSize(300, 450), _service.Of(Key)!.Cover!.Size);
        var again = await _service.ChoicesAsync(Game, ArtworkKind.Cover);
        Assert.True(again.Offers.Single(o => o.Source == ArtworkSource.SteamGridDb).Is(again.CurrentImage));
        Assert.Equal(again.CurrentImage, await _service.InUseAsync(Game, ArtworkKind.Cover));
        Assert.Equal(12, again.Offers.Single(o => o.Source == ArtworkSource.SteamGridDb).Score);
    }

    [Fact]
    [Trait("TestCase", "T490-7")]
    public async Task Taking_the_image_already_in_use_makes_it_set_by_hand()
    {
        LauncherImages();
        await ScanAsync();
        var before = _service.Of(Key)!.Cover!;

        Assert.True(await _service.UseAsync(Game, ArtworkKind.Cover, (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers.Single()));

        Assert.True(Stored.Cover!.SetByHand);
        Assert.Equal(before.Path, _service.Of(Key)!.Cover!.Path);
    }

    [Fact]
    [Trait("TestCase", "T490-8")]
    [Trait("TestCase", "T490-9")]
    public async Task Restoring_gives_only_that_kind_back_to_arcadias_choice_and_keeps_the_own_file_offered()
    {
        LauncherImages();
        await ScanAsync();
        Assert.Equal(OwnFileResult.Taken, await _service.UseFileAsync(Game, ArtworkKind.Cover, File("mine.png", TestImages.Png(200, 300))));
        Assert.Equal(OwnFileResult.Taken, await _service.UseFileAsync(Game, ArtworkKind.Hero, File("mine-hero.png", TestImages.Png(1550, 500))));

        await _service.RestoreAsync(Game, ArtworkKind.Cover);

        Assert.Equal(new ImageSize(600, 900), _service.Of(Key)!.Cover!.Size);
        Assert.Equal((ArtworkSource.Launcher, false), (Stored.Cover!.Source, Stored.Cover.SetByHand));
        Assert.Equal((ArtworkSource.File, true), (Stored.Hero!.Source, Stored.Hero.SetByHand));
        var choices = await _service.ChoicesAsync(Game, ArtworkKind.Cover);
        Assert.Equal((new ImageSize(200, 300), "mine.png", false), (choices.Own!.Size, choices.OwnName, choices.OwnInUse));

        // Automatic again: the next scan follows the launcher.
        _source.Cover = [new("steam-cache", File: File("steam-cover-new.jpg", TestImages.Jpeg(400, 600)))];
        await ScanAsync();
        Assert.Equal(new ImageSize(400, 600), _service.Of(Key)!.Cover!.Size);
    }

    [Fact]
    [Trait("TestCase", "T490-11")]
    public async Task Restoring_a_game_without_launcher_images_shows_the_placeholder()
    {
        await ScanAsync();
        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("mine.png", TestImages.Png(200, 300)));

        await _service.RestoreAsync(Game, ArtworkKind.Cover);

        Assert.Null(_service.Of(Key));
        Assert.NotNull(Stored.OwnCover);
        Assert.Empty((await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers);
    }

    [Fact]
    [Trait("TestCase", "T490-15")]
    public async Task An_own_file_of_exactly_20_mb_is_taken_and_one_byte_more_is_refused()
    {
        LauncherImages();
        await ScanAsync();
        var exact = File("exact.jpg", TestImages.JpegOfLength(200, 300, ImageCheck.MaxBytes));

        Assert.Equal(OwnFileResult.Taken, await _service.UseFileAsync(Game, ArtworkKind.Cover, exact));
        Assert.Equal(OwnFileResult.TooLarge, await _service.UseFileAsync(Game, ArtworkKind.Hero, File("big.jpg", TestImages.JpegOfLength(1550, 500, ImageCheck.MaxBytes + 1))));

        Assert.Equal(new ImageSize(1920, 620), _service.Of(Key)!.Hero!.Size);
        Assert.Null(Stored.OwnHero);
    }

    [Theory]
    [Trait("TestCase", "T490-16")]
    [InlineData("text")]
    [InlineData("truncated")]
    [InlineData("gif")]
    [InlineData("animated-webp")]
    public async Task A_file_that_is_no_readable_image_is_refused_and_changes_nothing(string kind)
    {
        LauncherImages();
        await ScanAsync();
        var content = kind switch
        {
            "text" => "not an image, only text"u8.ToArray(),
            "truncated" => TestImages.Jpeg(200, 300)[..60],
            "gif" => [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x3B],
            _ => TestImages.AnimatedWebP(200, 300),
        };

        Assert.Equal(OwnFileResult.NotAnImage, await _service.UseFileAsync(Game, ArtworkKind.Cover, File("picture.png", content)));

        Assert.Equal(new ImageSize(600, 900), _service.Of(Key)!.Cover!.Size);
        Assert.Null(Stored.OwnCover);
    }

    [Fact]
    [Trait("TestCase", "T490-18")]
    [Trait("TestCase", "T490-19")]
    public async Task An_own_file_is_copied_under_arcadias_name_and_stays_after_the_original_is_gone_without_its_path_in_the_log()
    {
        await ScanAsync();
        var original = File(Path.Combine("Made-up User", "..weird name..jpg"), TestImages.Png(200, 300));

        Assert.Equal(OwnFileResult.Taken, await _service.UseFileAsync(Game, ArtworkKind.Cover, original));
        System.IO.File.Delete(original);
        Restart();
        await ScanAsync();

        var shown = _service.Of(Key)!.Cover!;
        Assert.Equal(Path.Combine(Cache.FolderOf(Key), "own-cover.png"), shown.Path);
        Assert.Equal(new ImageSize(200, 300), shown.Size);
        Assert.DoesNotContain(_log, line => line.Contains("Made-up User", StringComparison.Ordinal) || line.Contains("weird", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T490-14")]
    public async Task The_own_file_stays_offered_after_another_choice_and_a_new_one_replaces_it()
    {
        LauncherImages();
        await ScanAsync();
        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("first.jpg", TestImages.Jpeg(200, 300)));
        var launcher = (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers.Single();
        await _service.UseAsync(Game, ArtworkKind.Cover, launcher);
        Assert.Equal(new ImageSize(600, 900), _service.Of(Key)!.Cover!.Size);

        Assert.True(await _service.UseOwnFileAsync(Game, ArtworkKind.Cover));
        Assert.Equal(new ImageSize(200, 300), _service.Of(Key)!.Cover!.Size);
        Assert.Null((await _service.ChoicesAsync(Game, ArtworkKind.Hero)).Own);

        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("second.png", TestImages.Png(400, 600)));
        Assert.Equal("second.png", (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).OwnName);
        Assert.False(System.IO.File.Exists(Path.Combine(Cache.FolderOf(Key), "own-cover.jpg")));
    }

    [Fact]
    [Trait("TestCase", "T490-23")]
    public async Task Reloading_all_keeps_images_set_by_hand_while_the_switch_is_off()
    {
        LauncherImages();
        await ScanAsync();
        await _service.UseAsync(Game, ArtworkKind.Cover, (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers.Single());
        await _service.UseFileAsync(Game, ArtworkKind.Hero, File("mine-hero.png", TestImages.Png(1550, 500)));
        var cover = System.IO.File.GetLastWriteTimeUtc(_service.Of(Key)!.Cover!.Path);
        _source.Cover = [new("steam-cache", File: File("steam-cover-new.jpg", TestImages.Jpeg(400, 600)))];

        Assert.True(_service.ReloadAll(overwriteSetByHand: false));
        await _service.WhenIdleAsync();

        Assert.Equal(new ImageSize(600, 900), _service.Of(Key)!.Cover!.Size);
        Assert.Equal(cover, System.IO.File.GetLastWriteTimeUtc(_service.Of(Key)!.Cover!.Path));
        Assert.Equal((ArtworkSource.File, true), (Stored.Hero!.Source, Stored.Hero.SetByHand));
    }

    [Fact]
    [Trait("TestCase", "T490-24")]
    public async Task Reloading_all_with_the_switch_on_replaces_them_and_keeps_the_own_file_offered()
    {
        LauncherImages();
        await ScanAsync();
        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("mine.png", TestImages.Png(200, 300)));

        Assert.True(_service.ReloadAll(overwriteSetByHand: true));
        await _service.WhenIdleAsync();

        Assert.Equal(new ImageSize(600, 900), _service.Of(Key)!.Cover!.Size);
        Assert.Equal((ArtworkSource.Launcher, false), (Stored.Cover!.Source, Stored.Cover.SetByHand));
        Assert.NotNull((await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Own);
    }

    [Fact]
    public async Task Reloading_all_loads_automatic_images_again_and_keeps_one_it_cannot_load()
    {
        _source.Cover = [new("steam-server", Download: new Uri("https://images.example/4000001/cover.jpg"))];
        _server.Serve("https://images.example/4000001/cover.jpg", TestImages.Jpeg(600, 900));
        await ScanAsync();
        Assert.False(new ArtworkService(Cache, new HttpClient(_server), _time).ReloadAll(false));

        _server.Offline = true;
        _service.ReloadAll(overwriteSetByHand: false);
        await _service.WhenIdleAsync();
        Assert.NotNull(_service.Of(Key)!.Cover);

        _server.Offline = false;
        var asked = _server.Requests.Count;
        _service.ReloadAll(overwriteSetByHand: false);
        await _service.WhenIdleAsync();
        Assert.Equal(asked + 1, _server.Requests.Count);
    }

    [Fact]
    [Trait("TestCase", "T490-26")]
    public async Task A_choice_for_a_game_with_unreadable_metadata_writes_it_anew_and_keeps_the_rest_protected()
    {
        var cache = Cache;
        cache.WriteImage(Key, ArtworkKind.Hero, TestImages.Jpeg(1920, 620), ".jpg");
        System.IO.File.WriteAllText(Path.Combine(cache.FolderOf(Key), ArtworkCache.MetadataFile), "not json");
        LauncherImages();
        await ScanAsync();

        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("mine.png", TestImages.Png(200, 300)));

        var stored = Stored;
        Assert.False(stored.Unreadable);
        Assert.Equal((ArtworkSource.File, true), (stored.Cover!.Source, stored.Cover.SetByHand));
        Assert.True(stored.Hero!.SetByHand);
    }

    [Fact]
    public async Task The_source_offers_at_most_20_images_and_leaves_out_those_it_cannot_load()
    {
        _suggestions.KeyState = SuggestionKeyState.Valid;
        _suggestions.Images = [.. Enumerable.Range(1, 25).Select(n => new SuggestedImage("steamgriddb", new Uri($"https://grid.example/{n}.png"), 30 - n))];
        foreach (var n in Enumerable.Range(1, 25).Where(n => n != 3))
        {
            _server.Serve($"https://grid.example/{n}.png", TestImages.Png(200, 300));
        }

        await ScanAsync();
        var offers = (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers;

        Assert.Equal(19, offers.Count);
        Assert.Equal([29, 28, 26], offers.Take(3).Select(o => o.Score!.Value));
    }

    [Fact]
    public async Task An_offered_image_the_view_cannot_decode_is_not_the_games_image()
    {
        LauncherImages();
        _source.Cover = [new("steam-server", Download: new Uri("https://images.example/4000001/cover.jpg"))];
        _server.Serve("https://images.example/4000001/cover.jpg", TestImages.Jpeg(600, 900));
        await ScanAsync();
        var shown = _service.Of(Key)!.Cover!;
        await _service.UseFileAsync(Game, ArtworkKind.Cover, File("mine.png", TestImages.Png(200, 300)));
        var offer = (await _service.ChoicesAsync(Game, ArtworkKind.Cover)).Offers.Single();
        Assert.NotEqual(shown.Path, offer.File.Path);

        _service.ReportBroken(offer.File);

        Assert.DoesNotContain(_log, line => line.Contains("beschädigt", StringComparison.Ordinal));
    }

    private sealed class Source : ILauncherArtwork, IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> Cover { get; set; } = [];

        public IReadOnlyList<ArtworkCandidate> Hero { get; set; } = [];

        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;
    }

    private sealed class FakeSuggestions : IArtworkSuggestions
    {
        public SuggestionKeyState KeyState { get; set; }

        public IReadOnlyList<SuggestedImage> Images { get; set; } = [];

        public int Asked { get; private set; }

        public Task<IReadOnlyList<SuggestedImage>> FindAsync(Game game, ArtworkKind kind, CancellationToken cancel)
        {
            Asked++;
            return Task.FromResult(Images);
        }
    }
}
