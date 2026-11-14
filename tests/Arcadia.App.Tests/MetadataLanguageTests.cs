using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #557: the detail page's metadata in the UI language and the region's age rating first, through the view models with a
/// made-up Steam library, a fake store and fake time (T557-3, T557-4, T557-6).
/// </summary>
public sealed class MetadataLanguageTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private const string German = "Baue eine Flotte aus Eisenschiffen und halte die Handelswege offen, denn der Winter ist lang.";
    private const string English = "Build a fleet of iron ships and keep the trade routes open, for the winter is long and the sea is cold.";
    private const string French = "Construisez une flotte de navires et gardez les routes ouvertes, car l'hiver est long dans cette mer très froide.";

    private static readonly Game Eisenflut = new()
    {
        Id = "4711",
        LauncherId = KnownLaunchers.Steam.Id,
        Title = "Eisenflut",
        Launch = new LaunchTarget.ProtocolUri("steam://run/4711"),
    };

    private static LauncherSpec Steam(GameMetadata? local = null)
    {
        var source = new FakeMetadataSource();
        source.Local[Eisenflut.Id] = local ?? new GameMetadata { Genres = [Genre.Action], Developers = ["Nordkai Studio"] };
        return LauncherSpec.With(KnownLaunchers.Steam, [Eisenflut]) with { Metadata = source };
    }

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

    private static IReadOnlyList<string> AskedLanguages(ArcadiaDriver arcadia) =>
        [.. arcadia.MetadataStore.Requests.Select(r => r.Request.RequestUri!.Query)];

    [AvaloniaFact]
    [Trait("TestCase", "T557-4")]
    public async Task After_switching_to_English_the_German_description_stays_until_all_metadata_is_reloaded_in_English()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Steam()], stores: store => store.Serve(Eisenflut.Id, German));
        await RunToIdleAsync(arcadia);
        Assert.Equal(["?l=de"], AskedLanguages(arcadia));
        Assert.Equal(German, OpenDetail(arcadia).GameInfo.Description);
        arcadia.Press(UiAction.Back);

        // "Sprache": English, then the restart "Sprache anwenden" does.
        arcadia.Settings.Update(settings => settings with { Localization = settings.Localization with { Language = "en" } });
        using var english = await arcadia.RestartAsync();
        await RunToIdleAsync(english);
        Assert.Equal(German, OpenDetail(english).GameInfo.Description);
        Assert.Single(english.MetadataStore.Requests);
        english.Press(UiAction.Back);

        english.MetadataStore.Serve(Eisenflut.Id, English);
        Assert.True(english.Metadata.ReloadAll());
        await RunToIdleAsync(english);
        Assert.Equal(["?l=de", "?l=en"], AskedLanguages(english));
        Assert.Equal(English, OpenDetail(english).GameInfo.Description);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T557-3")]
    public async Task A_description_in_neither_the_ui_language_nor_English_is_left_out()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Steam()], stores: store => store.Serve(Eisenflut.Id, French));
        await RunToIdleAsync(arcadia);
        var page = OpenDetail(arcadia);

        Assert.Null(page.GameInfo.Description);
        Assert.Null(page.GameInfo.ShortDescription);
        Assert.Equal("Action · Nordkai Studio", page.GameInfo.InfoLine);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T557-6")]
    [InlineData(AgeRatingBoard.Usk, "USK 16")]
    [InlineData(AgeRatingBoard.Pegi, "PEGI 18")]
    [InlineData(AgeRatingBoard.Esrb, "ESRB M")]
    public async Task The_badge_shows_the_regions_age_rating_and_Infos_lists_it_first(AgeRatingBoard region, string badge)
    {
        var local = new GameMetadata
        {
            AgeRatings = [new AgeRating(AgeRatingBoard.Esrb, "M"), new AgeRating(AgeRatingBoard.Pegi, "18"), new AgeRating(AgeRatingBoard.Usk, "16")],
        };
        using var arcadia = await ArcadiaDriver.StartAsync([Steam(local)], firstAgeRatingBoard: region);
        await RunToIdleAsync(arcadia);
        var page = OpenDetail(arcadia);

        Assert.Equal(badge, page.GameInfo.Badge);
        Assert.Equal(badge, page.GameInfo.Facts.First(fact => fact.IsBadge).Value);
        Assert.Equal(3, page.GameInfo.Facts.Count(fact => fact.IsBadge));
    }
}
