using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #531: the detail page's readability, as far as the view models and the title's sizing show it: the line for a
/// missing description (T531-6) and the long title that shrinks before it wraps (T531-3). The look itself (shadows,
/// raised buttons, chips, the tab strip's fade) is checked in the running app.
/// </summary>
public sealed class DetailReadabilityTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private const string NoDescription = "Es liegt keine Beschreibung zu dem Spiel vor.";

    private const string Description = "Baue eine Flotte aus Eisenschiffen und halte die Handelswege offen, denn der Winter ist lang.";

    [AvaloniaFact]
    [Trait("TestCase", "T531-6")]
    public async Task Without_a_description_Uebersicht_and_Infos_say_so_until_the_store_brings_one()
    {
        var game = new Game
        {
            Id = "4711",
            LauncherId = KnownLaunchers.Steam.Id,
            Title = "Eisenflut",
            Launch = new LaunchTarget.ProtocolUri("steam://run/4711"),
        };
        var source = new FakeMetadataSource();
        source.Local[game.Id] = new GameMetadata { Genres = [Genre.Action], Developers = ["Nordkai Studio"] };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Steam, [game]) with { Metadata = source }]);
        await RunToIdleAsync(arcadia);
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        // Genres and developer, but no description: the place says so instead of staying empty.
        var info = arcadia.Detail!.Current.GameInfo;
        Assert.False(info.IsEmpty);
        Assert.Equal((false, NoDescription), (info.HasDescription, info.MissingDescription));

        arcadia.MetadataStore.Serve("4711", Description);
        await arcadia.RescanAsync();
        await RunToIdleAsync(arcadia);

        info = arcadia.Detail!.Current.GameInfo;
        Assert.Equal((Description, null), (info.ShortDescription, info.MissingDescription));
    }

    [Fact]
    [Trait("TestCase", "T531-3")]
    public void A_title_that_fits_keeps_the_full_size()
    {
        Assert.Equal(66, ShrinkingTitle.FittingSize(size => size * 10, 1164, 66, 33));
    }

    [Fact]
    [Trait("TestCase", "T531-3")]
    public void A_longer_title_takes_the_largest_size_that_fits_one_line()
    {
        // 20 units wide per point of size: 1164 / 20 = 58.2, so 58 fits and 59 does not.
        Assert.Equal(58, ShrinkingTitle.FittingSize(size => size * 20, 1164, 66, 33));
    }

    [Fact]
    [Trait("TestCase", "T531-3")]
    public void A_width_that_grows_faster_than_the_size_is_checked_down_from_the_first_guess()
    {
        // Wider at small sizes than proportional (hinting, spacing): the first guess doesn't fit, a smaller size does.
        static double WidthAt(double size) => (size * 20) + (size < 60 ? 40 : 0);

        var size = ShrinkingTitle.FittingSize(WidthAt, 1164, 66, 33);

        Assert.True(WidthAt(size) <= 1164);
        Assert.True(WidthAt(size + 1) > 1164);
    }

    [Fact]
    [Trait("TestCase", "T531-3")]
    public void A_title_too_long_even_at_half_the_size_stays_at_half_the_size()
    {
        Assert.Equal(33, ShrinkingTitle.FittingSize(size => size * 60, 1164, 66, 33));
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
}
