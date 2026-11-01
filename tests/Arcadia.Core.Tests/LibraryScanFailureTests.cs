using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

/// <summary>A launcher whose scan throws says so instead of looking empty (#369).</summary>
public sealed class LibraryScanFailureTests
{
    private readonly FakeScanner _epic = new(KnownLaunchers.Epic);
    private readonly FakeScanner _gog = new(KnownLaunchers.Gog);

    private LibraryService CreateService() =>
        new(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic }, new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog }));

    private static Game GameOf(string launcherId, string id) => new() { Id = id, LauncherId = launcherId, Title = id, Launch = new LaunchTarget.None() };

    [Fact]
    public async Task A_throwing_scanner_yields_a_failed_library_and_the_others_are_read()
    {
        _epic.Failure = new IOException("locked");
        _gog.Games = [GameOf("gog", "b")];

        var libraries = await CreateService().LoadAsync();

        var epic = libraries.Single(l => l.Launcher.Id == "epic");
        Assert.True(epic.ScanFailed);
        Assert.True(epic.IsFound);
        Assert.Empty(epic.Games);
        Assert.False(libraries.Single(l => l.Launcher.Id == "gog").ScanFailed);
    }

    [Fact]
    public async Task A_failed_rescan_keeps_the_games_read_before()
    {
        var service = CreateService();
        _epic.Games = [GameOf("epic", "a")];
        await service.LoadAsync();

        _epic.Failure = new UnauthorizedAccessException("locked");
        var failed = await service.LoadAsync("epic");

        Assert.True(failed!.ScanFailed);
        Assert.Equal(["a"], failed.Games.Select(g => g.Id));

        _epic.Failure = null;
        var again = await service.LoadAsync("epic");
        Assert.False(again!.ScanFailed);
    }

    [Fact]
    public async Task A_cancelled_scan_still_cancels()
    {
        _epic.Failure = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().LoadAsync());
    }

    [Fact]
    public void A_scan_that_fails_or_recovers_counts_as_a_change()
    {
        var games = new[] { GameOf("epic", "a") };
        var read = new LauncherLibrary(KnownLaunchers.Epic, games);

        Assert.True(LibraryRefresher.HasChanged([read], [read with { ScanFailed = true }]));
        Assert.True(LibraryRefresher.HasChanged([read with { ScanFailed = true }], [read]));
    }
}
