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

        Assert.False(Assert.Single(Assert.Single(scanned!).Games).IsInstalled);
    }

    [Fact]
    public async Task Returns_the_rescan_when_a_launcher_is_gone()
    {
        _epic.Games = [GameOf("epic", "a")];
        _gog.Games = [GameOf("gog", "b")];
        var current = await new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog })).LoadAsync();

        _gog.Games = [];
        var scanned = await CreateRefresher().RefreshAsync(() => current);

        Assert.Equal("epic", Assert.Single(scanned!).Launcher.Id);
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
    public async Task Skips_a_rescan_while_the_previous_one_runs()
    {
        var refresher = CreateRefresher();
        IReadOnlyList<LauncherLibrary> empty = [];
        _epic.Games = [GameOf("epic", "a")];
        _epic.Gate = new TaskCompletionSource();

        var first = refresher.RefreshAsync(() => empty, force: true);
        Assert.Null(await refresher.RefreshAsync(() => empty, force: true));

        _epic.Gate.SetResult();
        Assert.NotNull(await first);
        Assert.Equal(1, _epic.Scans);
    }
}
