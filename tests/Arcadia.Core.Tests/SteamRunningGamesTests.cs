using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class SteamRunningGamesTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 21, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Started.AddMinutes(5));
    private readonly FakeAppsRegistry _registry = new();

    private SteamRunningGames CreateMonitor() => new(_time, _registry);

    [Fact]
    public void A_game_with_the_running_flag_runs_since_steam_wrote_its_key()
    {
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        _registry.Apps["570"] = new FakeApp(Running: false, WrittenAt: Started.AddDays(-3));

        Assert.Equal([new RunningGame("440", Started)], CreateMonitor().Poll());
    }

    [Fact]
    public void The_running_app_id_alone_counts()
    {
        _registry.Apps["440"] = new FakeApp(Running: false, WrittenAt: Started);
        _registry.Current = "440";

        Assert.Equal([new RunningGame("440", Started)], CreateMonitor().Poll());
    }

    [Fact]
    public void Polls_in_quick_succession_look_only_at_the_games_known_to_run_and_the_running_app_id()
    {
        var monitor = CreateMonitor();
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        monitor.Poll();
        Assert.Equal(1, _registry.Enumerations);

        // A second game started meanwhile is Steam's last started one.
        _registry.Apps["570"] = new FakeApp(Running: true, WrittenAt: Started.AddMinutes(4));
        _registry.Current = "570";
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal([new RunningGame("440", Started), new RunningGame("570", Started.AddMinutes(4))], monitor.Poll());
        Assert.Equal(1, _registry.Enumerations);
    }

    [Fact]
    public void A_poll_after_a_pause_reads_every_app_again()
    {
        var monitor = CreateMonitor();
        monitor.Poll();

        // Started from Steam while Arcadia was in the background, and not Steam's last started game any more.
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        _time.Advance(SteamRunningGames.FullReadAfter + TimeSpan.FromSeconds(1));

        Assert.Equal([new RunningGame("440", Started)], monitor.Poll());
        Assert.Equal(2, _registry.Enumerations);
    }

    [Fact]
    public void A_game_ends_when_steam_clears_its_flag()
    {
        var monitor = CreateMonitor();
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        monitor.Poll();

        _registry.Apps["440"] = new FakeApp(Running: false, WrittenAt: _time.GetUtcNow());
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Empty(monitor.Poll());
    }

    [Fact]
    public void The_start_is_read_once_since_steam_may_write_the_key_again_while_the_game_runs()
    {
        var monitor = CreateMonitor();
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        monitor.Poll();

        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started.AddMinutes(30));
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal([new RunningGame("440", Started)], monitor.Poll());
    }

    [Fact]
    public void A_game_started_again_has_its_new_start()
    {
        var monitor = CreateMonitor();
        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started);
        monitor.Poll();
        _registry.Apps["440"] = new FakeApp(Running: false, WrittenAt: Started.AddMinutes(10));
        _time.Advance(TimeSpan.FromSeconds(2));
        monitor.Poll();

        _registry.Apps["440"] = new FakeApp(Running: true, WrittenAt: Started.AddMinutes(20));
        _registry.Current = "440";
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal([new RunningGame("440", Started.AddMinutes(20))], monitor.Poll());
    }

    private sealed record FakeApp(bool Running, DateTimeOffset? WrittenAt);

    private sealed class FakeAppsRegistry : ISteamAppsRegistry
    {
        public Dictionary<string, FakeApp> Apps { get; } = [];

        public string? Current { get; set; }

        public int Enumerations { get; private set; }

        public string? RunningAppId() => Current;

        public IReadOnlyList<string> AppIds()
        {
            Enumerations++;
            return [.. Apps.Keys];
        }

        public bool IsRunning(string appId) => Apps.TryGetValue(appId, out var app) && app.Running;

        public DateTimeOffset? WrittenAt(string appId) => Apps.TryGetValue(appId, out var app) ? app.WrittenAt : null;
    }
}
