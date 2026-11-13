using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #494 part 2: entries marked "Metadaten" on "Warten auf deine Auswahl", through the view models and the controller's
/// buttons, beside the artwork entries of #491. A made-up launcher without images or store, a fake SteamGridDB that knows
/// two "Echo Drift", a fake IGDB that knows two "Echo Drift" and two "Köln"; no window.
/// </summary>
public sealed class WaitingListMetadataTests
{
    private static readonly Game Echo = WaitingListTests.GameOf("echo", "Echo Drift");
    private static readonly Game Koeln = WaitingListTests.GameOf("koeln", "K?ln");
    private const string CoverAddress = "https://images.igdb.com/igdb/image/upload/t_cover_big/co2015.jpg";

    /// <summary>"Echo Drift" waits for artwork and metadata, "K?ln" for metadata; access and key valid, both searches done.</summary>
    private static async Task<ArcadiaDriver> StartAsync()
    {
        var images = new FakeImageServer();
        images.Serve("https://cdn.example/grid/501.png", TestImages.Png(600, 900));
        images.Serve("https://cdn.example/grid/502.png", TestImages.Png(600, 900));
        images.Serve(CoverAddress, TestImages.Png(264, 374));
        var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(WaitingListTests.Other, [Echo, Koeln]) with { Metadata = new FakeMetadataSource { Store = null } }],
            WaitingListTests.Settings,
            imageServer: images);
        arcadia.Igdb.ServeName(
            "Echo Drift",
            FakeIgdb.Game(11, "Echo Drift", extra: "\"first_release_date\":1420070400,\"summary\":\"The 2015 one.\",\"cover\":{\"image_id\":\"co2015\"}"),
            FakeIgdb.Game(12, "Echo Drift", extra: "\"summary\":\"The undated one.\""));
        arcadia.Igdb.ServeName("K ln", FakeIgdb.Game(21, "Köln", extra: "\"summary\":\"First Köln.\""), FakeIgdb.Game(22, "Köln", extra: "\"summary\":\"Second Köln.\""));
        await arcadia.Metadata.Igdb!.SetClientIdAsync(FakeIgdb.ClientId);
        await arcadia.Metadata.Igdb.SetSecretAsync(FakeIgdb.Secret);
        arcadia.Metadata.FetchMissing();
        await RunToIdleAsync(arcadia);

        arcadia.SteamGridDb.Finds("Echo Drift", (501, "Echo Drift", 2019), (502, "Echo Drift", 2023))
            .Cover("game", "501", "https://cdn.example/grid/501.png", 600, 900)
            .Cover("game", "502", "https://cdn.example/grid/502.png", 600, 900);
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        arcadia.Artwork.SearchAgain(byPlayer: false);
        await WaitingListTests.SettledAsync(arcadia);
        return arcadia;
    }

    private static async Task RunToIdleAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 3000 && !arcadia.Metadata.WhenIdleAsync().IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromMilliseconds(250));
        }

        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    private static int IgdbSearches(ArcadiaDriver arcadia) =>
        arcadia.Igdb.Snapshot().Count(r => r.Address.Host == "api.igdb.com" && r.Body.StartsWith("search", StringComparison.Ordinal));

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    private static async Task<MetadataHitsViewModel> ShownMetadataHitsAsync(WaitingListViewModel list)
    {
        var hits = Assert.IsType<MetadataHitsViewModel>(list.Hits);
        await Task.Delay(WaitingHitsViewModel.RestDelay * 2);
        await hits.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        await hits.Images.WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
        return hits;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-16")]
    [Trait("TestCase", "T494-18")]
    public async Task A_game_with_several_igdb_hits_waits_marked_metadaten_beside_its_artwork_entry_and_keeps_its_launcher_data()
    {
        using var arcadia = await StartAsync();
        WaitingListTests.OpenDataSources(arcadia);

        var row = Assert.IsType<ActionRowViewModel>(Row(arcadia, ArtworkNameRows.WaitingKey));
        Assert.Equal(("3", AppTexts.DataSourcesWaitingSubtitle, true), (row.CountBadge, row.Subtitle, row.CanActivate));
        var facts = Assert.Single(row.Info.OfType<InfoFacts>()).Facts;
        Assert.Equal(
            [("Echo Drift", "Anderer Launcher · Artwork"), ("Echo Drift", "Anderer Launcher · Metadaten"), ("K?ln", "Anderer Launcher · Metadaten")],
            facts.Select(f => (f.Label, f.Value)));
        Assert.Equal("K?ln", arcadia.Main.Session.Titles.Shown(Koeln).Title);
        Assert.Null(arcadia.Metadata.Of(Koeln.Key).Description);

        arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.WaitingList!;
        Assert.Equal(
            [("Echo Drift", "Anderer Launcher · Artwork"), ("Echo Drift", "Anderer Launcher · Metadaten"), ("K?ln", "Anderer Launcher · Metadaten")],
            list.Games.Select(g => (g.Title, g.Subline)));
        Assert.IsType<ArtworkHitsViewModel>(list.Hits);

        arcadia.Press(UiAction.Down);
        var hits = await ShownMetadataHitsAsync(list);
        Assert.Equal((AppTexts.WaitingSoughtMetadata, AppTexts.WaitingIntroMetadata(2), AppTexts.WaitingNoteMetadata, "Echo Drift"), (hits.SoughtText, hits.Intro, hits.Note, hits.Term));
        Assert.Equal([("Echo Drift", "2015", true), ("Echo Drift", string.Empty, false)], hits.Hits.Select(h => (h.Name, h.YearText, h.HasYear)));
        Assert.NotNull(hits.Hits[0].Image);
        Assert.Null(hits.Hits[1].Image);
        Assert.Equal(2, IgdbSearches(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-19")]
    [Trait("TestCase", "T494-31")]
    public async Task A_on_a_metadata_hit_shows_its_fields_and_title_everywhere_at_once_and_y_skips_for_good()
    {
        using var arcadia = await StartAsync();
        WaitingListTests.OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.WaitingList!;

        // ▲ goes round to "K?ln", A into its hits, ▶ to the second, A takes it.
        arcadia.Press(UiAction.Up);
        await ShownMetadataHitsAsync(list);
        arcadia.Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Accept);
        await RunToIdleAsync(arcadia);

        Assert.Equal(["artwork|" + Echo.Key, "metadata|" + Echo.Key], list.Games.Select(g => g.Key));
        Assert.Equal("Köln", arcadia.Main.Session.Titles.Shown(Koeln).Title);
        Assert.Equal("Second Köln.", arcadia.Metadata.Of(Koeln.Key).Description?.Value);
        Assert.Equal(2, IgdbSearches(arcadia));

        // After the last entry the first is current; ▼ to the metadata entry of "Echo Drift", Y skips it, its artwork
        // entry stays; B closes the list.
        Assert.Equal("artwork|" + Echo.Key, list.CurrentGame!.Key);
        arcadia.Press(UiAction.Down);
        Assert.Equal("metadata|" + Echo.Key, list.CurrentGame!.Key);
        arcadia.Press(UiAction.Status);
        await RunToIdleAsync(arcadia);
        Assert.Equal(["artwork|" + Echo.Key], list.Games.Select(g => g.Key));
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);

        // A reload asks the chosen game by its IGDB id, the skipped one not at all.
        arcadia.Igdb.ServeId(22, FakeIgdb.Game(22, "Köln", extra: "\"summary\":\"Second Köln.\""));
        arcadia.Metadata.ReloadAll();
        await RunToIdleAsync(arcadia);
        Assert.Contains(arcadia.Igdb.Snapshot(), r => r.Body.Contains("where id = 22;", StringComparison.Ordinal));
        using var restarted = await arcadia.RestartAsync();
        await RunToIdleAsync(restarted);
        Assert.Empty(restarted.Metadata.Waiting);
        Assert.Equal(2, IgdbSearches(restarted));
        Assert.Equal("Köln", restarted.Main.Session.Titles.Shown(Koeln).Title);
        Assert.Null(restarted.Metadata.Of(Echo.Key).Description);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-17")]
    public async Task Ersten_nehmen_takes_the_first_hit_of_artwork_and_metadata_entries_with_one_toast()
    {
        using var arcadia = await StartAsync();
        WaitingListTests.OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkNameRows.SeveralHitsKey);
        // Collected as shown: the fake time running to idle can outlast the toast's four seconds, most on a slow runner.
        var toasts = new List<string>();
        arcadia.Main.Toast.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(arcadia.Main.Toast.Text) && arcadia.Main.Toast.Text is { } text)
            {
                toasts.Add(text);
            }
        };

        arcadia.Press(UiAction.Accept);
        await WaitingListTests.SettledAsync(arcadia);
        await RunToIdleAsync(arcadia);

        Assert.Equal([AppTexts.DataSourcesTookFirstHits(2)], toasts);
        Assert.Empty(arcadia.Artwork.Waiting);
        Assert.Empty(arcadia.Metadata.Waiting);
        Assert.Equal("First Köln.", arcadia.Metadata.Of(Koeln.Key).Description?.Value);
        Assert.Equal("Köln", arcadia.Main.Session.Titles.Shown(Koeln).Title);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(SeveralHits.Ask, arcadia.Settings.Current.Artwork.WithValidValues().SeveralHits);
        Assert.Empty(arcadia.Metadata.Waiting);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T494-18")]
    public async Task An_own_term_on_a_metadata_entry_searches_igdbs_main_games()
    {
        using var arcadia = await StartAsync();
        arcadia.Igdb.ServeName("Echo Drift Remix", FakeIgdb.Game(31, "Echo Drift Remix", 9), FakeIgdb.Game(32, "Echo Drift Remix Soundtrack", 1));
        WaitingListTests.OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.WaitingList!;
        arcadia.Press(UiAction.Down);
        var hits = await ShownMetadataHitsAsync(list);

        // A into the hits, ▲ to the field, A opens the keyboard, ≡ takes the term.
        arcadia.Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Accept);
        Assert.NotNull(hits.Editor);
        hits.Editor.Type(" Remix");
        arcadia.Press(UiAction.Settings);
        await RunToIdleAsync(arcadia);
        await hits.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Echo Drift Remix", hits.Term);
        Assert.Equal(["Echo Drift Remix"], hits.Hits.Select(h => h.Name));
        Assert.Equal(AppTexts.WaitingIntroMetadataOwnTerm(1), hits.Intro);
    }
}
