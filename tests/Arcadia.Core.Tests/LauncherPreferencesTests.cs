using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;

namespace Arcadia.Core.Tests;

public sealed class LauncherPreferencesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-launchers-").FullName;

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static LauncherInfo Steam => KnownLaunchers.Steam;

    private static LauncherInfo Epic => KnownLaunchers.Epic;

    [Theory]
    [InlineData("Steam Deck", null)]
    [InlineData("  ", "Der Name darf nicht leer sein.")]
    [InlineData("", "Der Name darf nicht leer sein.")]
    [InlineData("Ein Name mit viel zu vielen Zeichen", "Höchstens 24 Zeichen.")]
    [InlineData("Tab\tName", "Der Name enthält ein Steuerzeichen.")]
    public void A_name_must_not_be_empty_or_too_long(string name, string? problem) =>
        Assert.Equal(problem, LauncherNames.NameProblem(name));

    [Theory]
    [InlineData("valve", null)]
    [InlineData("st3am", null)]
    [InlineData("spiele", null)]
    [InlineData("übersicht", null)]
    [InlineData("éñçß", null)]
    [InlineData("µ", "Nur Kleinbuchstaben und Ziffern.")]
    [InlineData("ﬁ", "Nur Kleinbuchstaben und Ziffern.")]
    [InlineData("", "Das Kürzel darf nicht leer sein.")]
    [InlineData("steam-epic", "Kein „-“: Er trennt mehrere Launcher.")]
    [InlineData("Steam", "Nur Kleinbuchstaben und Ziffern.")]
    [InlineData("st am", "Nur Kleinbuchstaben und Ziffern.")]
    [InlineData("/steam", "Nur Kleinbuchstaben und Ziffern.")]
    [InlineData("abcdefghijklm", "Höchstens 12 Zeichen.")]
    [InlineData("epic", "/epic hat schon Epic Games.")]
    public void A_keyword_holds_only_lower_case_letters_and_digits_and_belongs_to_no_other_launcher(string keyword, string? problem) =>
        Assert.Equal(problem, LauncherNames.KeywordProblem(keyword, Steam.Id, [Steam, Epic]));

    [Fact]
    public void A_launcher_may_keep_its_own_keyword() =>
        Assert.Null(LauncherNames.KeywordProblem("steam", Steam.Id, [Steam, Epic]));

    [Fact]
    public void The_registry_shows_the_name_and_keyword_the_user_gave_and_the_launcher_keeps_its_own_identity()
    {
        var preferences = new Dictionary<string, LauncherPreferences> { ["steam"] = new(Name: "Valve", Keyword: "valve") };
        var registry = new LauncherRegistry([new FakeIntegration(Steam), new FakeIntegration(Epic)], () => null, id => preferences.GetValueOrDefault(id) ?? new());

        Assert.Equal(Steam with { DisplayName = "Valve", SearchKeyword = "valve" }, registry.Info("steam"));
        Assert.Equal(Epic, registry.Info("epic"));
        Assert.Equal(["Valve", "Epic Games"], registry.Shown.Select(l => l.DisplayName));
    }

    [Theory]
    [InlineData("", "a-b")]
    [InlineData("   ", "STEAM")]
    [InlineData("Ein Name mit viel zu vielen Zeichen", "")]
    public void Values_that_break_the_rules_give_way_to_the_launchers_own(string name, string keyword)
    {
        var registry = new LauncherRegistry([new FakeIntegration(Steam)], () => null, _ => new LauncherPreferences(name, keyword));

        Assert.Equal(Steam, registry.Info("steam"));
    }

    [Fact]
    public async Task The_library_names_its_tabs_as_the_user_named_the_launchers()
    {
        var registry = new LauncherRegistry([new FakeIntegration(Steam)], () => null, _ => new LauncherPreferences(Name: "Valve"));

        var libraries = await new LibraryService(registry).LoadAsync();

        Assert.Equal("Valve", Assert.Single(libraries).Launcher.DisplayName);
    }

    [Fact]
    public void Launcher_preferences_survive_a_restart_and_an_entry_with_nothing_set_goes()
    {
        var service = new SettingsService(new SettingsStore(SettingsPath));
        service.Update(s => s.WithLauncherPreferences("steam", p => p with { Name = "Valve", LogoFile = "steam-1.png" }));
        service.Update(s => s.WithLauncherPreferences("epic", p => p with { Keyword = "eg" }));
        service.Update(s => s.WithLauncherPreferences("epic", p => p with { Keyword = null }));

        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(new LauncherPreferences(Name: "Valve", LogoFile: "steam-1.png"), restarted.PreferencesOf("steam"));
        Assert.False(restarted.Launchers.ContainsKey("epic"));
        Assert.True(restarted.PreferencesOf("gog").IsEmpty);
    }

    [Fact]
    public void Settings_without_launcher_preferences_or_with_null_read_as_nothing_set()
    {
        File.WriteAllText(SettingsPath, """{ "Launchers": null }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.True(loaded.PreferencesOf("steam").IsEmpty);
        Assert.Equal("eg", loaded.WithLauncherPreferences("epic", p => p with { Keyword = "eg" }).PreferencesOf("epic").Keyword);
    }

    [Fact]
    public void A_chosen_folder_is_checked_again_before_every_search_and_one_no_longer_valid_gives_way_to_the_own_search()
    {
        var choice = new FakeFolderChoice { Valid = { [@"D:\Steam"] = @"D:\Steam" } };
        string? chosen = @"D:\Steam";
        var registry = new LauncherRegistry([new FakeIntegration(Steam) { FolderChoice = choice }], () => null, _ => new LauncherPreferences(Folder: chosen));

        registry.Locate();
        Assert.Equal(@"D:\Steam", choice.Used);

        choice.Valid.Clear();
        registry.Locate();
        Assert.Null(choice.Used);

        chosen = null;
        choice.Used = "unchanged";
        registry.Locate();
        Assert.Null(choice.Used);
    }

    private sealed class FakeFolderChoice : ILauncherFolderChoice
    {
        public Dictionary<string, string> Valid { get; } = [];

        public string? Used { get; set; }

        public string Expected => "launcher.exe";

        public string? Find(string folder) => Valid.GetValueOrDefault(folder);

        public void Use(string? folder) => Used = folder;
    }
}
