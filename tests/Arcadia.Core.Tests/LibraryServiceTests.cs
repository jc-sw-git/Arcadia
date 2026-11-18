using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public sealed class LibraryServiceTests
{
    private readonly TimerWatchingTimeProvider _time = new();

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
        var libraries = await PastTheDeadline(load);

        Assert.Empty(libraries.Single(l => l.Launcher.Id == "epic").Games);
        Assert.True(libraries.Single(l => l.Launcher.Id == "epic").ScanFailed);
        Assert.Equal(["chess"], libraries.Single(l => l.Launcher.Id == "gog").Games.Select(g => g.Id));
        Assert.Null(service.ScannedAt("epic"));
        Assert.Contains("abgebrochen", Assert.Single(_log));
        _epic.Gate!.SetResult();
    }

    [Fact]
    [Trait("TestCase", "T637-1")]
    public async Task A_load_never_waits_for_the_thread_that_started_it()
    {
        // At start the UI thread builds the window for a second or more; a scan that needed it in between waited that long.
        var service = new LibraryService(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Gog) { Scanner = _gog }));
        Task<IReadOnlyList<LauncherLibrary>> all;
        Task<LauncherLibrary?> one;
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new BusyThread());
            all = service.LoadAsync();
            one = service.LoadAsync(KnownLaunchers.Gog.Id);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        // Times out (TimeoutException) where a continuation waits for the busy thread.
        var libraries = await all.WaitAsync(TimeSpan.FromSeconds(10));
        var library = await one.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(["chess"], libraries.Single().Games.Select(g => g.Id));
        Assert.Equal(["chess"], library!.Games.Select(g => g.Id));
    }

    /// <summary>A thread that never gets to what is posted to it, as the UI thread while it builds the window.</summary>
    private sealed class BusyThread : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
        }
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

    // Polled without blocking a thread: on a busy runner the scans need the thread pool too.
    private async Task WhenScanning()
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!(_epic.Scans == 1 && _gog.Scans == 1))
        {
            Assert.True(DateTime.UtcNow < until, "Die Scans haben nicht angefangen.");
            await Task.Delay(10);
        }
    }

    // The deadline runs on fake time, and its waits begin only around the scans: each scan's expiry, and the wait for
    // the scan that hangs (the one that answered needs none). The clock moves once, when all three exist.
    private async Task<T> PastTheDeadline<T>(Task<T> load)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (_time.TimersCreated(LibraryService.ScanDeadline) < 3)
        {
            Assert.True(DateTime.UtcNow < until, "Die Frist hat nicht angefangen.");
            await Task.Delay(10);
        }

        Assert.False(load.IsCompleted);
        _time.Advance(LibraryService.ScanDeadline);
        return await load.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
