using System.ComponentModel;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class GameLaunchTests
{
    private static Game GameOf(string launcherId, LaunchTarget? launch = null, string? installDirectory = null) => new()
    {
        Id = "a",
        LauncherId = launcherId,
        Title = "A",
        Launch = launch ?? new LaunchTarget.None(),
        IsInstalled = true,
        InstallDirectory = installDirectory,
    };

    private static GameLaunchService ServiceWith(ILauncherClient client, List<string>? log = null) =>
        new(FakeIntegration.Registry(new FakeIntegration(FakeIntegration.LauncherOf("x")) { Client = client }))
        {
            Log = log is null ? null : log.Add,
        };

    [Fact]
    public async Task An_unexpected_error_is_logged_and_reported_instead_of_thrown()
    {
        var log = new List<string>();
        var service = ServiceWith(new ThrowingClient(new NullReferenceException("boom")), log);

        var result = await service.TryStartAsync(GameOf("x"));

        Assert.Equal(LaunchOutcome.FailedUnexpectedly, result.Outcome);
        Assert.IsType<NullReferenceException>(result.Error);
        // The whole exception, stack trace included.
        Assert.Contains(log, line => line.Contains("NullReferenceException") && line.Contains(nameof(ThrowingClient)));
    }

    [Fact]
    public async Task A_known_failure_keeps_its_message_for_the_user()
    {
        var service = ServiceWith(new ThrowingClient(new Win32Exception(2, "Datei nicht gefunden")));

        var result = await service.TryStartAsync(GameOf("x"));

        Assert.Equal(LaunchOutcome.Failed, result.Outcome);
        Assert.Equal("Datei nicht gefunden", result.Error?.Message);
    }

    [Fact]
    public async Task A_failing_preparation_is_caught_as_well()
    {
        var client = new ThrowingClient(null);
        var service = ServiceWith(client);

        var result = await service.TryStartAsync(GameOf("x"), _ => throw new UnauthorizedAccessException("verweigert"));

        Assert.Equal(LaunchOutcome.Failed, result.Outcome);
        Assert.Equal(0, client.Starts);
    }

    [Fact]
    public async Task Cancelling_is_silent()
    {
        var service = ServiceWith(new ThrowingClient(new TaskCanceledException()));

        var result = await service.TryStartAsync(GameOf("x"));

        Assert.Equal(LaunchOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task After_a_failure_the_next_start_runs()
    {
        var client = new ThrowingClient(new InvalidCastException());
        var service = ServiceWith(client);

        await service.TryStartAsync(GameOf("x"));
        client.Error = null;
        var result = await service.TryStartAsync(GameOf("x"));

        Assert.Equal(LaunchOutcome.Requested, result.Outcome);
        Assert.Equal(2, client.Starts);
    }

    [Fact]
    public async Task Only_one_start_runs_at_a_time()
    {
        var client = new ThrowingClient(null) { Gate = new TaskCompletionSource() };
        var service = ServiceWith(client);

        var first = service.TryStartAsync(GameOf("x"));
        var second = await service.TryStartAsync(GameOf("x"));
        client.Gate.SetResult();

        Assert.Equal(LaunchOutcome.AlreadyLaunching, second.Outcome);
        Assert.Equal(LaunchOutcome.Requested, (await first).Outcome);
        Assert.Equal(1, client.Starts);
    }

    [Fact]
    public async Task Epic_reports_a_launcher_that_does_not_come_up_and_still_asks_once_more()
    {
        var time = new FakeTimeProvider();
        var processes = new FakeEpicProcesses { LauncherRunning = false, LauncherReady = false };
        var client = new EpicLauncherClient(time, processes);
        var stages = new List<LaunchStage>();
        var directory = Directory.CreateTempSubdirectory("arcadia-epic-").FullName;
        try
        {
            var start = client.StartAsync(GameOf("epic", new LaunchTarget.ProtocolUri("com.epicgames.launcher://apps/a?action=launch"), directory), new SyncProgress(stages.Add));
            await RunToEnd(start, time);

            Assert.Equal([LaunchStage.StartingLauncher, LaunchStage.LauncherNotReady, LaunchStage.NotConfirmed], stages);
            Assert.Equal(1, processes.LauncherStarts);
            Assert.Equal(2, processes.Requests);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task Epic_reports_ready_when_the_launcher_comes_up()
    {
        var time = new FakeTimeProvider();
        var processes = new FakeEpicProcesses { LauncherRunning = false, LauncherReady = true, GameRunning = true };
        var client = new EpicLauncherClient(time, processes);
        var stages = new List<LaunchStage>();
        var directory = Directory.CreateTempSubdirectory("arcadia-epic-").FullName;
        try
        {
            var start = client.StartAsync(GameOf("epic", new LaunchTarget.ProtocolUri("com.epicgames.launcher://apps/a?action=launch"), directory), new SyncProgress(stages.Add));
            await RunToEnd(start, time);

            Assert.Equal([LaunchStage.StartingLauncher, LaunchStage.LauncherReady, LaunchStage.Starting, LaunchStage.Running], stages);
            Assert.Equal(1, processes.Requests);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    /// <summary>Advances fake time until <paramref name="task"/> is done (bounded, so a bug can't hang the test).</summary>
    private static async Task RunToEnd(Task task, FakeTimeProvider time)
    {
        for (var i = 0; i < 2_000 && !task.IsCompleted; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1);
        }

        await task;
    }

    /// <summary>Reports synchronously; <see cref="Progress{T}"/> would post to the thread pool.</summary>
    private sealed class SyncProgress(Action<LaunchStage> report) : IProgress<LaunchStage>
    {
        public void Report(LaunchStage value) => report(value);
    }

    private sealed class ThrowingClient(Exception? error) : ILauncherClient
    {
        public Exception? Error { get; set; } = error;

        public TaskCompletionSource? Gate { get; init; }

        public int Starts { get; private set; }

        public string? LauncherId => "x";

        public async Task StartAsync(Game game, IProgress<LaunchStage>? progress = null, CancellationToken cancellationToken = default)
        {
            Starts++;
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (Error is { } error)
            {
                throw error;
            }
        }
    }

    private sealed class FakeEpicProcesses : IEpicLauncherProcesses
    {
        public bool LauncherRunning { get; init; }

        public bool LauncherReady { get; init; }

        public bool GameRunning { get; init; }

        public int LauncherStarts { get; private set; }

        public int Requests { get; private set; }

        public bool IsLauncherRunning() => LauncherRunning;

        public bool IsLauncherReady() => LauncherReady;

        public string? FindLauncherExecutable() => "EpicGamesLauncher.exe";

        public void StartLauncherSilently(string executable) => LauncherStarts++;

        public void Request(LaunchTarget target) => Requests++;

        public bool AnyRunningFrom(string directory) => GameRunning;
    }
}
