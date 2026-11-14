using Arcadia.App.Filter;
using Arcadia.App.Input;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// The library's filter by metadata (#466) with values only IGDB brings: a launcher without metadata of its own, IGDB's
/// themes and keywords as the game's tags.
/// </summary>
public sealed class LibraryIgdbFilterTests
{
    private static readonly LauncherInfo Shop = new("shop", "Made-up Shop", "shop", null);

    private static Game GameOf(string id, string title) => new()
    {
        Id = id,
        LauncherId = Shop.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri("shop://run/" + id),
    };

    private static readonly Game Nebel = GameOf("nebel", "Nebelwald");
    private static readonly Game Ohne = GameOf("ohne", "Ohne Treffer");

    /// <summary>"Nebelwald" gets English tags from IGDB alone; "Ohne Treffer" has none.</summary>
    private static async Task<ArcadiaDriver> StartAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Shop, [Nebel, Ohne]) with { Metadata = new FakeMetadataSource { Store = null } }],
            settings: new AppSettings { ActiveLaunchers = [Shop.Id] });
        arcadia.Igdb.ServeName(
            "Nebelwald",
            FakeIgdb.Game(51, "Nebelwald", extra: "\"themes\":[{\"name\":\"Survival\"},{\"name\":\"Open world\"},{\"name\":\"Science fiction\"}]"));
        await arcadia.Metadata.Igdb!.SetClientIdAsync(FakeIgdb.ClientId);
        await arcadia.Metadata.Igdb.SetSecretAsync(FakeIgdb.Secret);
        arcadia.Metadata.FetchMissing();
        await LibraryMetadataFilterTests.RunToIdleAsync(arcadia);
        return arcadia;
    }

    private static FilterModalViewModel OpenTags(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.KeyboardLayout);
        var modal = arcadia.Main.FilterModal ?? throw new InvalidOperationException("Das Filter-Modal ist zu.");
        for (var i = 0; i < 20 && modal.Current.Label != "Tags"; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.Equal("Tags", modal.Current.Label);
        arcadia.Press(UiAction.Right);
        return modal;
    }

    private static List<string> Values(FilterModalViewModel modal) =>
        [.. modal.ListPane!.VisibleRows.SelectMany(row => row.Cells).Where(cell => !cell.IsAll).Select(cell => cell.Label)];

    [AvaloniaFact]
    [Trait("TestCase", "T466-6")]
    public async Task Igdb_tags_show_in_the_filter_exactly_as_on_the_detail_page()
    {
        using var arcadia = await StartAsync();
        var shown = arcadia.Metadata.Of(Nebel.Key).Tags?.Value;
        Assert.NotNull(shown);

        var modal = OpenTags(arcadia);

        // Untranslated, untrimmed, none added or left out: the detail page's list.
        Assert.Equal(shown.Order(StringComparer.Ordinal), Values(modal).Order(StringComparer.Ordinal));
        Assert.Contains("Science fiction", Values(modal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T466-21")]
    public async Task Values_only_igdb_brings_go_with_igdb_off_and_come_back_with_it_while_the_choice_stays_saved()
    {
        using var arcadia = await StartAsync();
        var modal = OpenTags(arcadia);
        for (var i = 0; i < 20 && modal.ListPane!.VisibleRows.SelectMany(row => row.Cells).SingleOrDefault(cell => cell.IsFocused)?.Label != "Survival"; i++)
        {
            arcadia.Press(i % 3 == 2 ? UiAction.Down : UiAction.Right);
        }

        arcadia.Press(UiAction.Accept).Press(UiAction.Back).Press(UiAction.Back);
        Assert.Equal(["Nebelwald"], arcadia.PageGames.Select(tile => tile.Title));

        await SwitchIgdbAsync(arcadia, on: false);

        // Without IGDB nothing has the tag: the library shows the tab unfiltered by it, the choice stays saved.
        Assert.Equal(["Nebelwald", "Ohne Treffer"], arcadia.PageGames.Select(tile => tile.Title).Order());
        Assert.Equal(["Survival"], arcadia.Settings.Current.LibraryFilters[Shop.Id].Tags!.Names);
        // Nothing else to filter by here, so the filter doesn't even open; either way "Tags" isn't offered.
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.NotEqual(true, arcadia.Main.FilterModal?.Categories.Any(row => row.Label == "Tags"));
        if (arcadia.Main.FilterModal is not null)
        {
            arcadia.Press(UiAction.Back);
        }

        await SwitchIgdbAsync(arcadia, on: true);

        Assert.Equal(["Nebelwald"], arcadia.PageGames.Select(tile => tile.Title));
    }

    private static async Task SwitchIgdbAsync(ArcadiaDriver arcadia, bool on)
    {
        arcadia.Settings.Update(s => s with { Metadata = s.Metadata with { Off = on ? [] : [MetadataSourceKind.Igdb] } });
        arcadia.Metadata.SettingsChanged();
        for (var i = 0; i < 2000 && (arcadia.Metadata.Of(Nebel.Key).Tags is null) == on; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
        }

        await LibraryMetadataFilterTests.RunToIdleAsync(arcadia);
        Dispatcher.UIThread.RunJobs();
    }
}
