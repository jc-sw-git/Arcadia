using System.Globalization;
using System.Text.RegularExpressions;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>
/// The category "Lokalisierung" (#553): the on-screen keyboard's default layout and quick selection, the clock, dates and
/// numbers by the region. Not parallel: the clock and the region are Arcadia-wide, and these tests change them.
/// </summary>
[Collection(Name)]
public sealed class LocalizationSettingsTests : IDisposable
{
    public const string Name = "Clock and region";

    // The switches name layout and kind; the pill of "Standard-Layout" has room only for the name.
    private static readonly string[] LayoutNames = ["Deutsch · QWERTZ", "English (US) · QWERTY", "English (UK) · QWERTY", "Français · AZERTY", "Español · QWERTY con Ñ"];
    private static readonly string[] DefaultNames = ["Deutsch", "English (US)", "English (UK)", "Français", "Español"];

    public void Dispose()
    {
        Formatting.ClockFormat = ClockFormat.System;
        Formatting.UseRegion(TestSystem.GermanRegion);
        SystemKeyboardLayout.Read = () => null;
        SystemKeyboardLayout.Forget();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-1")]
    public async Task The_category_sits_before_About_with_the_keyboard_and_clock_groups_and_B_goes_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var category = Open(arcadia);

        var names = arcadia.Main.Settings!.Categories.Select(c => c.Name).ToList();
        Assert.Equal(names.IndexOf(AppTexts.AboutTitle) - 1, names.IndexOf(AppTexts.LocalizationTitle));
        Assert.Equal(category.Rows[0], arcadia.Main.Settings.FocusedRow);
        Assert.Equal(
            [AppTexts.LocalizationDefaultLayout, .. LayoutNames, AppTexts.LocalizationClock],
            category.Rows.Select(row => row.Title));
        Assert.Equal([AppTexts.LocalizationKeyboardGroup, AppTexts.LocalizationClockGroup], category.Rows.Where(r => r.HasGroupHeading).Select(r => r.Group));
        Assert.All(category.Rows.Skip(1).Take(5), row => Assert.Equal(1, row.Indent));
        Assert.Equal(5, category.Rows[0].ValueBoxes.Count);
        Assert.Equal(3, arcadia.FocusRow(LocalizationSettingsViewModel.ClockKey).ValueBoxes.Count);
        Assert.Equal(AppTexts.LocalizationClockSystem, CurrentBox(category.Rows[^1]).Title);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-3")]
    public async Task The_default_layout_steps_and_wraps_and_is_saved_at_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);
        var row = Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow(LocalizationSettingsViewModel.DefaultLayoutKey));

        Assert.Equal(DefaultNames[0], row.ValueText);
        arcadia.Press(UiAction.Left);
        Assert.Equal(DefaultNames[4], row.ValueText);
        Assert.Equal("es", arcadia.Settings.Current.Localization.KeyboardLayout);
        arcadia.Press(UiAction.Right);
        Assert.Equal("de", arcadia.Settings.Current.Localization.KeyboardLayout);
        arcadia.Click(row.StepOnCommand);
        Assert.Equal("en-us", arcadia.Settings.Current.Localization.KeyboardLayout);

        arcadia.Press(UiAction.Back);
        using var restarted = await arcadia.RestartAsync();
        Open(restarted);
        Assert.Equal(DefaultNames[1], Assert.IsAssignableFrom<ChoiceRowViewModel>(restarted.FocusRow(LocalizationSettingsViewModel.DefaultLayoutKey)).ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-4")]
    [Trait("TestCase", "T553-5")]
    public async Task The_defaults_switch_is_on_and_greyed_and_the_others_switch_the_quick_selection()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);
        var german = Switch(arcadia, "de");

        Assert.True(german.IsOn);
        Assert.True(german.HasDimmedNote);
        Assert.Equal(AppTexts.LocalizationDefaultAlwaysIn, german.DimmedNote);
        arcadia.Press(UiAction.Left).Press(UiAction.Right).Press(UiAction.Accept);
        arcadia.Click(german.ClickCommand);
        Assert.True(german.IsOn);
        Assert.Null(arcadia.Settings.Current.Localization.QuickLayouts);

        var french = Switch(arcadia, "fr");
        arcadia.Press(UiAction.Right);
        Assert.False(french.IsOn);
        Assert.Equal(["de", "en-us", "en-gb", "es"], arcadia.Settings.Current.Localization.QuickLayouts);

        // French as the default: its switch on and greyed, German's now free and still on.
        arcadia.FocusRow(LocalizationSettingsViewModel.DefaultLayoutKey);
        arcadia.Press(UiAction.Right, times: 3);
        Assert.True(french.IsOn);
        Assert.True(french.IsDimmed);
        Assert.False(german.IsDimmed);
        Assert.True(german.IsOn);
        Switch(arcadia, "de");
        arcadia.Click(german.ClickCommand);
        Assert.False(german.IsOn);

        arcadia.Press(UiAction.Back);
        using var restarted = await arcadia.RestartAsync();
        Assert.Equal(["en-us", "en-gb", "fr", "es"], SystemKeyboardLayout.Choice(restarted.Settings.Current.Localization).Quick.Select(l => l.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-6")]
    [Trait("TestCase", "T553-7")]
    public async Task The_search_keyboard_opens_in_the_default_and_an_R3_switch_holds_only_until_it_closes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"], Localization = new() { KeyboardLayout = "fr" } });
        arcadia.Press(UiAction.Search);
        Assert.Equal("fr", arcadia.Keyboard().Layout.Id);

        // B puts the keyboard away, X brings it out again: it closed in between, so it is back on the default.
        PickLayout(arcadia, "US");
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Search!.IsKeyboardOut);
        arcadia.Press(UiAction.Search);
        Assert.True(arcadia.Search!.IsKeyboardOut);
        Assert.Equal("fr", arcadia.Keyboard().Layout.Id);

        PickLayout(arcadia, "US");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Search);
        Assert.Equal("fr", arcadia.Settings.Current.Localization.KeyboardLayout);

        arcadia.Press(UiAction.Search);
        Assert.Equal("fr", arcadia.Keyboard().Layout.Id);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-6")]
    [Trait("TestCase", "T553-7")]
    public async Task A_settings_text_field_opens_in_the_default_and_forgets_an_R3_switch()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"], Localization = new() { KeyboardLayout = "es" } });
        var keyboard = OpenKeywordEditor(arcadia);
        Assert.Equal("es", keyboard.Layout.Id);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(keyboard.LayoutMenu.IsOpen);
        arcadia.Press(UiAction.Up).Press(UiAction.Accept);
        Assert.Equal("fr", keyboard.Layout.Id);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings!.Selected.Editor);

        arcadia.Press(UiAction.Accept);
        Assert.Equal("es", arcadia.Main.Settings.Selected.Editor!.Keyboard.Layout.Id);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-6")]
    [Trait("TestCase", "T553-8")]
    public void The_mouse_modes_keyboard_opens_in_the_default_and_offers_only_the_quick_selection()
    {
        var spanishOnly = KeyboardLayoutChoice.From(new LocalizationSettings { KeyboardLayout = "es", QuickLayouts = [] }, null, "de-DE");
        var two = KeyboardLayoutChoice.From(new LocalizationSettings { KeyboardLayout = "es", QuickLayouts = ["en-gb"] }, null, "de-DE");
        var keyboard = new OnScreenKeyboardViewModel(two, new NoTarget(), new FakeTimeProvider(ArcadiaDriver.Now), action => action());

        Assert.Equal("es", keyboard.Layout.Id);
        Assert.Equal(["en-gb", "es"], keyboard.LayoutMenu.Items.Select(item => item.Layout.Id));
        Assert.False(LayoutKey(keyboard).IsUnavailable);

        keyboard.Open(spanishOnly, atTop: false);
        Assert.True(LayoutKey(keyboard).IsUnavailable);
        PressLayout(keyboard);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        LayoutKey(keyboard).PressCommand.Execute(null);
        Assert.False(keyboard.LayoutMenu.IsOpen);

        keyboard.Open(two, atTop: false);
        Assert.False(LayoutKey(keyboard).IsUnavailable);
        PressLayout(keyboard);
        Assert.True(keyboard.LayoutMenu.IsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-8")]
    public async Task R3_offers_only_the_quick_selection_and_is_greyed_with_the_default_alone()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"], Localization = new() { KeyboardLayout = "de", QuickLayouts = ["fr"] } });
        arcadia.Press(UiAction.Search);
        var keyboard = arcadia.Keyboard();
        Assert.Equal(["de", "fr"], keyboard.LayoutMenu.Items.Select(item => item.Layout.Id));
        arcadia.Press(UiAction.Back).Press(UiAction.Back);

        arcadia.Settings.Update(s => s with { Localization = s.Localization with { QuickLayouts = [] } });
        arcadia.Press(UiAction.Search);
        Assert.True(LayoutKey(keyboard).IsUnavailable);
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        Assert.Equal("de", keyboard.Layout.Id);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-9")]
    public async Task R3_without_an_open_keyboard_changes_no_layout()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Search).Press(UiAction.Back);
        Assert.False(arcadia.Search!.IsKeyboardOut);

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.False(arcadia.Keyboard().LayoutMenu.IsOpen);
        Assert.Null(arcadia.Settings.Current.Localization.KeyboardLayout);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-10")]
    public async Task Without_a_saved_default_the_row_shows_the_systems_layout_and_nothing_is_written()
    {
        SystemKeyboardLayout.Read = () => "en-gb";
        SystemKeyboardLayout.Forget();
        using var arcadia = await ArcadiaDriver.StartAsync();
        Open(arcadia);

        Assert.Equal(DefaultNames[2], Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow(LocalizationSettingsViewModel.DefaultLayoutKey)).ValueText);
        Assert.True(Switch(arcadia, "en-gb").IsDimmed);
        Assert.Null(arcadia.Settings.Current.Localization.KeyboardLayout);
        arcadia.Press(UiAction.Back).Press(UiAction.Search);
        Assert.Equal("en-gb", arcadia.Keyboard().Layout.Id);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-12")]
    public async Task The_clock_steps_through_its_values_and_every_time_shown_follows_at_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Main.Toast.Show(Arcadia.ControllerSupport.HealthSeverity.Info, "Eine Meldung");
        Open(arcadia);
        var clock = Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow(LocalizationSettingsViewModel.ClockKey));
        Assert.Equal(AppTexts.LocalizationClockSystem, clock.ValueText);
        Assert.Equal("20:00", arcadia.Main.Status.Clock);

        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.LocalizationClock12, clock.ValueText);
        Assert.Equal(ClockFormat.TwelveHours, arcadia.Settings.Current.Localization.Clock);
        Assert.Equal("8:00 PM", arcadia.Main.Status.Clock);
        Assert.Equal("8:00 PM", Formatting.Clock(arcadia.Time.GetLocalNow()));
        Assert.Equal(AppTexts.AtTime("6:00 PM"), LauncherTexts.Ago(arcadia.Time, arcadia.Time.GetUtcNow().AddHours(-2)));

        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.LocalizationClockSystem, clock.ValueText);
        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.LocalizationClock24, clock.ValueText);
        Assert.Equal("20:00", arcadia.Main.Status.Clock);
        Assert.Equal(AppTexts.AtTime("18:00 Uhr"), LauncherTexts.Ago(arcadia.Time, arcadia.Time.GetUtcNow().AddHours(-2)));

        // "Letzte Meldungen" shows its times anew each time the list is shown.
        arcadia.Press(UiAction.Left, times: 2);
        arcadia.Press(UiAction.NextTab);
        var recent = arcadia.FocusRow("logging-list");
        Assert.Contains(Flatten(recent.Info).OfType<InfoMessage>(), entry => entry.TimeText == "8:00 PM");
    }

    [Theory]
    [Trait("TestCase", "T553-13")]
    [InlineData(0, 5, "12:05 AM", "00:05")]
    [InlineData(9, 5, "9:05 AM", "09:05")]
    [InlineData(12, 0, "12:00 PM", "12:00")]
    [InlineData(23, 59, "11:59 PM", "23:59")]
    public void Twelve_and_twenty_four_hours_with_AM_and_PM_also_in_a_German_region(int hour, int minute, string twelve, string twentyFour)
    {
        var time = new DateTimeOffset(2026, 11, 13, hour, minute, 0, TimeSpan.Zero);

        Formatting.ClockFormat = ClockFormat.TwelveHours;
        Assert.Equal(twelve, Formatting.Clock(time));
        Assert.Equal(twelve, Formatting.ClockWithUnit(time));
        Formatting.ClockFormat = ClockFormat.TwentyFourHours;
        Assert.Equal(twentyFour, Formatting.Clock(time));
        Assert.Equal(twentyFour + " Uhr", Formatting.ClockWithUnit(time));
    }

    [Fact]
    [Trait("TestCase", "T553-14")]
    public void No_display_text_keeps_a_fixed_time_or_date_format()
    {
        var root = Path.Combine(RepoRoot(), "src");
        var resources = Directory.EnumerateFiles(root, "*Texts*.resx", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories));
        Assert.All(resources, file => Assert.DoesNotMatch(new Regex("HH:mm|hh:mm|dd\\.MM"), File.ReadAllText(file)));
        Assert.DoesNotContain("de-DE", File.ReadAllText(Path.Combine(root, "Arcadia.App", "Services", "Formatting.cs")), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("TestCase", "T553-15")]
    [InlineData("de-DE", "21:58")]
    [InlineData("en-US", "9:58 PM")]
    [InlineData("en-GB", "21:58")]
    public void Like_the_system_follows_the_systems_time_format(string region, string expected)
    {
        Formatting.UseRegion(CultureInfo.GetCultureInfo(region));
        Formatting.ClockFormat = ClockFormat.System;

        // ICU puts a narrow no-break space before PM, as Windows does: shown as it comes.
        Assert.Equal(expected, Formatting.Clock(new DateTimeOffset(2026, 11, 13, 21, 58, 0, TimeSpan.Zero)).Replace('\u202f', ' '));
    }

    [Theory]
    [Trait("TestCase", "T553-16")]
    [InlineData("de-DE", "13.11.2026", "12,5 GB", "1.234")]
    [InlineData("en-US", "11/13/2026", "12.5 GB", "1,234")]
    [InlineData("en-GB", "13/11/2026", "12.5 GB", "1,234")]
    [InlineData("fr-FR", "13/11/2026", "12,5 GB", "1 234")]
    public void Dates_and_numbers_follow_the_region_and_names_the_UI_language(string region, string date, string size, string count)
    {
        Formatting.UseRegion(CultureInfo.GetCultureInfo(region));

        Assert.Equal(date, Formatting.Date(new DateOnly(2026, 11, 13)));
        Assert.Equal(size, Formatting.Size((long)(12.5 * 1024 * 1024 * 1024)));
        Assert.Equal(count, Formatting.Count(1234));
        Assert.Contains("Nov", Formatting.ShortDate(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)), StringComparison.Ordinal);
        Assert.Contains("Fr", Formatting.ShortDate(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)), StringComparison.Ordinal);
        Assert.Equal("42\u00A0%", Formatting.Percent(0.429));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T553-16")]
    public async Task With_an_English_US_region_the_library_counts_and_dates_follow_it_and_texts_stay_German()
    {
        Formatting.UseRegion(CultureInfo.GetCultureInfo("en-US"));
        using var arcadia = await ArcadiaDriver.StartAsync();

        Assert.Equal("Fr, Nov 13", Formatting.ShortDate(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)).Replace(".", string.Empty, StringComparison.Ordinal));
        Assert.Equal("11/13/2026, 9:58 PM", Formatting.DateAndTime(new DateTimeOffset(2026, 11, 13, 21, 58, 0, TimeSpan.Zero)).Replace('\u202f', ' '));
        Assert.Equal("Sa, Okt 24", arcadia.Main.Status.Date.Replace(".", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T553-18")]
    public void The_clock_reads_the_region_once_not_per_tick()
    {
        var first = Formatting.Region;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Same(first, Formatting.Region);
        }
        finally
        {
            CultureInfo.CurrentCulture = TestSystem.GermanRegion;
        }
    }

    // From this file, not the build output: the CI runs the tests from a copied output folder.
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Arcadia.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException($"Arcadia.slnx not found above {thisFile}.");
    }

    private static LocalizationSettingsViewModel Open(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var tabs = 0; tabs < 8 && arcadia.Main.Settings!.Selected is not LocalizationSettingsViewModel; tabs++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        return Assert.IsType<LocalizationSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static SwitchRowViewModel Switch(ArcadiaDriver arcadia, string layoutId) =>
        Assert.IsAssignableFrom<SwitchRowViewModel>(arcadia.FocusRow(LocalizationSettingsViewModel.QuickLayoutKey(KeyboardLayout.Find(layoutId))));

    private static ViewModels.ValueBox CurrentBox(SettingsRowViewModel row) => Assert.Single(row.ValueBoxes, box => box.IsCurrent);

    private static BoardKeyViewModel LayoutKey(OnScreenKeyboardViewModel keyboard) => keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Layout);

    private static void PressLayout(OnScreenKeyboardViewModel keyboard)
    {
        keyboard.Handle(KeyboardButton.Layout, down: true);
        keyboard.Handle(KeyboardButton.Layout, down: false);
    }

    private static IEnumerable<InfoBlock> Flatten(IEnumerable<InfoBlock> blocks) =>
        blocks.SelectMany(block => block is InfoSection section ? Flatten(section.Blocks).Prepend(block) : [block]);

    private static void PickLayout(ArcadiaDriver arcadia, string code)
    {
        var menu = arcadia.Keyboard().LayoutMenu;
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(menu.IsOpen);
        for (var i = 0; i < menu.Items.Count && menu.Items.Single(item => item.IsFocused).Layout.Code != code; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.Equal(code, arcadia.Keyboard().Layout.Code);
    }

    private static OnScreenKeyboardViewModel OpenKeywordEditor(ArcadiaDriver arcadia)
    {
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow("launcher-keyword");
        arcadia.Press(UiAction.Accept);
        return arcadia.Main.Settings!.Selected.Editor!.Keyboard;
    }

    private sealed class NoTarget : IKeyboardTarget
    {
        public void Type(char character)
        {
        }

        public void Press(BoardKeyKind key, bool withShift)
        {
        }

        public void Hide()
        {
        }

        public void Dock(bool atTop)
        {
        }
    }
}

[CollectionDefinition(LocalizationSettingsTests.Name, DisableParallelization = true)]
public sealed class ClockAndRegionCollection;
