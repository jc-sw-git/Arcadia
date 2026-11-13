using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-settings-").FullName;

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Library_view_survives_a_restart()
    {
        var sort = new SortOrder(SortField.LastPlayed, SortDirection.Descending);
        new SettingsService(new SettingsStore(SettingsPath)).Update(settings => settings with
        {
            LibrarySort = sort,
            PreferredPageSize = 64,
            LastTabLauncherId = "epic",
        });

        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(sort, restarted.LibrarySort);
        Assert.Equal(64, restarted.PreferredPageSize);
        Assert.Equal("epic", restarted.LastTabLauncherId);
    }

    [Fact]
    public void A_saved_file_says_its_format_and_leaves_no_temporary_file()
    {
        new SettingsService(new SettingsStore(SettingsPath)).Update(settings => settings with { KeepSisr = true });

        Assert.Contains($"\"SchemaVersion\": {AppSettings.CurrentSchemaVersion}", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void A_file_from_a_newer_Arcadia_is_kept_beside_the_one_this_version_saves()
    {
        var newer = $$"""{ "SchemaVersion": {{AppSettings.CurrentSchemaVersion + 1}}, "KeepSisr": true, "SomethingNew": 3 }""";
        File.WriteAllText(SettingsPath, newer);

        var service = new SettingsService(new SettingsStore(SettingsPath));
        service.Update(settings => settings with { PanEnabled = true });

        Assert.True(service.Current.KeepSisr);
        Assert.Equal(newer, File.ReadAllText(SettingsPath + $".v{AppSettings.CurrentSchemaVersion + 1}"));
        Assert.Contains($"\"SchemaVersion\": {AppSettings.CurrentSchemaVersion}", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void A_file_without_a_format_is_read_as_the_first_and_makes_no_copy()
    {
        File.WriteAllText(SettingsPath, """{ "KeepSisr": true }""");

        var service = new SettingsService(new SettingsStore(SettingsPath));

        Assert.True(service.Current.KeepSisr);
        Assert.Equal(1, service.Current.SchemaVersion);
        Assert.Empty(Directory.GetFiles(_directory, "settings.json.v*"));
    }

    [Fact]
    [Trait("TestCase", "T214-9")]
    public void SISR_s_old_settings_are_dropped_with_the_next_save_and_the_modes_per_game_stay()
    {
        File.WriteAllText(SettingsPath, """
            {
              "SteamControllerSupport": true,
              "LastSisrUpdateCheck": "2026-10-01T12:00:00+00:00",
              "ControllerSupportOverrides": { "epic:fortnite": false }
            }
            """);
        var service = new SettingsService(new SettingsStore(SettingsPath));

        service.Update(settings => settings with { KeepSisr = true });

        var saved = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain("SteamControllerSupport", saved);
        Assert.DoesNotContain("LastSisrUpdateCheck", saved);
        Assert.False(new SettingsService(new SettingsStore(SettingsPath)).Current.ControllerSupportOverrides["epic:fortnite"]);
    }

    [Fact]
    [Trait("TestCase", "T538-2")]
    public void Metadata_keeps_the_shared_choice_for_several_hits_from_before_until_it_gets_its_own()
    {
        File.WriteAllText(SettingsPath, """{ "Artwork": { "SeveralHits": "TakeFirst" } }""");
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.Null(service.Current.Metadata.SeveralHits);
        Assert.Equal(SeveralHits.TakeFirst, service.Current.Metadata.SeveralHitsOr(service.Current.Artwork.SeveralHits));

        service.Update(settings => settings with { Metadata = settings.Metadata with { SeveralHits = SeveralHits.Ask } });

        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current;
        Assert.Equal(SeveralHits.Ask, restarted.Metadata.SeveralHitsOr(restarted.Artwork.SeveralHits));
        Assert.Equal(SeveralHits.TakeFirst, restarted.Artwork.SeveralHits);
    }

    [Fact]
    public void Settings_from_before_the_library_view_get_its_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "PanEnabled": false }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.False(loaded.PanEnabled);
        Assert.Equal(SortOrder.Default, loaded.LibrarySort);
        Assert.Equal(PageSizes.Default, loaded.PreferredPageSize);
        Assert.Null(loaded.LastTabLauncherId);
    }

    [Fact]
    public void Unknown_view_values_fall_back_to_the_defaults_and_keep_the_rest()
    {
        // As a newer Arcadia with more sort fields or page sizes would write them.
        File.WriteAllText(SettingsPath, """
            {
              "PanEnabled": false,
              "LibrarySort": { "Field": 7, "Direction": 0 },
              "PreferredPageSize": 30,
              "LastTabLauncherId": "gog"
            }
            """);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.False(loaded.PanEnabled);
        Assert.Equal(SortOrder.Default, loaded.LibrarySort);
        Assert.Equal(PageSizes.Default, loaded.PreferredPageSize);
        Assert.Equal("gog", loaded.LastTabLauncherId);
    }

    /// <summary>T24-3: a size of an older Arcadia, or one written by hand, becomes 24 and is saved so at the start.</summary>
    [Theory]
    [InlineData("10")]
    [InlineData("25")]
    [InlineData("50")]
    [InlineData("100")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("7")]
    [InlineData("1000")]
    [Trait("TestCase", "T24-3")]
    public void A_page_size_that_no_longer_exists_becomes_24_and_is_saved_at_the_start(string saved)
    {
        File.WriteAllText(SettingsPath, $$"""{ "PreferredPageSize": {{saved}}, "LastTabLauncherId": "gog" }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(24, loaded.PreferredPageSize);
        var file = new SettingsStore(SettingsPath).Load();
        Assert.Equal((24, "gog"), (file.PreferredPageSize, file.LastTabLauncherId));
    }

    /// <summary>T24-3, failure case: a page size written as a string makes the file unreadable; it is kept aside, 24 applies.</summary>
    [Fact]
    [Trait("TestCase", "T24-3")]
    public void A_page_size_written_as_text_gives_the_defaults_and_keeps_the_file_aside()
    {
        File.WriteAllText(SettingsPath, """{ "PreferredPageSize": "25" }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(24, loaded.PreferredPageSize);
        Assert.True(File.Exists(SettingsPath + ".broken"));
    }

    /// <summary>A valid size isn't written again at the start: nothing changed, so settings.json stays untouched.</summary>
    [Fact]
    public void A_valid_page_size_is_not_saved_again_at_the_start()
    {
        File.WriteAllText(SettingsPath, """{ "PreferredPageSize": 64 }""");
        var written = File.GetLastWriteTimeUtc(SettingsPath) - TimeSpan.FromHours(1);
        File.SetLastWriteTimeUtc(SettingsPath, written);

        _ = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(written, File.GetLastWriteTimeUtc(SettingsPath));
    }

    /// <summary>A replaced page size that can't be saved at the start (read-only folder, lock) doesn't stop Arcadia.</summary>
    [Fact]
    public void A_replaced_page_size_that_cant_be_saved_at_the_start_still_applies()
    {
        File.WriteAllText(SettingsPath, """{ "PreferredPageSize": 25 }""");
        Directory.CreateDirectory(SettingsPath + ".tmp");  // the temp file can't be written now

        var service = new SettingsService(new SettingsStore(SettingsPath));

        Assert.Equal(24, service.Current.PreferredPageSize);
        Directory.Delete(SettingsPath + ".tmp");
        service.Update(settings => settings with { LastTabLauncherId = "epic" });
        Assert.Equal(24, new SettingsStore(SettingsPath).Load().PreferredPageSize);
    }

    [Fact]
    public void Mouse_mode_settings_survive_a_restart_and_start_with_the_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "PanEnabled": false }""");
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.Equal(MouseModeActivation.Manual, service.Current.MouseMode.Activation);
        Assert.Equal(3, service.Current.MouseMode.HoldSeconds);
        Assert.Equal(5, service.Current.MouseMode.PointerSpeed);
        Assert.Equal(MouseModeInGames.Allowed, service.Current.MouseMode.InGames);
        Assert.True(service.Current.MouseMode.PerGameDefaultAllowed);
        Assert.Empty(service.Current.MouseMode.GameChoices);

        service.Update(s => s with
        {
            MouseMode = (s.MouseMode with { HoldSeconds = 7, Activation = MouseModeActivation.Automatic, InGames = MouseModeInGames.PerGame, PerGameDefaultAllowed = false })
                .WithGameChoice("steam:1", true),
        });
        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.MouseMode;

        Assert.Equal(7, restarted.HoldSeconds);
        Assert.Equal(MouseModeActivation.Automatic, restarted.Activation);
        Assert.Equal(MouseModeInGames.PerGame, restarted.InGames);
        Assert.False(restarted.PerGameDefaultAllowed);
        Assert.True(restarted.GameChoiceFor("steam:1"));
    }

    [Theory]
    [InlineData("""{ "MouseMode": { "HoldSeconds": 0, "PointerSpeed": 42, "Activation": 0 } }""", MouseModeActivation.Off)]
    [InlineData("""{ "MouseMode": { "Activation": 7 } }""", MouseModeActivation.Manual)]
    [InlineData("""{ "MouseMode": null }""", MouseModeActivation.Manual)]
    [InlineData("""{ "MouseMode": { "InGames": 9, "GameChoices": null } }""", MouseModeActivation.Manual)]
    public void Broken_mouse_mode_values_fall_back_to_the_defaults_and_keep_the_rest(string json, MouseModeActivation activation)
    {
        File.WriteAllText(SettingsPath, json);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current.MouseMode;

        Assert.Equal(3, loaded.HoldSeconds);
        Assert.Equal(5, loaded.PointerSpeed);
        Assert.Equal(activation, loaded.Activation);
        Assert.Equal(MouseModeInGames.Allowed, loaded.InGames);
        Assert.NotNull(loaded.GameChoices);
    }

    [Fact]
    public void Return_with_view_survives_a_restart_and_starts_on_with_one_second()
    {
        File.WriteAllText(SettingsPath, "{}");
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.True(service.Current.ReturnWithView.Enabled);
        Assert.Equal(1, service.Current.ReturnWithView.HoldSeconds);
        Assert.Equal(MouseModeActivation.Manual, service.Current.MouseMode.Activation);
        Assert.True(service.Current.IgnoreSteamDesktopLayout);

        service.Update(s => s with { ReturnWithView = new ReturnWithViewSettings { Enabled = false, HoldSeconds = 9 } });
        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.ReturnWithView;

        Assert.False(restarted.Enabled);
        Assert.Equal(9, restarted.HoldSeconds);
    }

    [Fact]
    public void The_on_screen_keyboard_opens_at_the_bottom_until_moved_and_keeps_its_edge()
    {
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.False(service.Current.MouseMode.KeyboardAtTop);

        service.Update(s => s with { MouseMode = s.MouseMode with { KeyboardAtTop = true } });

        Assert.True(new SettingsService(new SettingsStore(SettingsPath)).Current.MouseMode.KeyboardAtTop);
    }

    [Theory]
    [InlineData("""{ "ReturnWithView": { "HoldSeconds": -1, "Enabled": false } }""", false)]
    [InlineData("""{ "ReturnWithView": { "HoldSeconds": 10 } }""", true)]
    [InlineData("""{ "ReturnWithView": { "HoldSeconds": 0, "Enabled": false } }""", false)]
    [InlineData("""{ "ReturnWithView": null }""", true)]
    public void Broken_return_with_view_values_fall_back_to_the_defaults_and_keep_the_rest(string json, bool enabled)
    {
        File.WriteAllText(SettingsPath, json);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current.ReturnWithView;

        Assert.Equal(1, loaded.HoldSeconds);
        Assert.Equal(enabled, loaded.Enabled);
    }

    [Fact]
    public void Changes_from_two_threads_at_once_are_all_saved()
    {
        var service = new SettingsService(new SettingsStore(SettingsPath));
        var games = Enumerable.Range(0, 200)
            .Select(i => new Game { Id = $"{i}", LauncherId = "epic", Title = $"Game {i}", Launch = new LaunchTarget.None() })
            .ToList();

        // The UI saves the view while Steam Controller Support saves from the thread pool.
        var pageSizes = new[] { 24, 64, 128, 64 };
        Parallel.Invoke(
            () => Parallel.ForEach(games, game => service.Update(settings => settings.WithControllerModeChoice(game.Key, false))),
            () => Parallel.For(0, 200, i => service.Update(settings => settings with { PreferredPageSize = pageSizes[i % 4], LastTabLauncherId = "steam" })));

        var saved = new SettingsStore(SettingsPath).Load();
        Assert.Equal(games.Count, saved.ControllerSupportOverrides.Count);
        Assert.Equal("steam", saved.LastTabLauncherId);
        Assert.Equal(service.Current.PreferredPageSize, saved.PreferredPageSize);
    }

    [Fact]
    public void A_failed_save_still_applies_the_change_until_the_next_save()
    {
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Directory.CreateDirectory(SettingsPath + ".tmp");  // the temp file can't be written now

        var error = Record.Exception(() => service.Update(settings => settings with { PreferredPageSize = 128 }));
        Assert.True(error is IOException or UnauthorizedAccessException, $"unexpected {error}");
        Assert.Equal(128, service.Current.PreferredPageSize);

        Directory.Delete(SettingsPath + ".tmp");
        service.Update(settings => settings with { LastTabLauncherId = "epic" });
        Assert.Equal(128, new SettingsStore(SettingsPath).Load().PreferredPageSize);
    }

    [Fact]
    public void A_settings_file_locked_at_the_start_is_not_overwritten()
    {
        File.WriteAllText(SettingsPath, """{ "PreferredPageSize": 64, "ActiveLaunchers": ["steam"] }""");
        SettingsService service;
        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            service = new SettingsService(new SettingsStore(SettingsPath));
        }

        var error = Record.Exception(() => service.Update(settings => settings with { PreferredPageSize = 128 }));

        Assert.IsType<IOException>(error);
        Assert.Equal(128, service.Current.PreferredPageSize);
        var kept = new SettingsStore(SettingsPath).Load();
        Assert.Equal(64, kept.PreferredPageSize);
        Assert.Equal(["steam"], kept.ActiveLaunchers);
    }

    [Fact]
    public void A_settings_file_locked_only_for_a_moment_is_read()
    {
        File.WriteAllText(SettingsPath, """{ "PreferredPageSize": 64 }""");
        var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // A thread of its own: a busy thread pool (all tests run in parallel) could release the lock too late.
        var release = new Thread(() =>
        {
            Thread.Sleep(20);
            locked.Dispose();
        });
        release.Start();
        var service = new SettingsService(new SettingsStore(SettingsPath));
        release.Join();

        Assert.Equal(64, service.Current.PreferredPageSize);
        service.Update(settings => settings with { PreferredPageSize = 128 });
        Assert.Equal(128, new SettingsStore(SettingsPath).Load().PreferredPageSize);
    }
}
