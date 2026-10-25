using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.Launchers;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>A launcher's own page in the settings: active, name, keyword, reading again (#137).</summary>
public sealed class LauncherPageTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    [AvaloniaFact]
    [Trait("TestCase", "T137-2")]
    public async Task LT_and_RT_turn_to_the_launcher_before_and_after_in_the_order_of_the_list_and_wrap_around()
    {
        // The list shows the active ones in tab order, then the inactive one: Amazon, GOG, Xbox.
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["amazon", "gog"] });
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Accept);
        var head = Assert.IsType<LauncherPageHeadViewModel>(settings.Head);
        Assert.Equal(("Amazon Games", "Xbox", "GOG Galaxy"), (head.Name, head.Previous?.Name, head.Next?.Name));

        arcadia.Press(UiAction.NextPage);
        Assert.Equal(("gog", "GOG Galaxy"), (settings.PageLauncherId, head.Name));
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("xbox", settings.PageLauncherId);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("amazon", settings.PageLauncherId);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("xbox", settings.PageLauncherId);

        // LB/RB still change the category, also on a launcher's page.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);
        arcadia.Press(UiAction.PreviousTab);
        Assert.Same(settings, arcadia.Main.Settings.Selected);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-2")]
    public async Task A_single_launcher_has_no_neighbours_to_turn_to()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([new(KnownLaunchers.Gog, 31)]);
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Accept);
        var head = Assert.IsType<LauncherPageHeadViewModel>(settings.Head);
        Assert.False(head.HasNeighbours);
        Assert.Null(head.Next);

        arcadia.Press(UiAction.NextPage).Press(UiAction.PreviousPage);
        Assert.Equal("gog", settings.PageLauncherId);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-3")]
    public async Task Active_on_the_page_switches_like_the_list_and_the_head_shows_the_state()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        var head = Assert.IsType<LauncherPageHeadViewModel>(settings.Head);
        Assert.Equal(("launcher-active", "Aktiv · Tab 2"), (settings.FocusedRow?.Key, head.StateText));

        arcadia.Press(UiAction.Left);
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.TabNames());
        Assert.Equal("Nicht aktiv", head.StateText);

        // Switched on, it goes to the end and is read again.
        arcadia.Press(UiAction.Right);
        Assert.Equal("Aktiv · Tab 3", head.StateText);
        await arcadia.WaitUntilAsync(() => arcadia.TabNames().SequenceEqual(["GOG Galaxy", "Amazon Games", "Xbox"]), "Xbox soll den letzten Tab bekommen");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-3")]
    public async Task A_launcher_not_found_cannot_be_switched_on_on_its_page()
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Amazon, 6, IsInstalled: false)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, new AppSettings { ActiveLaunchers = ["gog"] });
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        var head = Assert.IsType<LauncherPageHeadViewModel>(settings.Head);
        Assert.Equal(("Amazon Games", "Nicht installiert"), (head.Name, head.StateText));

        foreach (var action in new[] { UiAction.Right, UiAction.Left, UiAction.Accept })
        {
            arcadia.Press(action);
            Assert.Equal(["GOG Galaxy"], arcadia.TabNames());
            Assert.Equal("Nicht installiert", head.StateText);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-5")]
    public async Task A_name_taken_shows_at_once_in_tab_list_search_preview_and_details_also_after_a_restart()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var settings = arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);

        arcadia.FocusRow("launcher-name");
        arcadia.Press(UiAction.Accept).Press(UiAction.Search, times: "GOG Galaxy".Length).Type("Galaxis").Press(UiAction.Settings);

        Assert.False(settings.IsEditing);
        Assert.Equal("Galaxis", ((LauncherPageHeadViewModel)settings.Head!).Name);
        Assert.Equal(["Galaxis", "Xbox", "Amazon Games"], arcadia.TabNames());
        arcadia.Press(UiAction.Back);
        Assert.Equal("Galaxis", settings.Rows.OfType<LauncherRowViewModel>().First().Title);

        arcadia.Press(UiAction.Back);
        Assert.Equal("Galaxis", arcadia.FocusedGame?.LauncherName);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(("Galaxis", "Galaxis"), (arcadia.Detail?.TabName, arcadia.Detail?.Current.Tile.LauncherName));
        arcadia.Press(UiAction.Back);

        arcadia.Press(UiAction.Search);
        Assert.Contains("Galaxis", arcadia.Search!.Chips.Select(chip => chip.Name));

        using var restarted = await arcadia.RestartAsync();
        Assert.Equal(["Galaxis", "Xbox", "Amazon Games"], restarted.TabNames());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-5")]
    public async Task A_name_taken_empty_is_the_launchers_own_name_again()
    {
        var renamed = new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"] }.WithLauncherPreferences("gog", p => p with { Name = "Galaxis" });
        using var arcadia = await ArcadiaDriver.StartAsync(settings: renamed);
        Assert.Equal("Galaxis", arcadia.TabNames()[0]);
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);

        arcadia.FocusRow("launcher-name");
        arcadia.Press(UiAction.Accept).Press(UiAction.Search, times: "Galaxis".Length).Press(UiAction.Settings);

        Assert.Equal("GOG Galaxy", arcadia.TabNames()[0]);
        Assert.Null(arcadia.Settings.Current.PreferencesOf("gog").Name);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-6")]
    public async Task A_keyword_taken_by_another_launcher_or_against_the_rules_is_red_at_once_and_not_taken()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        var row = Assert.IsType<TextRowViewModel>(arcadia.FocusRow("launcher-keyword"));
        Assert.Equal("/", row.Prefix);

        // Taken by Xbox, with "-", with a capital, with a special character, one too long.
        foreach (var keyword in new[] { "xbox", "go-g", "Gog", "gog!", "abcdefghijklm" })
        {
            arcadia.Press(UiAction.Accept).Press(UiAction.Search, times: "gog".Length).Type(keyword);
            Assert.True(row.Problem is not null, $"„{keyword}“ zeigt keinen Grund.");

            arcadia.Press(UiAction.Settings);
            Assert.False(row.IsEditing);
            Assert.Equal("gog", row.ValueText);
            Assert.Equal("gog", arcadia.TabsLauncher("gog").SearchKeyword);
            Assert.StartsWith("Such-Kürzel nicht übernommen: ", arcadia.Main.Toast.Text, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-6")]
    public async Task The_keywords_on_screen_keyboard_offers_only_keys_it_may_hold_and_the_D_pad_skips_the_others()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow("launcher-keyword");
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor!;
        var keyboard = editor.Keyboard;
        Assert.Equal("de", keyboard.Layout.Id);

        // Greyed out, its character still shown: special characters, the space bar, ⇧ and AltGr (µ doesn't count).
        var comma = KeyWith(keyboard, ',');
        Assert.True(comma.IsUnavailable);
        Assert.Equal(",", comma.Label);
        Assert.False(KeyWith(keyboard, 'g').IsUnavailable);
        Assert.All(KeysOf(keyboard, BoardKeyKind.Space, BoardKeyKind.Shift, BoardKeyKind.AltGr), key => Assert.True(key.IsUnavailable, $"{key.Key.Kind} ist nicht ausgegraut."));

        // Y types no space, LB and RB switch to no other level.
        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal("gog", editor.Text);
        arcadia.PressDown(UiAction.PreviousTab);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        arcadia.LetGo(UiAction.PreviousTab).PressDown(UiAction.NextTab);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        arcadia.LetGo(UiAction.NextTab);

        // Along every row and down the board, the focus never rests on a greyed-out key.
        var rows = keyboard.Keys.Select(key => key.Top).Distinct().Count();
        for (var row = 0; row < rows; row++)
        {
            for (var step = 0; step < 16; step++)
            {
                arcadia.Press(row % 2 == 0 ? UiAction.Right : UiAction.Left);
                Assert.False(keyboard.FocusedKey.IsUnavailable, $"Fokus auf der ausgegrauten Taste {keyboard.FocusedKey.Label}.");
            }

            arcadia.Press(UiAction.Down);
            Assert.False(keyboard.FocusedKey.IsUnavailable, $"Fokus auf der ausgegrauten Taste {keyboard.FocusedKey.Label}.");
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-6")]
    public async Task On_a_French_keyboard_shift_stays_for_the_digits_and_offers_only_them()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"], KeyboardLayout = "fr" });
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow("launcher-keyword");
        arcadia.Press(UiAction.Accept);
        var keyboard = arcadia.Main.Settings!.Selected.Editor!.Keyboard;
        Assert.Equal("fr", keyboard.Layout.Id);
        Assert.All(KeysOf(keyboard, BoardKeyKind.Shift), key => Assert.False(key.IsUnavailable));

        arcadia.PressDown(UiAction.PreviousTab);
        Assert.Equal(KeyLevel.Shift, keyboard.Level);
        var offered = keyboard.Keys.Where(key => key.IsCharacter && !key.IsUnavailable && key.Label.Length > 0).Select(key => key.Label).ToList();
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"], offered.Order(StringComparer.Ordinal));
        arcadia.LetGo(UiAction.PreviousTab);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-6")]
    public async Task The_names_on_screen_keyboard_offers_every_key()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow("launcher-name");
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor!;

        Assert.False(KeyWith(editor.Keyboard, ',').IsUnavailable);
        Assert.All(KeysOf(editor.Keyboard, BoardKeyKind.Space, BoardKeyKind.Shift, BoardKeyKind.AltGr), key => Assert.False(key.IsUnavailable));
        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal("GOG Galaxy ", editor.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-6")]
    public async Task A_valid_keyword_holds_at_once_in_the_search_and_its_chip()
    {
        IReadOnlyList<LauncherSpec> launchers =
        [
            LauncherSpec.With(KnownLaunchers.Gog, Installed(KnownLaunchers.Gog, "Hollow Knight", Installed), Installed(KnownLaunchers.Gog, "Iron Harbor", Installed)),
            LauncherSpec.With(KnownLaunchers.Xbox, Installed(KnownLaunchers.Xbox, "Ember Hollow", Installed)),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers);
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        var row = Assert.IsType<TextRowViewModel>(arcadia.FocusRow("launcher-keyword"));

        arcadia.Press(UiAction.Accept).Press(UiAction.Search, times: "gog".Length).Type("gg2");
        Assert.Null(row.Problem);
        arcadia.Press(UiAction.Settings);
        Assert.Equal("gg2", row.ValueText);

        arcadia.Press(UiAction.Back).Press(UiAction.Back).Press(UiAction.Search);
        var search = arcadia.Search!;
        Assert.Contains("/gg2", search.Chips.Select(chip => chip.Keyword));
        arcadia.Main.TypeInSearch("/gg2 hollow");
        Assert.Equal(["Hollow Knight"], search.Hits.Select(game => game.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-7")]
    public async Task The_info_panel_previews_the_order_with_only_the_pages_launcher_framed_and_gives_an_example_for_the_keyword()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        // The tab chosen in the library doesn't matter in the preview.
        arcadia.Press(UiAction.NextTab);
        var settings = arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);

        foreach (var key in new[] { "launcher-active", "launcher-name", "launcher-logo" })
        {
            var row = arcadia.FocusRow(key);
            Assert.Equal(("LAUNCHER-REIHENFOLGE", "Eine Vorschau der Reihenfolge, in der die Launcher aufgeführt werden"), (row.PreviewTitle, row.PreviewSubtitle));
            var tabs = Assert.IsType<InfoTabBar>(Assert.Single(row.Preview));
            Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], tabs.Tabs.Select(tab => tab.Name));
            Assert.Equal(["GOG Galaxy"], tabs.Tabs.Where(tab => tab.IsHighlighted).Select(tab => tab.Name));
            Assert.NotEmpty(row.Info.OfType<InfoText>());
        }

        var keyword = arcadia.FocusRow("launcher-keyword");
        Assert.Contains(keyword.Info.OfType<InfoExample>(), example => example.Sample == "/gog hollow");
        Assert.Same(settings, arcadia.Main.Settings!.Selected);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-7")]
    public async Task Read_again_on_the_page_reads_only_this_launcher_and_then_names_its_games_and_the_time()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(LauncherSettingsDriving.WithoutFolders);
        arcadia.Time.Advance(TimeSpan.FromMinutes(10));
        var settings = arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);

        var rescan = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow("launcher-rescan"));
        Assert.True(rescan.IsEnabled);
        Assert.Equal("2 Spiele · zuletzt vor 10 Min.", rescan.Subtitle);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "2 Spiele in GOG Galaxy gefunden.", "der Toast nach dem Einlesen");
        Assert.Equal("2 Spiele · zuletzt gerade eben", rescan.Subtitle);

        // The other launchers weren't read.
        arcadia.Press(UiAction.Back);
        var xbox = settings.Rows.OfType<LauncherRowViewModel>().Single(row => row.Key == "xbox");
        Assert.Equal("vor 10 Min.", xbox.Fact("Zuletzt eingelesen"));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-7")]
    public async Task Read_again_is_greyed_out_with_the_reason_for_a_launcher_inactive_or_not_found()
    {
        IReadOnlyList<LauncherSpec> launchers =
        [
            new(KnownLaunchers.Gog, 31),
            new(KnownLaunchers.Xbox, 62, IsInstalled: false),
            new(KnownLaunchers.Amazon, 6),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, new AppSettings { ActiveLaunchers = ["gog", "xbox"] });
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.Equal("xbox", settings.PageLauncherId);
        var rescan = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow("launcher-rescan"));
        Assert.Equal((false, "Nicht gefunden"), (rescan.IsEnabled, rescan.Subtitle));

        arcadia.Press(UiAction.NextPage);
        Assert.Equal("amazon", settings.PageLauncherId);
        Assert.Equal((false, "Nur für einen aktiven Launcher"), (rescan.IsEnabled, rescan.Subtitle));

        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    /// <summary>The key that types <paramref name="character"/> on the normal level.</summary>
    private static BoardKeyViewModel KeyWith(OnScreenKeyboardViewModel keyboard, char character) =>
        keyboard.Keys.Single(key => key.IsCharacter && key.Key.Cap!.On(KeyLevel.Normal) == character);

    private static IEnumerable<BoardKeyViewModel> KeysOf(OnScreenKeyboardViewModel keyboard, params BoardKeyKind[] kinds) =>
        keyboard.Keys.Where(key => kinds.Contains(key.Key.Kind) && !key.IsHidden);
}

internal static class LauncherPageDriving
{
    /// <summary>The launcher as Arcadia shows it now, with the name and keyword the user gave it.</summary>
    public static Arcadia.Launchers.Abstractions.LauncherInfo TabsLauncher(this ArcadiaDriver arcadia, string id) =>
        arcadia.Main.Tabs.Single(tab => tab.Launcher.Id == id).Launcher;
}
