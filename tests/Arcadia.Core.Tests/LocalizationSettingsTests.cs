using Arcadia.Core.Search;
using Arcadia.Core.Settings;

namespace Arcadia.Core.Tests;

/// <summary>The settings of "Lokalisierung" (#553): which layouts the keyboards offer, old and broken values.</summary>
public sealed class LocalizationSettingsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-localization-").FullName;
    private readonly List<string> _log = [];

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [Trait("TestCase", "T553-10")]
    [InlineData(0x04070407u, "de")]
    [InlineData(0x04070C07u, "de")] // German (Austria) with the German layout
    [InlineData(0x04070409u, "de")] // the German layout with English as input language
    [InlineData(0x04090409u, "en-us")]
    [InlineData(0x08090809u, "en-gb")]
    [InlineData(0x040C040Cu, "fr")]
    [InlineData(0x0C0A0C0Au, "es")]
    [InlineData(0x040A040Au, "es")]
    [InlineData(0x08070807u, null)] // Swiss German: a layout of its own
    [InlineData(0xF0020409u, null)] // US Dvorak, a variant of the US layout
    [InlineData(0x04100410u, null)] // Italian
    [InlineData(0u, null)] // nothing readable
    public void A_Windows_keyboard_layout_is_one_of_the_five_or_none(uint handle, string? expected)
    {
        Assert.Equal(expected, SystemKeyboardLayouts.IdOf(handle));
    }

    [Theory]
    [Trait("TestCase", "T553-10")]
    [InlineData(null, "fr", "de-DE", "fr")] // the system's layout wins over the UI language
    [InlineData(null, null, "de-DE", "de")] // none of the five: the UI language's
    [InlineData(null, null, "en-US", "en-us")]
    [InlineData(null, null, "en-GB", "en-gb")]
    [InlineData(null, null, "it-IT", "de")]
    [InlineData("es", "fr", "de-DE", "es")] // a saved default wins over everything
    public void Without_a_saved_default_the_system_layout_decides_else_the_UI_language(string? saved, string? system, string language, string expected)
    {
        var choice = KeyboardLayoutChoice.From(new LocalizationSettings { KeyboardLayout = saved }, system, language);

        Assert.Equal(expected, choice.Default.Id);
    }

    [Fact]
    [Trait("TestCase", "T553-10")]
    public void A_default_taken_from_the_system_is_not_written()
    {
        var store = new SettingsStore(SettingsPath);
        var service = new SettingsService(store, _log.Add);

        Assert.Equal("fr", KeyboardLayoutChoice.From(service.Current.Localization, "fr", "de-DE").Default.Id);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    [Trait("TestCase", "T553-11")]
    public void A_layout_saved_by_R3_before_the_update_is_the_default_and_all_five_are_in_the_quick_selection()
    {
        File.WriteAllText(SettingsPath, """{ "KeyboardLayout": "fr", "PreferredPageSize": 64 }""");

        var service = new SettingsService(new SettingsStore(SettingsPath), _log.Add);
        var choice = KeyboardLayoutChoice.From(service.Current.Localization, systemLayoutId: "de", "de-DE");

        Assert.Equal("fr", choice.Default.Id);
        Assert.Equal(KeyboardLayout.All, choice.Quick);
        Assert.Equal(ClockFormat.System, service.Current.Localization.Clock);
        Assert.Empty(_log);

        service.Update(s => s);
        Assert.Equal("fr", new SettingsStore(SettingsPath).Load().Localization.KeyboardLayout);
        Assert.Null(new SettingsStore(SettingsPath).Load().LegacyKeyboardLayout);
    }

    [Fact]
    [Trait("TestCase", "T553-4")]
    public void A_new_default_keeps_the_previous_default_in_the_quick_selection()
    {
        var german = KeyboardLayout.German;
        var french = KeyboardLayout.Find("fr");
        var spanish = KeyboardLayout.Find("es");
        var onlyGerman = new LocalizationSettings { KeyboardLayout = "de", QuickLayouts = [] };

        var changed = onlyGerman.WithDefaultLayout(french, before: german);
        var choice = KeyboardLayoutChoice.From(changed, null, "de-DE");

        Assert.Equal(french, choice.Default);
        Assert.Equal([german, french], choice.Quick);

        // Switched out again, the old default leaves; the new one stays in whatever its switch says.
        var withoutGerman = changed.WithQuickLayout(german, on: false).WithQuickLayout(french, on: false);
        Assert.Equal([french], KeyboardLayoutChoice.From(withoutGerman, null, "de-DE").Quick);
        Assert.False(KeyboardLayoutChoice.From(withoutGerman, null, "de-DE").CanSwitch);

        // The first switch writes all five that were in it, minus the one switched off.
        var first = new LocalizationSettings().WithQuickLayout(spanish, on: false);
        Assert.Equal(["de", "en-us", "en-gb", "fr"], first.QuickLayouts);
    }

    [Theory]
    [Trait("TestCase", "T553-17")]
    [InlineData("""{ "Localization": { "KeyboardLayout": "XX" } }""", 1)]
    [InlineData("""{ "Localization": { "KeyboardLayout": "" } }""", 1)]
    [InlineData("""{ "Localization": { "KeyboardLayout": 42 } }""", 1)]
    [InlineData("""{ "Localization": { "KeyboardLayout": { "id": "fr" } } }""", 1)]
    [InlineData("""{ "Localization": { "QuickLayouts": ["fr", "zz", "fr", 3, null] } }""", 1)]
    [InlineData("""{ "Localization": { "QuickLayouts": "fr" } }""", 1)]
    [InlineData("""{ "Localization": { "Clock": 7 } }""", 1)]
    [InlineData("""{ "Localization": { "Clock": "TwelveHours" } }""", 1)]
    [InlineData("""{ "Localization": { "KeyboardLayout": "XX", "QuickLayouts": 5, "Clock": -1 } }""", 3)]
    public void Unknown_or_unreadable_values_become_the_defaults_with_one_log_line_each_and_keep_the_rest(string localization, int lines)
    {
        File.WriteAllText(SettingsPath, localization.Replace("{ \"Localization\"", "{ \"PreferredPageSize\": 64, \"Localization\"", StringComparison.Ordinal));

        var service = new SettingsService(new SettingsStore(SettingsPath), _log.Add);
        var current = service.Current.Localization;

        Assert.Equal(lines, _log.Count);
        Assert.All(_log, line => Assert.StartsWith("Lokalisierung:", line, StringComparison.Ordinal));
        Assert.Equal(64, service.Current.PreferredPageSize);
        Assert.False(File.Exists(SettingsPath + ".broken"));
        Assert.Null(current.KeyboardLayout);
        Assert.Equal(ClockFormat.System, current.Clock);
        Assert.All(current.QuickLayouts ?? [], id => Assert.NotNull(KeyboardLayout.TryFind(id)));
        Assert.Equal((current.QuickLayouts ?? []).Distinct(), current.QuickLayouts ?? []);
        Assert.Equal("de", KeyboardLayoutChoice.From(current, null, "de-DE").Default.Id);

        // The next change writes valid values.
        service.Update(s => s);
        _log.Clear();
        _ = new SettingsService(new SettingsStore(SettingsPath), _log.Add);
        Assert.Empty(_log);
    }

    [Fact]
    [Trait("TestCase", "T553-17")]
    public void Known_quick_layouts_of_a_broken_list_are_kept()
    {
        File.WriteAllText(SettingsPath, """{ "Localization": { "QuickLayouts": ["fr", "zz", "fr", 3, null] } }""");

        var service = new SettingsService(new SettingsStore(SettingsPath), _log.Add);

        Assert.Equal(["fr"], service.Current.Localization.QuickLayouts);
        Assert.Equal(["de", "fr"], KeyboardLayoutChoice.From(service.Current.Localization, null, "de-DE").Quick.Select(l => l.Id));
    }
}
