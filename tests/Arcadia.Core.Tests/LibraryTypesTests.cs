using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;

namespace Arcadia.Core.Tests;

public sealed class LibraryTypesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-types-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static readonly LibraryTypeOption Tools = new("tools", "Werkzeuge", "", "", [], [GameType.Tool]);

    private static readonly LibraryTypeOption Soundtracks = new("soundtracks", "Soundtracks", "", "", [], [GameType.Soundtrack]);

    private static readonly LibraryTypeOption SoftwareAndTools = new("software", "Software", "", "", [], [GameType.Software, GameType.Tool]);

    private static Game GameOf(string id, GameType type, bool installed = true) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = id,
        Type = type,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    [Fact]
    public void Games_are_always_shown()
    {
        Assert.True(LibraryTypes.IsShown(GameType.Game, [Tools], []));
        Assert.True(LibraryTypes.IsShown(GameType.Game, [], []));
    }

    [Fact]
    public void A_type_with_a_switch_is_shown_only_while_its_switch_is_on()
    {
        Assert.False(LibraryTypes.IsShown(GameType.Tool, [Tools, Soundtracks], []));
        Assert.False(LibraryTypes.IsShown(GameType.Tool, [Tools, Soundtracks], ["soundtracks"]));
        Assert.True(LibraryTypes.IsShown(GameType.Tool, [Tools, Soundtracks], ["tools"]));
    }

    [Fact]
    public void A_type_two_switches_name_is_shown_while_either_is_on()
    {
        Assert.True(LibraryTypes.IsShown(GameType.Tool, [Tools, SoftwareAndTools], ["software"]));
        Assert.True(LibraryTypes.IsShown(GameType.Tool, [Tools, SoftwareAndTools], ["tools"]));
        Assert.False(LibraryTypes.IsShown(GameType.Tool, [Tools, SoftwareAndTools], []));
    }

    [Fact]
    public void A_type_no_switch_names_is_always_shown()
    {
        Assert.True(LibraryTypes.IsShown(GameType.Software, [Tools, Soundtracks], []));
        Assert.True(LibraryTypes.IsShown(GameType.Tool, [], []));
    }

    [Fact]
    public void Unknown_switch_ids_change_nothing()
    {
        Assert.False(LibraryTypes.IsShown(GameType.Tool, [Tools], ["removed-in-an-update"]));
    }

    [Fact]
    public void Shown_keeps_the_list_when_nothing_is_left_out()
    {
        IReadOnlyList<Game> games = [GameOf("a", GameType.Game), GameOf("b", GameType.Game)];
        IReadOnlyList<Game> withTool = [GameOf("a", GameType.Game), GameOf("t", GameType.Tool)];

        Assert.Same(games, LibraryTypes.Shown(games, [Tools], []));
        Assert.Same(withTool, LibraryTypes.Shown(withTool, [Tools], ["tools"]));
    }

    [Fact]
    public void Shown_leaves_out_the_types_switched_off()
    {
        IReadOnlyList<Game> games = [GameOf("a", GameType.Game), GameOf("t", GameType.Tool), GameOf("s", GameType.Soundtrack), GameOf("x", GameType.Software)];

        Assert.Equal(["a", "s", "x"], LibraryTypes.Shown(games, [Tools, Soundtracks], ["soundtracks"]).Select(g => g.Id));
    }

    [Fact]
    public void Counts_each_type_with_its_installed_entries_in_the_count_lines_order()
    {
        Game[] games =
        [
            GameOf("s", GameType.Soundtrack),
            GameOf("t1", GameType.Tool),
            GameOf("g1", GameType.Game),
            GameOf("g2", GameType.Game, installed: false),
            GameOf("t2", GameType.Tool),
            GameOf("u", (GameType)99, installed: false),
        ];

        Assert.Equal(
            [
                new TypeCount(GameType.Game, 2, 1),
                new TypeCount(GameType.Tool, 2, 2),
                new TypeCount(GameType.Soundtrack, 1, 1),
                new TypeCount((GameType)99, 1, 0),
            ],
            LibraryTypes.Count(games));
        Assert.Empty(LibraryTypes.Count([]));
    }

    [Fact]
    public void A_switch_goes_on_and_off_and_leaves_the_others()
    {
        var preferences = new LauncherPreferences().WithLibraryOption("tools", true).WithLibraryOption("soundtracks", true);

        Assert.Equal(["tools", "soundtracks"], preferences.SwitchedOnOptions);
        Assert.Equal(["soundtracks"], preferences.WithLibraryOption("tools", false).SwitchedOnOptions);
        Assert.Equal(["soundtracks", "tools"], preferences.WithLibraryOption("tools", true).SwitchedOnOptions.Order());
    }

    [Fact]
    public void All_switches_off_is_nothing_set()
    {
        var preferences = new LauncherPreferences().WithLibraryOption("tools", true).WithLibraryOption("tools", false);

        Assert.Null(preferences.LibraryOptions);
        Assert.True(preferences.IsEmpty);
        Assert.False(new LauncherPreferences().WithLibraryOption("tools", true).IsEmpty);
    }

    [Fact]
    public void Switches_survive_a_restart_and_a_hand_edited_null_reads_as_off()
    {
        var path = Path.Combine(_directory, "settings.json");
        new SettingsService(new SettingsStore(path)).Update(s => s.WithLauncherPreferences("steam", p => p.WithLibraryOption("tools", true)));

        var restarted = new SettingsService(new SettingsStore(path)).Current;

        Assert.Equal(["tools"], restarted.PreferencesOf("steam").SwitchedOnOptions);

        File.WriteAllText(path, """{ "Launchers": { "steam": { "LibraryOptions": [null, "tools"] }, "epic": { "LibraryOptions": null } } }""");
        var edited = new SettingsService(new SettingsStore(path)).Current;

        Assert.Equal(["tools"], edited.PreferencesOf("steam").SwitchedOnOptions);
        Assert.Empty(edited.PreferencesOf("epic").SwitchedOnOptions);
    }

    [Fact]
    public void The_registry_shows_a_launchers_entries_by_its_switches()
    {
        var preferences = new LauncherPreferences();
        var registry = new LauncherRegistry(
            [new FakeIntegration(KnownLaunchers.Steam) { LibraryOptions = [Tools] }, new FakeIntegration(KnownLaunchers.Epic)],
            () => null,
            id => id == "steam" ? preferences : new LauncherPreferences());
        IReadOnlyList<Game> games = [GameOf("g", GameType.Game), GameOf("t", GameType.Tool)];

        Assert.Equal(["g"], registry.ShownEntries("steam", games).Select(g => g.Id));
        Assert.False(registry.IsOptionOn("steam", "tools"));

        // An integration without switches shows every type.
        Assert.Same(games, registry.ShownEntries("epic", games));
        Assert.Empty(registry.LibraryOptions("epic"));
        Assert.Empty(registry.LibraryOptions("unknown"));

        preferences = preferences.WithLibraryOption("tools", true);

        Assert.Same(games, registry.ShownEntries("steam", games));
        Assert.True(registry.IsOptionOn("steam", "tools"));
    }
}
