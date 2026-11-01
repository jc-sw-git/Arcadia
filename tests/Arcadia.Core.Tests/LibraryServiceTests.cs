using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class LibraryServiceTests
{
    private readonly FakeTimeProvider _time = new();

    // Ignores its token, as a scanner stuck in a file read on a share that went away would.
    private readonly FakeScanner _epic = new(KnownLaunchers.Epic) { Gate = new TaskCompletionSource() };
    private readonly FakeScanner _gog = new(KnownLaunchers.Gog) { Games = [GameOf("gog", "chess")] };

    private static Game GameOf(string launcherId, string id) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = true,
    };

    private readonly List<string> _log = [];

    private LibraryService CreateService() => new(FakeIntegration.Registry(
        new FakeIntegration(KnownLaunchers.Epic) { Scanner = _epic },
        new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog }), _time) { Log = _log.Add };

    [Fact]
    public async Task A_scan_that_hangs_ends_at_the_deadline_and_the_others_load()
    {
        var service = CreateService();

        var load = service.LoadAsync();
        await WhenScanning();
        _time.Advance(LibraryService.ScanDeadline);
        var libraries = await load;

        Assert.Empty(libraries.Single(l => l.Launcher.Id == "epic").Games);
        Assert.True(libraries.Single(l => l.Launcher.Id == "epic").ScanFailed);
        Assert.Equal(["chess"], libraries.Single(l => l.Launcher.Id == "gog").Games.Select(g => g.Id));
        Assert.Null(service.ScannedAt("epic"));
        Assert.Contains("abgebrochen", Assert.Single(_log));
        _epic.Gate!.SetResult();
    }

    [Fact]
    public async Task Cancelling_the_load_still_cancels_it()
    {
        using var cancel = new CancellationTokenSource();
        var load = CreateService().LoadAsync(cancel.Token);
        await WhenScanning();

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        _epic.Gate!.SetResult();
    }

    // The deadline runs on fake time: both scans must have started (after the launchers were located) before it moves.
    private async Task WhenScanning()
    {
        Assert.True(SpinWait.SpinUntil(() => _epic.Scans == 1 && _gog.Scans == 1, TimeSpan.FromSeconds(10)));
        await Task.Delay(100);
    }
}
