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
            PreferredPageSize = 50,
            LastTabLauncherId = "epic",
        });

        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.Equal(sort, restarted.LibrarySort);
        Assert.Equal(50, restarted.PreferredPageSize);
        Assert.Equal("epic", restarted.LastTabLauncherId);
    }

    [Fact]
    public void Settings_from_before_the_library_view_get_its_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "SteamControllerSupport": true }""");

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.True(loaded.SteamControllerSupport);
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
              "SteamControllerSupport": true,
              "LibrarySort": { "Field": 7, "Direction": 0 },
              "PreferredPageSize": 30,
              "LastTabLauncherId": "gog"
            }
            """);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current;

        Assert.True(loaded.SteamControllerSupport);
        Assert.Equal(SortOrder.Default, loaded.LibrarySort);
        Assert.Equal(PageSizes.Default, loaded.PreferredPageSize);
        Assert.Equal("gog", loaded.LastTabLauncherId);
    }

    [Fact]
    public void Mouse_mode_settings_survive_a_restart_and_start_with_the_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "SteamControllerSupport": true }""");
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.Equal(new MouseModeSettings(), service.Current.MouseMode);
        Assert.Equal(MouseModeActivation.Manual, service.Current.MouseMode.Activation);
        Assert.Equal(3, service.Current.MouseMode.HoldSeconds);
        Assert.Equal(5, service.Current.MouseMode.PointerSpeed);

        service.Update(s => s with { MouseMode = s.MouseMode with { HoldSeconds = 7, Activation = MouseModeActivation.Automatic } });
        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.MouseMode;

        Assert.Equal(7, restarted.HoldSeconds);
        Assert.Equal(MouseModeActivation.Automatic, restarted.Activation);
    }

    [Theory]
    [InlineData("""{ "MouseMode": { "HoldSeconds": 0, "PointerSpeed": 42, "Activation": 0 } }""", MouseModeActivation.Off)]
    [InlineData("""{ "MouseMode": { "Activation": 7 } }""", MouseModeActivation.Manual)]
    [InlineData("""{ "MouseMode": null }""", MouseModeActivation.Manual)]
    public void Broken_mouse_mode_values_fall_back_to_the_defaults_and_keep_the_rest(string json, MouseModeActivation activation)
    {
        File.WriteAllText(SettingsPath, json);

        var loaded = new SettingsService(new SettingsStore(SettingsPath)).Current.MouseMode;

        Assert.Equal(3, loaded.HoldSeconds);
        Assert.Equal(5, loaded.PointerSpeed);
        Assert.Equal(activation, loaded.Activation);
    }

    [Fact]
    public void Return_with_view_survives_a_restart_and_starts_on_with_one_second()
    {
        File.WriteAllText(SettingsPath, "{}");
        var service = new SettingsService(new SettingsStore(SettingsPath));
        Assert.True(service.Current.ReturnWithView.Enabled);
        Assert.Equal(1, service.Current.ReturnWithView.HoldSeconds);
        Assert.Equal(MouseModeActivation.Manual, service.Current.MouseMode.Activation);
        Assert.True(service.Current.MouseMode.IgnoreSteamDesktopLayout);

        service.Update(s => s with { ReturnWithView = new ReturnWithViewSettings { Enabled = false, HoldSeconds = 0 } });
        var restarted = new SettingsService(new SettingsStore(SettingsPath)).Current.ReturnWithView;

        Assert.False(restarted.Enabled);
        Assert.Equal(0, restarted.HoldSeconds);
    }

    [Theory]
    [InlineData("""{ "ReturnWithView": { "HoldSeconds": -1, "Enabled": false } }""", false)]
    [InlineData("""{ "ReturnWithView": { "HoldSeconds": 10 } }""", true)]
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
        var pageSizes = new[] { 10, 25, 50, 100 };
        Parallel.Invoke(
            () => Parallel.ForEach(games, game => service.Update(settings => settings.WithControllerSupportOverride(game, false))),
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

        var error = Record.Exception(() => service.Update(settings => settings with { PreferredPageSize = 100 }));
        Assert.True(error is IOException or UnauthorizedAccessException, $"unexpected {error}");
        Assert.Equal(100, service.Current.PreferredPageSize);

        Directory.Delete(SettingsPath + ".tmp");
        service.Update(settings => settings with { LastTabLauncherId = "epic" });
        Assert.Equal(100, new SettingsStore(SettingsPath).Load().PreferredPageSize);
    }
}
