using Arcadia.Core.Settings;

namespace Arcadia.Core.Tests;

/// <summary>Which entries a game's quick selection in "Übersicht" shows, and how it is stored (#500).</summary>
public sealed class QuickSelectionSettingsTests : IDisposable
{
    private static readonly string[] Defaults = ["controller-mode", "mouse-mode", "artwork", "uninstall"];

    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-quick-").FullName;

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    [Trait("TestCase", "T500-1")]
    public void A_game_never_changed_shows_the_defaults()
    {
        Assert.Equal(Defaults, new QuickSelectionSettings().Of("steam:1", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-13")]
    public void A_game_s_own_selection_holds_for_it_alone_and_survives_a_restart()
    {
        new SettingsService(new SettingsStore(SettingsPath)).Update(s => s with { QuickSelection = s.QuickSelection.WithGame("steam:1", ["artwork"]) });

        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection;

        Assert.Equal(["artwork"], restarted.Of("steam:1", Defaults));
        Assert.Equal(Defaults, restarted.Of("steam:2", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-15")]
    public void An_empty_selection_stays_empty_and_is_not_the_defaults()
    {
        new SettingsService(new SettingsStore(SettingsPath)).Update(s => s with { QuickSelection = s.QuickSelection.WithGame("steam:1", []) });

        Assert.Empty(new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection.Of("steam:1", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-18")]
    [Trait("TestCase", "T500-19")]
    [Trait("TestCase", "T500-20")]
    public void For_all_games_replaces_every_own_selection_holds_for_games_added_later_and_one_game_can_differ_again()
    {
        var own = new QuickSelectionSettings().WithGame("steam:1", ["artwork"]).WithGame("epic:2", ["uninstall"]);

        var all = QuickSelectionSettings.ForAllGames(["mouse-mode"]);

        Assert.Empty(all.PerGame);
        Assert.Equal(["mouse-mode"], all.Of("steam:1", Defaults));
        Assert.Equal(["mouse-mode"], all.Of("gog:added-later", Defaults));
        var oneDiffers = all.WithGame("steam:1", ["artwork", "uninstall"]);
        Assert.Equal(["artwork", "uninstall"], oneDiffers.Of("steam:1", Defaults));
        Assert.Equal(["mouse-mode"], oneDiffers.Of("epic:2", Defaults));
        Assert.NotEqual(own.Of("steam:1", Defaults), all.Of("steam:1", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-21")]
    public void A_new_default_entry_reaches_only_games_never_changed_and_only_while_nothing_was_applied_to_all()
    {
        string[] olderDefaults = ["controller-mode", "artwork"];
        var stored = new QuickSelectionSettings().WithGame("steam:own", ["artwork"]);

        // A later version's defaults gain "uninstall".
        Assert.Equal(Defaults, stored.Of("steam:never-changed", Defaults));
        Assert.Equal(["artwork"], stored.Of("steam:own", Defaults));
        Assert.Equal(olderDefaults, stored.Of("steam:never-changed", olderDefaults));
        Assert.Equal(["controller-mode"], QuickSelectionSettings.ForAllGames(["controller-mode"]).Of("steam:never-changed", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-14")]
    public void No_path_stores_more_than_five_entries_or_one_twice()
    {
        string[] six = ["a", "b", "c", "d", "e", "f"];

        Assert.Equal(["a", "b", "c", "d", "e"], new QuickSelectionSettings().WithGame("steam:1", six).Of("steam:1", Defaults));
        Assert.Equal(["a", "b", "c", "d", "e"], QuickSelectionSettings.ForAllGames(six).Of("steam:1", Defaults));
        Assert.Equal(["a"], new QuickSelectionSettings().WithGame("steam:1", ["a", "a"]).Of("steam:1", Defaults));

        File.WriteAllText(SettingsPath, """{ "QuickSelection": { "AllGames": ["a", "b", "c", "d", "e", "f", "g"] } }""");
        Assert.Equal(5, new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection.Of("steam:1", Defaults).Count);
    }

    [Theory]
    [Trait("TestCase", "T500-22")]
    [InlineData("""{ "PanEnabled": true, "QuickSelection": 42 }""")]
    [InlineData("""{ "PanEnabled": true, "QuickSelection": { "PerGame": { "steam:1": "artwork" } } }""")]
    [InlineData("""{ "PanEnabled": true, "QuickSelection": { "AllGames": { "nope": 1 } } }""")]
    public void An_unreadable_quick_selection_gives_the_defaults_and_keeps_every_other_setting(string json)
    {
        File.WriteAllText(SettingsPath, json);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.True(loaded.PanEnabled);
        Assert.True(loaded.QuickSelection.WasUnreadable);
        Assert.Equal(Defaults, loaded.QuickSelection.Of("steam:1", Defaults));
        Assert.False(File.Exists(SettingsPath + ".broken"));
    }

    [Theory]
    [Trait("TestCase", "T500-22")]
    [InlineData("""{ "QuickSelection": null }""")]
    [InlineData("""{ "QuickSelection": { "PerGame": { "steam:1": null }, "AllGames": null } }""")]
    public void A_quick_selection_written_as_null_gives_the_defaults(string json)
    {
        File.WriteAllText(SettingsPath, json);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection;

        Assert.Equal(Defaults, loaded.Of("steam:1", Defaults));
    }

    [Fact]
    [Trait("TestCase", "T500-22")]
    public void Nulls_and_empty_ids_inside_a_selection_drop_out()
    {
        File.WriteAllText(SettingsPath, """{ "QuickSelection": { "PerGame": { "steam:1": ["artwork", null, "", "uninstall"] } } }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection;

        Assert.Equal(["artwork", "uninstall"], loaded.Of("steam:1", Defaults));
        Assert.False(loaded.WasUnreadable);
    }

    [Fact]
    [Trait("TestCase", "T500-23")]
    public void For_all_games_is_stored_in_one_save_so_a_failed_write_leaves_the_old_file_whole()
    {
        var service = new SettingsService(new SettingsStore(SettingsPath));
        service.Update(s => s with { QuickSelection = s.QuickSelection.WithGame("steam:1", ["artwork"]).WithGame("steam:2", ["uninstall"]) });
        var before = File.ReadAllText(SettingsPath);

        // The temporary file can't be written: the save fails before settings.json is touched.
        Directory.CreateDirectory(SettingsPath + ".tmp");
        var failed = Record.Exception(() => service.Update(s => s with { QuickSelection = QuickSelectionSettings.ForAllGames(["mouse-mode"]) }));
        Assert.True(failed is IOException or UnauthorizedAccessException, failed?.ToString());

        Assert.Equal(before, File.ReadAllText(SettingsPath));
        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection;
        Assert.Equal(["artwork"], restarted.Of("steam:1", Defaults));
        Assert.Equal(["uninstall"], restarted.Of("steam:2", Defaults));
        Assert.Null(restarted.AllGames);

        Directory.Delete(SettingsPath + ".tmp");
        service.Update(s => s with { QuickSelection = QuickSelectionSettings.ForAllGames(["mouse-mode"]) });
        var replaced = new SettingsService(new SettingsStore(SettingsPath)).Current.QuickSelection;
        Assert.Empty(replaced.PerGame);
        Assert.Equal(["mouse-mode"], replaced.Of("steam:1", Defaults));
    }
}
