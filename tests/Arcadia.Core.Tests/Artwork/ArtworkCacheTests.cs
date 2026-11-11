using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#487: the artwork cache on disk (T487-7, T487-12, T487-14).</summary>
public sealed class ArtworkCacheTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-artwork-cache-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("steam:../../1")]
    [InlineData("steam:..\\..\\Windows")]
    [InlineData("steam:C:\\x")]
    [InlineData("steam:/etc/passwd")]
    [InlineData("epic:CON")]
    [InlineData("steam:a.")]
    public void No_game_key_leads_outside_the_cache(string gameKey)
    {
        var cache = new ArtworkCache(_root);
        var folder = cache.FolderOf(gameKey);

        Assert.Equal(cache.Root, Path.GetDirectoryName(folder));
        Assert.Matches("^[a-z0-9~-]+$", Path.GetFileName(folder));
    }

    [Fact]
    public void Keys_that_differ_only_in_case_get_folders_of_their_own() =>
        Assert.NotEqual(new ArtworkCache(_root).FolderOf("epic:Fortnite").ToLowerInvariant(), new ArtworkCache(_root).FolderOf("epic:fortnite").ToLowerInvariant());

    [Fact]
    public void A_very_long_key_gets_a_short_folder()
    {
        var cache = new ArtworkCache(_root);

        Assert.True(Path.GetFileName(cache.FolderOf("epic:" + new string('x', 500))).Length <= 100);
    }

    [Fact]
    public void Holds_the_images_and_their_source_and_set_by_hand()
    {
        var cache = new ArtworkCache(_root);
        var file = cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Jpeg(600, 900), ".jpg");
        var artwork = new CachedArtwork("steam:4000001", new CachedImage(file, ArtworkSource.Launcher, "steam-cache", "1:2"), null);
        cache.WriteMetadata(artwork);

        Assert.Equal(artwork, cache.Read("steam:4000001"));
        Assert.Equal(artwork, Assert.Single(cache.All()));
        var json = File.ReadAllText(Path.Combine(cache.FolderOf("steam:4000001"), ArtworkCache.MetadataFile));
        Assert.Contains("\"source\": \"launcher\"", json);
        Assert.Contains("\"setByHand\": false", json);
        Assert.Equal(["artwork.json", "cover.jpg"], Directory.GetFiles(cache.FolderOf("steam:4000001")).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void A_new_image_of_another_type_replaces_the_old_and_leftover_temporary_files_go()
    {
        var cache = new ArtworkCache(_root);
        cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Jpeg(600, 900), ".jpg");
        var folder = cache.FolderOf("steam:4000001");
        File.WriteAllBytes(Path.Combine(folder, "cover.png.1a2b3c4d.tmp"), [1, 2, 3]);

        cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Png(60, 90), ".png");

        Assert.Equal(["cover.png"], Directory.GetFiles(folder).Select(Path.GetFileName));
    }

    [Fact]
    public void An_unreadable_metadata_file_makes_every_image_set_by_hand()
    {
        var cache = new ArtworkCache(_root);
        cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Jpeg(600, 900), ".jpg");
        cache.WriteImage("steam:4000001", ArtworkKind.Hero, TestImages.Jpeg(1920, 620), ".jpg");
        File.WriteAllText(Path.Combine(cache.FolderOf("steam:4000001"), ArtworkCache.MetadataFile), "{ \"game\": \"steam:40");

        var artwork = cache.Read("steam:4000001")!;

        Assert.True(artwork.Unreadable);
        Assert.True(artwork.Cover!.SetByHand);
        Assert.True(artwork.Hero!.SetByHand);
        Assert.Empty(cache.All());
    }

    [Fact]
    public void Images_without_metadata_are_left_from_a_run_that_ended_and_count_as_missing()
    {
        var cache = new ArtworkCache(_root);
        cache.WriteImage("steam:4000001", ArtworkKind.Cover, TestImages.Jpeg(600, 900), ".jpg");

        Assert.Null(cache.Read("steam:4000001"));
    }

    [Fact]
    public void A_metadata_file_naming_a_file_outside_the_folder_counts_as_missing()
    {
        var cache = new ArtworkCache(_root);
        var folder = Directory.CreateDirectory(cache.FolderOf("steam:4000001")).FullName;
        File.WriteAllText(Path.Combine(folder, ArtworkCache.MetadataFile), """
            { "game": "steam:4000001", "cover": { "file": "../../secret.jpg", "source": "launcher", "origin": "x", "stamp": "y" } }
            """);

        Assert.Null(cache.Read("steam:4000001")!.Cover);
    }

    [Fact]
    public void Without_images_the_games_folder_goes()
    {
        var cache = new ArtworkCache(_root);
        cache.WriteMetadata(new CachedArtwork("steam:4000001", new CachedImage("cover.jpg", ArtworkSource.Launcher, "x", "y"), null));

        cache.WriteMetadata(new CachedArtwork("steam:4000001", null, null));

        Assert.False(Directory.Exists(cache.FolderOf("steam:4000001")));
    }
}
