using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// #487: Steam's images of a game, best first (T487-2, T487-3, T487-7). Steam's folders as docs/launchers/steam/artwork.md
/// describes them, with made-up AppIDs and accounts; the image server's answers are <see cref="ArtworkServiceTests"/>'.
/// </summary>
public sealed class SteamArtworkTests : IDisposable
{
    private const uint Account = 11111111;

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-steam-artwork-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IArtworkLookup Look(uint? activeUser = Account) => new SteamArtwork(() => new SteamInstallation(_root, activeUser)).Look();

    private static Game SteamGame(string appId) => new()
    {
        Id = appId,
        LauncherId = "steam",
        Title = "Made-up Game",
        Launch = new LaunchTarget.ProtocolUri($"steam://rungameid/{appId}"),
    };

    private string Grid(uint account) => Path.Combine(_root, "userdata", account.ToString(System.Globalization.CultureInfo.InvariantCulture), "config", "grid");

    private string LibraryCache(string appId) => Path.Combine(_root, "appcache", "librarycache", appId);

    [Fact]
    public void Own_images_come_before_steams_cache_and_its_server()
    {
        TestImages.Write(Path.Combine(Grid(Account), "4000001p.png"), TestImages.Png(60, 90));
        TestImages.Write(Path.Combine(Grid(Account), "4000001_hero.jpg"), TestImages.Jpeg(1920, 620));
        TestImages.Write(Path.Combine(LibraryCache("4000001"), "library_600x900.jpg"), TestImages.Jpeg(600, 900));
        TestImages.Write(Path.Combine(LibraryCache("4000001"), "library_hero.jpg"), TestImages.Jpeg(1920, 620));

        var lookup = Look();
        var cover = lookup.For(SteamGame("4000001"), ArtworkKind.Cover);
        var hero = lookup.For(SteamGame("4000001"), ArtworkKind.Hero);

        Assert.Equal(["steam-grid", "steam-cache", "steam-server", "steam-server"], cover.Select(c => c.Origin));
        Assert.EndsWith("4000001p.png", cover[0].File);
        Assert.EndsWith("library_600x900.jpg", cover[1].File);
        Assert.EndsWith("4000001_hero.jpg", hero[0].File);
        Assert.All(cover.Skip(2), c => Assert.Equal("https", c.Download!.Scheme));
        Assert.All(cover.Skip(2), c => Assert.EndsWith("/4000001/library_600x900.jpg", c.Download!.AbsolutePath));
        Assert.All(hero.Skip(2), c => Assert.EndsWith("/4000001/library_hero.jpg", c.Download!.AbsolutePath));
    }

    [Fact]
    public void Steams_cache_is_found_in_a_hash_named_subfolder_too()
    {
        TestImages.Write(Path.Combine(LibraryCache("4000002"), "3f2a9c0d41b7e865", "library_600x900.jpg"), TestImages.Jpeg(600, 900));

        var lookup = Look();
        var cover = lookup.For(SteamGame("4000002"), ArtworkKind.Cover);
        var hero = lookup.For(SteamGame("4000002"), ArtworkKind.Hero);

        Assert.Equal("steam-cache", cover[0].Origin);
        Assert.Contains("3f2a9c0d41b7e865", cover[0].File);
        Assert.DoesNotContain(hero, c => c.File is not null);
    }

    [Fact]
    public void Only_the_grid_of_the_account_whose_library_shows_is_read()
    {
        const uint other = 22222222;
        TestImages.Write(Path.Combine(Grid(other), "4000003p.png"), TestImages.Png(60, 90));
        Directory.CreateDirectory(Grid(Account));

        Assert.DoesNotContain(Look().For(SteamGame("4000003"), ArtworkKind.Cover), c => c.Origin == "steam-grid");
        Assert.Contains(Look(activeUser: other).For(SteamGame("4000003"), ArtworkKind.Cover), c => c.Origin == "steam-grid");
    }

    [Theory]
    [InlineData("../1")]
    [InlineData("1?x=")]
    [InlineData("12/../../x")]
    [InlineData("")]
    [InlineData("99999999999")]
    [InlineData("１２３")]
    public void An_appid_that_is_no_plain_number_names_no_file_and_no_address(string appId)
    {
        TestImages.Write(Path.Combine(LibraryCache("1"), "library_600x900.jpg"), TestImages.Jpeg(600, 900));

        Assert.Empty(Look().For(SteamGame(appId), ArtworkKind.Cover));
        Assert.Empty(Look().For(SteamGame(appId), ArtworkKind.Hero));
    }

    [Fact]
    public void Without_steam_only_the_server_is_offered()
    {
        var lookup = new SteamArtwork(() => null).Look();

        Assert.All(lookup.For(SteamGame("4000004"), ArtworkKind.Cover), c => Assert.NotNull(c.Download));
    }
}
