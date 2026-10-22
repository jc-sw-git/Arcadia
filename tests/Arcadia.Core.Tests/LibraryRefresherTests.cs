using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class LibraryRefresherTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeScanner _epic = new(KnownLaunchers.Epic);
    private readonly FakeScanner _gog = new(KnownLaunchers.Gog);

    private static Game GameOf(string launcherId, string id, bool installed = true) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    private LibraryRefresher CreateRefresher() => new(new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog })), _time);

    [Fact]
    public async Task Returns_nothing_when_the_library_is_unchanged()
    {
        _epic.Games = [GameOf("epic", "a")];
        var current = await new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog })).LoadAsync();

        Assert.Null(await CreateRefresher().RefreshAsync(() => current));
    }

    [Fact]
    public async Task Returns_the_rescan_when_a_game_was_uninstalled()
    {
        _epic.Games = [GameOf("epic", "a")];
        var current = await new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog })).LoadAsync();

        _epic.Games = [GameOf("epic", "a", installed: false)];
        var scanned = await CreateRefresher().RefreshAsync(() => current);

        Assert.False(Assert.Single(scanned!.Single(l => l.Launcher.Id == "epic").Games).IsInstalled);
    }

    [Fact]
    public async Task An_active_launcher_without_games_keeps_its_place()
    {
        _epic.Games = [GameOf("epic", "a")];
        _gog.Games = [GameOf("gog", "b")];
        var current = await new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog })).LoadAsync();

        _gog.Games = [];
        var scanned = await CreateRefresher().RefreshAsync(() => current);

        Assert.NotNull(scanned);
        Assert.Equal(["epic", "gog"], scanned.Select(l => l.Launcher.Id));
        Assert.Empty(scanned[1].Games);
    }

    [Fact]
    public void A_new_tab_order_or_a_launcher_no_longer_found_is_a_change()
    {
        var epic = new LauncherLibrary(KnownLaunchers.Epic, [GameOf("epic", "a")]);
        var gog = new LauncherLibrary(KnownLaunchers.Gog, []);

        Assert.True(LibraryRefresher.HasChanged([epic, gog], [gog, epic]));
        Assert.True(LibraryRefresher.HasChanged([epic, gog], [epic, gog with { IsFound = false }]));
        Assert.True(LibraryRefresher.HasChanged([epic, gog], [epic]));
        Assert.False(LibraryRefresher.HasChanged([epic, gog], [epic, gog with { }]));
    }

    [Fact]
    public void Tells_launchers_apart_even_with_the_same_game_count()
    {
        var epic = new LauncherLibrary(KnownLaunchers.Epic, [GameOf("epic", "a")]);
        var gog = new LauncherLibrary(KnownLaunchers.Gog, [GameOf("gog", "a")]);

        Assert.True(LibraryRefresher.HasChanged([epic], [gog]));
        Assert.False(LibraryRefresher.HasChanged([epic], [epic with { Games = [.. epic.Games] }]));
    }

    [Fact]
    public void A_new_playtime_or_last_start_is_a_change_since_the_library_sorts_by_it()
    {
        var before = new LauncherLibrary(KnownLaunchers.Steam, [GameOf("steam", "620") with { Playtime = TimeSpan.FromHours(2), LastPlayedAt = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero) }]);
        var played = before.Games[0] with { Playtime = TimeSpan.FromHours(3), LastPlayedAt = new DateTimeOffset(2026, 10, 21, 18, 0, 0, TimeSpan.Zero) };

        Assert.True(LibraryRefresher.HasChanged([before], [before with { Games = [played] }]));
        Assert.True(LibraryRefresher.HasChanged([before], [before with { Games = [played with { Playtime = before.Games[0].Playtime }] }]));
        Assert.False(LibraryRefresher.HasChanged([before], [before with { Games = [before.Games[0] with { }] }]));
    }

    [Fact]
    public async Task Throttles_unforced_rescans()
    {
        var refresher = CreateRefresher();
        IReadOnlyList<LauncherLibrary> empty = [];
        _epic.Games = [GameOf("epic", "a")];

        Assert.NotNull(await refresher.RefreshAsync(() => empty));
        Assert.Equal(1, _epic.Scans);

        _time.Advance(LibraryRefresher.Throttle - TimeSpan.FromSeconds(1));
        Assert.Null(await refresher.RefreshAsync(() => empty));
        Assert.Equal(1, _epic.Scans);

        Assert.NotNull(await refresher.RefreshAsync(() => empty, force: true));
        Assert.Equal(2, _epic.Scans);

        _time.Advance(LibraryRefresher.Throttle);
        Assert.NotNull(await refresher.RefreshAsync(() => empty));
        Assert.Equal(3, _epic.Scans);
    }

    [Fact]
    public async Task Skips_an_unforced_rescan_while_the_previous_one_runs()
    {
        var refresher = CreateRefresher();
        IReadOnlyList<LauncherLibrary> empty = [];
        _epic.Games = [GameOf("epic", "a")];
        _epic.Gate = new TaskCompletionSource();

        var first = refresher.RefreshAsync(() => empty, force: true);
        _time.Advance(LibraryRefresher.Throttle);
        Assert.Null(await refresher.RefreshAsync(() => empty));

        _epic.Gate.SetResult();
        Assert.NotNull(await first);
        Assert.Equal(1, _epic.Scans);
    }

    [Fact]
    public async Task A_forced_rescan_waits_for_the_running_one_and_scans_again()
    {
        // E.g. a launcher switched on while a rescan runs that started before (#136).
        var refresher = CreateRefresher();
        IReadOnlyList<LauncherLibrary> empty = [];
        _epic.Games = [GameOf("epic", "a")];
        _epic.Gate = new TaskCompletionSource();

        var first = refresher.RefreshAsync(() => empty, force: true);
        var second = refresher.RefreshAsync(() => empty, force: true);
        Assert.False(second.IsCompleted);

        _epic.Gate.SetResult();
        Assert.NotNull(await first);
        Assert.NotNull(await second);
        Assert.Equal(2, _epic.Scans);
    }
}
