using System.Diagnostics;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #493: a game's metadata on the detail page through the view models, with made-up launchers, their local data, a fake
/// store and fake time: the info line, "Übersicht" and "Infos", read by controller (T493-1, -2, -10, -11, -14, -20,
/// -28, -30, -41).
/// </summary>
public sealed class DetailMetadataTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private const string Description = "Baue eine Flotte aus Eisenschiffen und halte die Handelswege offen, denn der Winter ist lang und das Meer ist kalt.";

    private static Game GameOf(LauncherInfo launcher, string id, string title = "Eisenflut") => new()
    {
        Id = id,
        LauncherId = launcher.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri(launcher.Id + "://run/" + id),
    };

    /// <summary>What a Steam game's <c>appinfo.vdf</c> gives, without asking the store: genres, developer, year, tags.</summary>
    private static GameMetadata LocalOf() => new()
    {
        Genres = [Genre.Action, Genre.Adventure],
        Developers = ["Nordkai Studio"],
        Publishers = ["Seidenfaden Interactive"],
        ReleaseDate = new DateOnly(2021, 3, 14),
        Tags = ["Open World", "Atmosphärisch", "Erkundung", "Koop", "Handgezeichnet"],
    };

    private static LauncherSpec SteamWith(params Game[] games)
    {
        var source = new FakeMetadataSource();
        foreach (var game in games)
        {
            source.Local[game.Id] = LocalOf();
        }

        return LauncherSpec.With(KnownLaunchers.Steam, games) with { Metadata = source };
    }

    /// <summary>Lets fake time pass until the metadata run is idle, the UI thread following.</summary>
    private static async Task RunToIdleAsync(ArcadiaDriver arcadia)
    {
        for (var i = 0; i < 3000 && !arcadia.Metadata.WhenIdleAsync().IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
            arcadia.Time.Advance(TimeSpan.FromSeconds(1));
        }

        await arcadia.Metadata.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();
    }

    private static GameDetailPageViewModel OpenDetail(ArcadiaDriver arcadia)
    {
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        return arcadia.Detail!.Current;
    }

    /// <summary>From the buttons down the rail to "Infos", the way the controller goes.</summary>
    private static void GoToInfos(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Down);
        while (!arcadia.Detail!.Current.OpenArea.IsInfos)
        {
            arcadia.Press(UiAction.Down);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-1")]
    [Trait("TestCase", "T493-2")]
    public async Task The_local_data_shows_at_once_and_the_stores_description_and_rating_follow_while_the_page_is_open()
    {
        // The store has no answer at first: the info line comes from the local file alone.
        using var arcadia = await ArcadiaDriver.StartAsync([SteamWith(GameOf(KnownLaunchers.Steam, "4711"))]);
        await RunToIdleAsync(arcadia);
        var page = OpenDetail(arcadia);

        Assert.Equal("Action, Abenteuer · Nordkai Studio · 2021", page.GameInfo.InfoLine);
        Assert.Null(page.GameInfo.Badge);
        Assert.Null(page.GameInfo.ShortDescription);
        Assert.Equal(["Open World", "Atmosphärisch", "Erkundung"], page.GameInfo.OverviewTags.Select(tag => tag.Name));
        Assert.Equal("+2", page.GameInfo.MoreTags);
        Assert.Equal("Action · 2021", page.Area(DetailAreaViewModel.InfosKey).ValueLine);

        // The store answers at the next scan; the open page shows it without being opened again.
        arcadia.MetadataStore.Serve("4711", Description);
        await arcadia.RescanAsync();
        await RunToIdleAsync(arcadia);

        Assert.True(arcadia.Main.IsDetailOpen);
        page = arcadia.Detail!.Current;
        Assert.Equal(Description, page.GameInfo.ShortDescription);
        Assert.Equal(Description, page.GameInfo.Description);
        Assert.Equal("USK 12", page.GameInfo.Badge);
        Assert.Equal("Action · 2021 · USK 12", page.Area(DetailAreaViewModel.InfosKey).ValueLine);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-2")]
    public async Task Metadata_arriving_for_the_shown_game_keeps_Infos_open_and_the_focus_in_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([SteamWith(GameOf(KnownLaunchers.Steam, "4711"))]);
        await RunToIdleAsync(arcadia);
        OpenDetail(arcadia);
        GoToInfos(arcadia);

        arcadia.MetadataStore.Serve("4711", Description);
        await arcadia.RescanAsync();
        await RunToIdleAsync(arcadia);

        var page = arcadia.Detail!.Current;
        Assert.Equal(Description, page.GameInfo.Description);
        Assert.True(page.OpenArea.IsInfos);
        Assert.Equal(DetailZone.Rail, page.Zone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-10")]
    public async Task An_epic_game_shows_only_its_developer_with_its_source_and_no_placeholder()
    {
        var game = GameOf(KnownLaunchers.Epic, "fennel");
        var source = new FakeMetadataSource { Store = null };
        source.Local[game.Id] = new GameMetadata { Developers = ["Kornfeld Interactive"] };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Epic, game) with { Metadata = source }]);
        await RunToIdleAsync(arcadia);
        var toasts = new List<string?>();
        arcadia.Main.Toast.PropertyChanged += (_, _) => toasts.Add(arcadia.Main.Toast.Text);
        var page = OpenDetail(arcadia);

        Assert.Equal("Kornfeld Interactive", page.GameInfo.InfoLine);
        Assert.False(page.GameInfo.HasBadge || page.GameInfo.HasShortDescription || page.GameInfo.HasDescription || page.GameInfo.HasTags);
        var fact = Assert.Single(page.GameInfo.Facts);
        Assert.Equal((AppTexts.DetailDevelopersCaption, "Kornfeld Interactive", "Epic Games"), (fact.Caption, fact.Value, fact.Source));
        Assert.Equal("Kornfeld Interactive", page.Area(DetailAreaViewModel.InfosKey).ValueLine);
        Assert.Empty(toasts);
        Assert.Equal(0, arcadia.MetadataStore.RequestCount);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-11")]
    public async Task A_game_without_metadata_has_no_info_line_description_or_tags_and_Infos_says_there_is_nothing_yet()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, GameOf(KnownLaunchers.Gog, "bravo"))]);
        var page = OpenDetail(arcadia);

        Assert.True(page.GameInfo.IsEmpty);
        Assert.False(page.GameInfo.HasInfoLine || page.GameInfo.HasShortDescription || page.GameInfo.HasTags || page.GameInfo.HasFacts);
        Assert.Equal(AppTexts.DetailInfosNone, page.Area(DetailAreaViewModel.InfosKey).ValueLine);
        Assert.Equal(GameActionKind.Play, page.FocusedKind);

        // "Infos" shows that there is nothing yet; A has nothing to lead into.
        GoToInfos(arcadia);
        Assert.Null(page.AcceptHint);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rail, page.Zone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-14")]
    [Trait("TestCase", "T493-28")]
    [Trait("TestCase", "T541-1")]
    public async Task Infos_lists_every_field_with_its_source_and_the_controller_alone_scrolls_it_and_goes_back()
    {
        var game = GameOf(KnownLaunchers.Steam, "4711");
        var source = new FakeMetadataSource { Store = null };
        source.Local[game.Id] = LocalOf() with
        {
            Description = Description,
            AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "16", ByStore: true), new AgeRating(AgeRatingBoard.Pegi, "16")],
        };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Steam, game) with { Metadata = source }]);
        await RunToIdleAsync(arcadia);
        var page = OpenDetail(arcadia);

        Assert.Equal(
            [
                (AppTexts.DetailGenresCaption, "Action, Abenteuer", "Steam", false),
                (AppTexts.DetailDevelopersCaption, "Nordkai Studio", "Steam", false),
                (AppTexts.DetailPublishersCaption, "Seidenfaden Interactive", "Steam", false),
                (AppTexts.DetailReleasedCaption, "14.03.2021", "Steam", false),
                (AppTexts.DetailAgeRatingCaption, "USK 16", "Steam", true),
                (string.Empty, "PEGI 16", "Steam", true),
            ],
            page.GameInfo.Facts.Select(fact => (fact.Caption, fact.Value, fact.Source, fact.IsBadge)));
        Assert.Equal((Description, "Steam"), (page.GameInfo.Description, page.GameInfo.DescriptionSource));
        Assert.Equal((5, "Steam"), (page.GameInfo.Tags.Count, page.GameInfo.TagsSource));

        // Rail → "Infos" → RS scrolls the text without A (#530) → B closes the page. The view says the text is longer
        // than it shows; A does nothing there.
        var scrolls = new List<int>();
        arcadia.Detail!.InfoScrollRequested += scrolls.Add;
        GoToInfos(arcadia);
        Assert.Null(page.AcceptHint);
        Assert.True(page.IsReadingInfos);
        arcadia.Detail.InfoOverflows = true;
        arcadia.Press(UiAction.SecondaryDown).Press(UiAction.SecondaryDown).Press(UiAction.SecondaryUp);
        Assert.Equal([1, 1, -1], scrolls);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.True(page.OpenArea.IsInfos);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-41")]
    public async Task Infos_offers_nothing_in_Uebersicht_anpassen_and_none_of_its_content_is_a_quick_entry()
    {
        var game = GameOf(KnownLaunchers.Steam, "4711");
        var source = new FakeMetadataSource { Store = null };
        source.Local[game.Id] = LocalOf() with { Description = Description };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Steam, game) with { Metadata = source }]);
        await RunToIdleAsync(arcadia);
        var page = OpenDetail(arcadia);

        var infos = page.Area(DetailAreaViewModel.InfosKey);
        Assert.Empty(infos.Rows);
        Assert.DoesNotContain(page.QuickChoices, choice => choice.AreaName == infos.Name);
        Assert.All(page.Overview.Rows, row => Assert.Contains(page.Areas.Where(area => !area.IsOverview && !area.IsInfos).SelectMany(area => area.Rows), other => other.QuickId == row.QuickId));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-20")]
    public async Task Offline_after_a_fetch_the_next_start_shows_everything_loaded_before_without_a_message()
    {
        var game = GameOf(KnownLaunchers.Steam, "4711");
        using var arcadia = await ArcadiaDriver.StartAsync([SteamWith(game)], stores: store => store.Serve("4711", Description));
        await RunToIdleAsync(arcadia);
        Assert.Equal(Description, arcadia.Main.MetadataOf(game.Key).Description?.Value);
        arcadia.MetadataStore.Offline = true;

        using var restarted = await arcadia.RestartAsync();
        var toasts = new List<string?>();
        restarted.Main.Toast.PropertyChanged += (_, _) => toasts.Add(restarted.Main.Toast.Text);
        await RunToIdleAsync(restarted);
        var page = OpenDetail(restarted);

        Assert.Equal((Description, "USK 12"), (page.GameInfo.Description, page.GameInfo.Badge));
        Assert.Equal("Action, Abenteuer · Nordkai Studio · 2021", page.GameInfo.InfoLine);
        Assert.Empty(toasts);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T493-30")]
    public async Task While_1000_slow_store_answers_run_the_library_and_the_settings_stay_quick()
    {
        // Each answer takes 2 s of real time: the fetch runs the whole test long.
        var games = Enumerable.Range(1, 1000).Select(i => GameOf(KnownLaunchers.Steam, (4_000_000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), $"Spiel {i:0000}")).ToArray();
        var started = Stopwatch.StartNew();
        using var arcadia = await ArcadiaDriver.StartAsync([SteamWith(games)], stores: store => store.ServeEverySlowly(Description, TimeSpan.FromSeconds(2)));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10), $"Start dauerte {started.Elapsed.TotalSeconds:0.0} s");
        Assert.Equal(1000, arcadia.Main.Library.ShownGames(arcadia.Main.LauncherTabs.SelectedTab!).Count);
        await arcadia.WaitUntilAsync(() => arcadia.MetadataStore.RequestCount > 0, "erste Anfrage läuft");

        var slowest = TimeSpan.Zero;
        for (var i = 0; i < 20; i++)
        {
            var press = Stopwatch.StartNew();
            arcadia.Press(i % 2 == 0 ? UiAction.Right : UiAction.Down);
            slowest = press.Elapsed > slowest ? press.Elapsed : slowest;
        }

        Assert.True(slowest < TimeSpan.FromMilliseconds(50), $"Fokuswechsel dauerte {slowest.TotalMilliseconds:0} ms");

        // The settings page and the progress row answer as well while the fetch goes on.
        var open = Stopwatch.StartNew();
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not Settings.DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        arcadia.FocusRow(Settings.MetadataRows.MissingKey);
        Assert.True(open.Elapsed < TimeSpan.FromMilliseconds(500), $"Einstellungen brauchten {open.Elapsed.TotalMilliseconds:0} ms");
        Assert.False(arcadia.Metadata.WhenIdleAsync().IsCompleted);
    }
}
