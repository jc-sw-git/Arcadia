using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class LauncherRegistryTests
{
    private readonly FakeTimeProvider _time = new();

    private static Game GameOf(string launcherId, string id, bool installed = true) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    [Fact]
    public async Task Disabled_launchers_are_neither_scanned_nor_watched()
    {
        var enabled = new HashSet<string> { "epic", "gog" };
        var epicScanner = new FakeScanner(KnownLaunchers.Epic) { Games = [GameOf("epic", "a")] };
        var epicMonitor = new FakeMonitor("epic");
        var gogScanner = new FakeScanner(KnownLaunchers.Gog) { Games = [GameOf("gog", "b")] };
        var registry = new LauncherRegistry(
            [
                new FakeIntegration(KnownLaunchers.Epic) { Scanner = epicScanner, InstallMonitor = epicMonitor },
                new FakeIntegration(KnownLaunchers.Gog) { Scanner = gogScanner },
            ],
            enabled.Contains);
        var library = new LibraryService(registry);
        var installs = new InstallTracker(registry, _time);

        enabled.Remove("epic");

        Assert.Equal("gog", Assert.Single(await library.LoadAsync()).Launcher.Id);
        Assert.Null(await installs.PollAsync());
        Assert.Null(installs.Started(GameOf("epic", "a", installed: false)));
        Assert.Equal(0, epicScanner.Scans);
        Assert.Equal(0, epicMonitor.Polls);

        // Takes effect without restarting.
        enabled.Add("epic");

        Assert.Equal(["epic", "gog"], (await library.LoadAsync()).Select(l => l.Launcher.Id));
        Assert.NotNull(await installs.PollAsync());
        Assert.Equal(1, epicMonitor.Polls);
    }

    [Fact]
    public async Task Starts_games_with_the_client_of_their_enabled_launcher()
    {
        var enabled = true;
        var client = new FakeClient("epic");
        var registry = new LauncherRegistry([new FakeIntegration(KnownLaunchers.Epic) { Client = client }], _ => enabled);
        var launches = new GameLaunchService(registry);

        await launches.StartAsync(GameOf("epic", "a"));
        Assert.Equal(1, client.Starts);

        // Disabled: the fallback starts the target as it is; for demo data that is nothing.
        enabled = false;
        var stages = new List<LaunchStage>();
        await launches.StartAsync(GameOf("epic", "a"), new SynchronousProgress(stages.Add));
        Assert.Equal(1, client.Starts);
        Assert.Equal([LaunchStage.NotStartable], stages);
    }

    [Fact]
    public void Keeps_the_registration_order()
    {
        var registry = FakeIntegration.Registry(
            new FakeIntegration(KnownLaunchers.Gog),
            new FakeIntegration(KnownLaunchers.Steam),
            new FakeIntegration(FakeIntegration.LauncherOf("itch")));

        Assert.Equal(["gog", "steam", "itch"], registry.Enabled.Select(i => i.Launcher.Id));
        Assert.Empty(registry.Rejected);
    }

    [Fact]
    public void Leaves_out_a_launcher_registered_twice()
    {
        var first = new FakeIntegration(KnownLaunchers.Epic);
        var registry = FakeIntegration.Registry(first, new FakeIntegration(KnownLaunchers.Epic));

        Assert.Same(first, Assert.Single(registry.All));
        Assert.Single(registry.Rejected);
    }

    [Fact]
    public void Leaves_out_a_launcher_with_parts_of_another()
    {
        var registry = FakeIntegration.Registry(
            new FakeIntegration(KnownLaunchers.Epic) { Scanner = new FakeScanner(KnownLaunchers.Gog) },
            new FakeIntegration(KnownLaunchers.Epic) { Client = new FakeClient("gog") },
            new FakeIntegration(KnownLaunchers.Epic) { InstallMonitor = new FakeMonitor("gog") },
            new FakeIntegration(KnownLaunchers.Steam));

        Assert.Equal("steam", Assert.Single(registry.All).Launcher.Id);
        Assert.Equal(3, registry.Rejected.Count);
    }

    [Fact]
    public void Built_in_launchers_follow_the_known_order_and_add_demo_data_only_on_request()
    {
        var real = BuiltInLaunchers.Create(_time, demo: false);
        Assert.Equal(["epic"], real.Select(i => i.Launcher.Id));

        var demo = new LauncherRegistry(BuiltInLaunchers.Create(_time, demo: true), _ => true);
        Assert.Equal(KnownLaunchers.All.Select(l => l.Id), demo.All.Select(i => i.Launcher.Id));
        Assert.Empty(demo.Rejected);
        Assert.NotNull(demo.Find("epic")?.InstallMonitor);
    }

    private sealed class FakeClient(string launcherId) : ILauncherClient
    {
        public int Starts { get; private set; }

        public string? LauncherId => launcherId;

        public Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
        {
            Starts++;
            return Task.CompletedTask;
        }
    }

    // Progress<T> reports on the thread pool; the test needs the stages before it asserts.
    private sealed class SynchronousProgress(Action<LaunchStage> report) : IProgress<LaunchStage>
    {
        public void Report(LaunchStage value) => report(value);
    }
}
