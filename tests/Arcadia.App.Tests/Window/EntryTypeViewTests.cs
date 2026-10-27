using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>Software, tools and soundtracks as drawn (#141): the type icon on cover, search hit, preview and detail page, the count line.</summary>
public sealed class EntryTypeViewTests
{
    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private const string LongTitle = "Source SDK Base 2013 Multiplayer Dedicated Server Toolkit Edition";

    private static readonly GameType[] OtherTypes = [GameType.Software, GameType.Tool, GameType.Soundtrack];

    /// <summary>
    /// A game, and per type an entry in every state a cover shows a badge for: not installed, update, running, being
    /// installed, plus one that is about to start (A on it in the test) for software and tools; a soundtrack never
    /// starts in Arcadia, Steam plays it. And a tool with a title too long for three lines.
    /// </summary>
    private static LauncherSpec EveryState() => new(
        KnownLaunchers.Gog,
        0,
        Library:
        [
            Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
            .. OtherTypes.SelectMany(type => new[]
            {
                NotInstalled(KnownLaunchers.Gog, $"{type} fehlt") with { Type = type },
                Installed(KnownLaunchers.Gog, $"{type} Update", Installed) with { Type = type, UpdateAvailable = true },
                Installed(KnownLaunchers.Gog, $"{type} läuft", Installed) with { Type = type },
                NotInstalled(KnownLaunchers.Gog, $"{type} lädt") with { Type = type },
                Installed(KnownLaunchers.Gog, $"{type} startet", Installed) with { Type = type },
            }),
            Installed(KnownLaunchers.Gog, LongTitle, Installed) with { Type = GameType.Tool },
        ],
        Running: [.. OtherTypes.Select(type => $"{type} läuft")]);

    [AvaloniaFact]
    [Trait("TestCase", "T141-4")]
    public async Task On_a_cover_the_type_icon_stands_above_the_title_clear_of_every_state_badge_and_the_progress_bar_and_games_have_none()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([EveryState()], ArcadiaWindow.WithPageSize(25, [EveryState()]));
        var driver = arcadia.Arcadia;
        foreach (var type in OtherTypes)
        {
            driver.Installs.Set(driver.PageGames.Single(tile => tile.Title == $"{type} lädt").Game, new InstallProgress(InstallPhase.Downloading, 0.42));
        }

        await driver.FollowInstallsAsync();
        var starts = 0;
        foreach (var type in new[] { GameType.Software, GameType.Tool })
        {
            driver.FocusTile($"{type} startet");
            driver.Press(UiAction.Accept);
            Assert.True(driver.FocusedGame!.IsStarting, $"{type} startet");

            // The launcher hears of it on the thread pool; the next start waits for that, as a user's second A would.
            starts++;
            await driver.WaitUntilAsync(() => driver.Starts.Keys.Count == starts, $"{type} soll gestartet werden");
        }

        Assert.All(OtherTypes.Take(2), type => Assert.True(driver.PageGames.Single(tile => tile.Title == $"{type} startet").IsStarting));

        arcadia.Settle();
        var game = Cover(arcadia, "Hollow Knight");
        Assert.Null(game.Badge);

        foreach (var tile in driver.PageGames.Where(tile => tile.HasTypeIcon))
        {
            var cover = Cover(arcadia, tile.Title);
            var badge = cover.Badge ?? throw new InvalidOperationException($"{tile.Title}: kein Typsymbol");
            Assert.True(badge.Bottom <= cover.Title.Top + 0.5, $"{tile.Title}: das Symbol steht über dem Titel");
            Assert.Equal(cover.Title.Left, badge.Left, tolerance: 0.5);
            Assert.Equal(26 * arcadia.ScaleOf(cover.TitleText), badge.Width, tolerance: 0.5);
            Assert.Equal(badge.Width, badge.Height, tolerance: 0.5);
            Assert.All(cover.States, state => Assert.False(state.Intersects(badge), $"{tile.Title}: das Symbol berührt ein Zustandsabzeichen"));
            Assert.All(cover.States, state => Assert.False(state.Intersects(cover.Title), $"{tile.Title}: ein Abzeichen verdeckt den Titel"));
        }

        // Each state was really drawn on a cover of each type.
        foreach (var type in OtherTypes)
        {
            Assert.All(new[] { "fehlt", "Update", "läuft", "lädt" }, state => Assert.NotEmpty(Cover(arcadia, $"{type} {state}").States));
        }

        Assert.NotEmpty(Cover(arcadia, "Software startet").States);
        Assert.NotEmpty(Cover(arcadia, "Tool startet").States);

        // Three lines, the rest cut off with an ellipsis, the icon above it.
        var longTitle = Cover(arcadia, LongTitle);
        Assert.Equal(3, longTitle.TitleText.TextLayout.TextLines.Count);
        Assert.True(longTitle.TitleText.TextLayout.TextLines[^1].HasCollapsed, "Der Titel endet mit „…“");
        Assert.True(longTitle.Badge!.Value.Bottom <= longTitle.Title.Top + 0.5);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-4")]
    public async Task The_focus_ring_stays_whole_around_first_edge_and_last_cover_with_a_type_icon_and_a_state_badge()
    {
        // A full page of tools, each with an update: the type icon and a state badge on every cover.
        var tools = Enumerable.Range(1, 25)
            .Select(i => Installed(KnownLaunchers.Gog, $"Werkzeug {i:D2}", Installed) with { Type = GameType.Tool, UpdateAvailable = true })
            .ToArray();
        IReadOnlyList<LauncherSpec> launchers = [LauncherSpec.With(KnownLaunchers.Gog, tools)];
        using var arcadia = await ArcadiaWindow.ShowAsync(launchers, ArcadiaWindow.WithPageSize(25, launchers));
        var driver = arcadia.Arcadia;

        foreach (var (index, where) in new[] { (0, "erstes Cover"), (7, "letzte Spalte"), (24, "letztes Cover der Seite") })
        {
            var tile = driver.FocusTile(driver.PageGames[index].Title);

            // The driver's presses don't tell the window its frame is stale: Settle does, so the scroll and the lift run.
            arcadia.Settle().WaitForTransitions();
            var cover = Cover(arcadia, tile.Title);
            Assert.NotNull(cover.Badge);
            Assert.NotEmpty(cover.States);
            arcadia.AssertRingAround(arcadia.Tile(tile), where, "FocusRingLifted");
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-4")]
    public async Task Preview_and_detail_page_name_the_type_with_its_icon_for_software_tools_and_soundtracks_and_never_for_games()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([EveryState()]);
        var driver = arcadia.Arcadia;

        foreach (var (title, name) in new[] { ("Hollow Knight", (string?)null), ("Software Update", "Software"), ("Tool Update", "Werkzeug"), ("Soundtrack Update", "Soundtrack") })
        {
            driver.FocusTile(title);
            arcadia.Settle();
            var preview = PreviewTypeLine(arcadia);
            Assert.Equal(name, preview?.Text);
            if (preview is not null)
            {
                Assert.True(arcadia.Bounds(preview).Top > arcadia.Bounds(arcadia.WithText(title).OfType<TextBlock>().Single(text => text.FontSize == 26)).Bottom - 1, $"{title}: die Zeile steht unter dem Titel");
            }

            arcadia.Arcadia.Hold(UiAction.Accept, PastHoldTime);
            arcadia.WaitForTransitions();
            var chip = DetailChip(arcadia.Showing(driver.Detail!.Current), name?.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
            Assert.Equal(name is not null, chip is not null);
            arcadia.Press(UiAction.Back).WaitForTransitions();
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-4")]
    public async Task A_search_hit_shows_the_launcher_logo_top_left_the_type_icon_above_the_title_and_a_state_badge_top_right_at_once()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([EveryState()]);

        arcadia.Key(Key.F).Type("Tool fehlt");
        arcadia.Settle();
        var search = arcadia.Arcadia.Search!;
        var hit = search.Results.First(tile => tile.Title == "Tool fehlt");
        var button = Assert.Single(
            arcadia.Named<Control>("SearchResults").GetVisualDescendants().OfType<Button>(),
            b => b.Classes.Contains("resultTile") && b.DataContext == hit && b.IsEffectivelyVisible);

        var tile = arcadia.Bounds(button);
        var logo = arcadia.Bounds(Visible<Border>(button, "launcherBadge").Single());
        var badge = arcadia.Bounds(Visible<Border>(button, "typeBadge").Single());
        var state = arcadia.Bounds(Visible<Border>(button, "installBadge").Single());
        var title = arcadia.Bounds(button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == hit.Title));

        Assert.True(logo.Left - tile.Left < 30 && logo.Top - tile.Top < 30, "Launcher-Logo oben links");
        Assert.True(tile.Right - state.Right < 30 && state.Top - tile.Top < 30, "Zustand oben rechts");
        Assert.True(badge.Bottom <= title.Top + 0.5 && badge.Top > logo.Bottom, "Typsymbol über dem Titel, unter dem Logo");
        Assert.False(badge.Intersects(state) || badge.Intersects(logo) || state.Intersects(logo), "Nichts überdeckt sich");
        Assert.DoesNotContain(search.Results, result => !result.Title.Contains(" fehlt", StringComparison.Ordinal) && result.HasTypeIcon && result.Game.Type == GameType.Game);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-7")]
    public async Task The_count_line_beside_a_long_tab_name_splits_its_parts_by_dividers_has_a_tooltip_and_stays_clear_of_the_view_button()
    {
        // All four types with four-digit counts beside "Ubisoft Connect".
        Game Entry(int i, GameType type) => (i % 2 == 0 ? Installed(KnownLaunchers.Ubisoft, $"{type} {i:D4}", Installed) : NotInstalled(KnownLaunchers.Ubisoft, $"{type} {i:D4}")) with { Type = type };
        var library = new[] { GameType.Soundtrack, GameType.Tool, GameType.Software, GameType.Game }
            .SelectMany(type => Enumerable.Range(1, 1234).Select(i => Entry(i, type)))
            .ToArray();
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Ubisoft, library)]);

        var line = arcadia.Window.GetVisualDescendants().OfType<Views.CountLineView>().Single(view => view.IsEffectivelyVisible);
        Assert.Equal(arcadia.Arcadia.Main.Library.Counts.Tip, ToolTip.GetTip(line));
        Assert.Equal("1.234 Spiele, davon 617 installiert · 1.234 Software, davon 617 installiert · 1.234 Werkzeuge, davon 617 installiert · 1.234 Soundtracks, davon 617 installiert", ToolTip.GetTip(line));

        // Spiele, Software, Werkzeuge, Soundtracks, left to right, each after a divider but the first.
        var icons = line.GetVisualDescendants().OfType<Controls.LineIcon>().Where(icon => icon.IsEffectivelyVisible && icon.Classes.Contains("countType"))
            .OrderBy(icon => arcadia.Bounds(icon).Left).Select(icon => icon.Icon).ToList();
        Assert.Equal(["IconTypeGame", "IconTypeSoftware", "IconTypeTool", "IconTypeSoundtrack"], icons);
        var dividers = line.GetVisualDescendants().OfType<Border>().Where(border => border.IsEffectivelyVisible && border.Classes.Contains("countDivider")).ToList();
        Assert.Equal(3, dividers.Count);
        Assert.All(dividers, divider => Assert.Equal((1, 24), (divider.Bounds.Width, divider.Bounds.Height)));
        Assert.Equal(4, line.GetVisualDescendants().OfType<TextBlock>().Count(text => text.IsEffectivelyVisible && text.Text == "1.234"));

        // No page number beside the name; the line ends before the view button.
        var name = Assert.Single(arcadia.WithText("Ubisoft Connect").OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.FontSize == 34);
        var beside = (Panel)name.GetVisualParent()!;
        var header = (Grid)beside.GetVisualParent()!;
        Assert.DoesNotContain(beside.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text?.Contains("Seite", StringComparison.Ordinal) == true);
        var view = header.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible);
        Assert.True(arcadia.Bounds(line).Left > arcadia.Bounds(name).Right, "Die Zeile steht rechts vom Namen");
        Assert.True(arcadia.Bounds(view).Left - arcadia.Bounds(line).Right > 24, "Die Zeile hält Abstand zur Ansicht");
    }

    /// <summary>What a cover shows: its type icon (null on a game), its title and the state badges and progress bar drawn on it.</summary>
    private sealed record CoverParts(Rect? Badge, Rect Title, TextBlock TitleText, IReadOnlyList<Rect> States);

    private static CoverParts Cover(ArcadiaWindow arcadia, string title)
    {
        var tile = arcadia.Arcadia.PageGames.Single(game => game.Title == title);
        var cover = arcadia.Tile(tile);
        var text = cover.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == title);
        var badge = Visible<Border>(cover, "typeBadge").Select(arcadia.Bounds).Cast<Rect?>().SingleOrDefault();
        var states = Visible<Border>(cover, "installBadge").Select(arcadia.Bounds)
            .Concat(Visible<ProgressBar>(cover, "install").Select(arcadia.Bounds))
            .ToList();
        return new CoverParts(badge, arcadia.Bounds(text), text, states);
    }

    private static IEnumerable<T> Visible<T>(Control within, string className)
        where T : Control => within.GetVisualDescendants().OfType<T>().Where(control => control.Classes.Contains(className) && control.IsEffectivelyVisible);

    /// <summary>The preview's line with the type's name, or null when it shows none.</summary>
    private static TextBlock? PreviewTypeLine(ArcadiaWindow arcadia) => arcadia.Window.GetVisualDescendants().OfType<TextBlock>()
        .SingleOrDefault(text => text.IsEffectivelyVisible && text.Text is "Software" or "Werkzeug" or "Soundtrack" or "Spiel"
            && text.GetVisualAncestors().OfType<ItemsControl>().All(items => items.Name != "GameGrid"));

    /// <summary>The detail page's type chip, or null.</summary>
    private static Border? DetailChip(Control page, string? name) => page.GetVisualDescendants().OfType<Border>()
        .SingleOrDefault(border => border.IsEffectivelyVisible && border.Classes.Contains("detailChip") && border.GetVisualDescendants().OfType<Controls.LineIcon>().Any()
            && (name is null || border.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == name)));
}
