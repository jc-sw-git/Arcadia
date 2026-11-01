using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;

namespace Arcadia.Core.Tests;

public sealed class LauncherSelectionTests : IDisposable
{
    private static readonly string[] Registered = ["steam", "epic", "gog", "xbox"];

    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-launchers-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static bool Installed(string id) => id != "xbox";

    private static Game GameOf(string launcherId, string id) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = true,
    };

    [Fact]
    public void Without_a_choice_every_launcher_found_is_active_in_registered_order()
    {
        Assert.Equal(["steam", "epic", "gog"], LauncherSelection.Active(Registered, null, Installed));
        Assert.Equal(["xbox"], LauncherSelection.Inactive(Registered, ["steam", "epic", "gog"], Installed));
    }

    [Fact]
    public void A_choice_decides_which_are_active_and_their_order()
    {
        Assert.Equal(["gog", "steam"], LauncherSelection.Active(Registered, ["gog", "steam"], Installed));

        // Not active: first the installed ones, then those not found.
        Assert.Equal(["epic", "xbox"], LauncherSelection.Inactive(Registered, ["gog", "steam"], Installed));
    }

    [Fact]
    public void A_chosen_launcher_no_longer_found_stays_active_and_an_unknown_or_doubled_one_is_left_out()
    {
        // A plugin that is gone, or a list edited by hand.
        Assert.Equal(["xbox", "steam"], LauncherSelection.Active(Registered, ["xbox", "itch", "steam", "xbox"], Installed));
    }

    [Fact]
    public void Placing_moves_switches_on_at_the_end_and_switches_off()
    {
        IReadOnlyList<string> active = ["steam", "epic", "gog"];

        Assert.Equal(["epic", "steam", "gog"], LauncherSelection.Placed(active, "steam", 1));
        Assert.Equal(["steam", "gog", "epic"], LauncherSelection.Placed(active, "epic", 99));
        Assert.Equal(["steam", "gog"], LauncherSelection.Placed(active, "epic", null));
        Assert.Equal(["steam", "epic", "gog", "xbox"], LauncherSelection.Placed(active, "xbox", active.Count));
        Assert.Equal(["xbox", "steam", "epic", "gog"], LauncherSelection.Placed(active, "xbox", -1));
    }

    [Fact]
    public async Task The_registry_follows_the_choice_at_once_and_counts_launchers_as_found_until_it_looked()
    {
        IReadOnlyList<string>? chosen = null;
        var xbox = new FakeIntegration(KnownLaunchers.Xbox) { Location = null };
        var registry = new LauncherRegistry(
            [new FakeIntegration(KnownLaunchers.Steam), new FakeIntegration(KnownLaunchers.Epic), xbox],
            () => chosen);

        // Before it looked, nothing is left out.
        Assert.True(registry.IsFound("xbox"));
        Assert.Equal(["steam", "epic", "xbox"], registry.ActiveIds);

        await Task.Run(registry.Locate);
        Assert.False(registry.IsFound("xbox"));
        Assert.Null(registry.LocationOf("xbox"));
        Assert.Equal(@"C:\Launchers\steam", registry.LocationOf("steam"));
        Assert.Equal(["steam", "epic"], registry.Enabled.Select(i => i.Launcher.Id));
        Assert.Equal(["xbox"], registry.InactiveIds);

        chosen = ["epic"];
        Assert.Equal(["epic"], registry.ActiveIds);
        Assert.Null(registry.Find("steam"));
        Assert.NotNull(registry.Registered("steam"));
        Assert.Equal(["steam", "xbox"], registry.InactiveIds);
    }

    [Fact]
    public void A_launcher_whose_search_fails_counts_as_not_found()
    {
        var registry = FakeIntegration.Registry(new ThrowingLocate(KnownLaunchers.Gog), new FakeIntegration(KnownLaunchers.Steam));

        registry.Locate();

        Assert.False(registry.IsFound("gog"));
        Assert.Equal(["steam"], registry.ActiveIds);
    }

    [Fact]
    public async Task Every_active_launcher_keeps_a_library_also_without_games_or_when_not_found()
    {
        var steam = new FakeIntegration(KnownLaunchers.Steam) { Scanner = new FakeScanner(KnownLaunchers.Steam) { Games = [GameOf("steam", "620")] } };
        var gogScanner = new FakeScanner(KnownLaunchers.Gog);
        var gog = new FakeIntegration(KnownLaunchers.Gog) { Scanner = gogScanner };
        var xboxScanner = new FakeScanner(KnownLaunchers.Xbox);
        var xbox = new FakeIntegration(KnownLaunchers.Xbox) { Scanner = xboxScanner, Location = null };
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 20, 0, 0, TimeSpan.Zero));
        var library = new LibraryService(new LauncherRegistry([steam, gog, xbox], () => ["xbox", "gog", "steam"]), time);

        var libraries = await library.LoadAsync();

        Assert.Equal(["xbox", "gog", "steam"], libraries.Select(l => l.Launcher.Id));
        Assert.Equal([false, true, true], libraries.Select(l => l.IsFound));
        Assert.Empty(libraries[1].Games);
        Assert.Single(libraries[2].Games);

        // A launcher that isn't installed has nothing to scan.
        Assert.Equal(0, xboxScanner.Scans);
        Assert.Null(library.ScannedAt("xbox"));
        Assert.Equal(time.GetUtcNow(), library.ScannedAt("gog"));
    }

    [Fact]
    public void The_choice_survives_a_restart_and_is_null_before_the_first()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, """{ "SteamControllerSupport": true }""");
        var service = new SettingsService(new SettingsStore(path));
        Assert.Null(service.Current.ActiveLaunchers);

        service.Update(s => s with { ActiveLaunchers = ["epic", "steam"] });

        Assert.Equal(["epic", "steam"], new SettingsService(new SettingsStore(path)).Current.ActiveLaunchers);
    }

    private sealed class ThrowingLocate(LauncherInfo launcher) : ILauncherIntegration
    {
        public LauncherInfo Launcher => launcher;

        public ILauncherScanner Scanner { get; } = new FakeScanner(launcher);

        public ILauncherClient? Client => null;

        public IInstallMonitor? InstallMonitor => null;

        public IRunningGamesMonitor? RunningGames => null;

        public IReadOnlyCollection<string> ProgramNames => [];

        public string? Locate() => throw new UnauthorizedAccessException("registry locked");
    }
}
