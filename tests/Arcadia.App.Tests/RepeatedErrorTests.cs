using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Arcadia.App.Services;
using Arcadia.ControllerSupport.Pan;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #399 as the player sees it: an unexpected error that repeats is toasted once per streak and again after 10 quiet
/// minutes; <c>arcadia.log</c> gets the first error with its stack and at most one line a minute with the count. Pan is
/// the real <see cref="PanClient"/> wired as in <c>App.axaml.cs</c>, over a transport whose look at Pan throws.
/// "Heartbeat an Pan" can't be planted: <c>SendHeartbeatAsync</c> catches every error of its request itself, so the
/// connection keeper ("Verbindung zu Pan halten", every 2 s) stands in for the steady error of T399-1, T399-3, T399-7.
/// </summary>
public sealed partial class RepeatedErrorTests
{
    private const string KeeperContext = "Verbindung zu Pan halten";

    [AvaloniaFact]
    [Trait("TestCase", "T399-1")]
    public async Task An_error_that_comes_back_after_ten_quiet_minutes_is_toasted_and_logged_with_its_stack_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var token = Token();
        var transport = new BrokenLook(token);
        var clock = new KeeperClock(arcadia.Time);
        var toasts = ToastsOf(arcadia, KeeperContext);
        await using var pan = StartPan(arcadia, clock, transport);

        await clock.KeeperWaitingAsync();
        Assert.Single(toasts);
        Assert.Equal(1, FirstErrorLines(token));

        transport.Failing = false;
        await RunKeeperAsync(arcadia, clock, PanClient.Retry * 2);
        var quietSince = arcadia.Time.GetTimestamp();
        await RunKeeperAsync(arcadia, clock, TimeSpan.FromMinutes(10));
        Assert.True(arcadia.Time.GetElapsedTime(quietSince) >= TimeSpan.FromMinutes(10));

        transport.Failing = true;
        await RunKeeperAsync(arcadia, clock, PanClient.Retry);

        Assert.Equal(2, toasts.Count);
        Assert.All(toasts, text => Assert.Equal($"Unerwarteter Fehler ({KeeperContext}) – Details stehen in arcadia.log.", text));
        Assert.Equal(2, FirstErrorLines(token));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T399-3")]
    public async Task A_steady_error_for_an_hour_is_toasted_once_and_logged_at_most_once_a_minute_with_every_repeat_counted()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var token = Token();
        var transport = new BrokenLook(token);
        var clock = new KeeperClock(arcadia.Time);
        var toasts = ToastsOf(arcadia, KeeperContext);
        await using var pan = StartPan(arcadia, clock, transport);

        await clock.KeeperWaitingAsync();
        await RunKeeperAsync(arcadia, clock, TimeSpan.FromHours(1));

        Assert.Single(toasts);
        Assert.Equal(1, FirstErrorLines(token));
        var counts = SummaryCounts(KeeperContext, token);
        Assert.InRange(counts.Count, 55, 60);
        // The repeats of the last minute are still pending: no timer writes them (T399-5).
        Assert.InRange(transport.Failures - 1 - counts.Sum(), 0, (int)(TimeSpan.FromMinutes(1) / PanClient.Retry) + 1);
        Assert.True(transport.Failures >= TimeSpan.FromHours(1) / PanClient.Retry, $"nur {transport.Failures} Fehler");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T399-7")]
    public async Task A_second_source_failing_during_a_steady_error_gets_its_own_toast_and_its_own_lines()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var token = Token();
        var transport = new BrokenLook(token);
        var clock = new KeeperClock(arcadia.Time);
        var panToasts = ToastsOf(arcadia, KeeperContext);
        var controllerToasts = ToastsOf(arcadia, "Controller-Eingabe");
        await using var pan = StartPan(arcadia, clock, transport);
        await clock.KeeperWaitingAsync();
        await RunKeeperAsync(arcadia, clock, TimeSpan.FromMinutes(2));
        Assert.Single(panToasts);

        // The second source: a handler of the controllers' poll breaks (the same toast as a broken SDL handler).
        var controllerToken = Token();
        arcadia.StartControllers();
        arcadia.BreakAPollHandler(controllerToken);
        for (var poll = 0; poll < 200; poll++)
        {
            arcadia.PollControllers();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Unerwarteter Fehler (Controller-Eingabe) – Details stehen in arcadia.log."], controllerToasts);
        Assert.Single(panToasts);
        Assert.Equal(1, FirstErrorLines(controllerToken, "Controller-Eingabe"));

        await RunKeeperAsync(arcadia, clock, TimeSpan.FromMinutes(2));
        Assert.Single(panToasts);
        Assert.Single(controllerToasts);
        Assert.NotEmpty(SummaryCounts(KeeperContext, token));
        Assert.DoesNotContain(SummaryLines(token), line => line.Contains("Controller-Eingabe", StringComparison.Ordinal));
    }

    private static string Token() => $"T399-{Guid.NewGuid():N}";

    private static PanClient StartPan(ArcadiaDriver arcadia, KeeperClock clock, BrokenLook transport)
    {
        // As App.axaml.cs wires it.
        var pan = new PanClient(transport, clock, AppLog.Write) { Unexpected = AppErrors.ReportRepeating };
        pan.Start();
        return pan;
    }

    /// <summary>The keeper's looks for <paramref name="span"/> of fake time, one retry at a time.</summary>
    private static async Task RunKeeperAsync(ArcadiaDriver arcadia, KeeperClock clock, TimeSpan span)
    {
        var start = arcadia.Time.GetTimestamp();
        while (arcadia.Time.GetElapsedTime(start) < span)
        {
            var waits = clock.KeeperWaits;
            arcadia.Time.Advance(PanClient.Retry);
            await clock.KeeperWaitingAsync(after: waits);
        }
    }

    /// <summary>What the toast showed for <paramref name="context"/>, in order.</summary>
    private static List<string> ToastsOf(ArcadiaDriver arcadia, string context)
    {
        var shown = new List<string>();
        arcadia.Main.Toast.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(arcadia.Main.Toast.Text) && arcadia.Main.Toast.Text is { } text && text.Contains($"({context})", StringComparison.Ordinal))
            {
                shown.Add(text);
            }
        };
        return shown;
    }

    private static string[] LogLines() => ArcadiaDriver.Log.Split('\n');

    private static int FirstErrorLines(string token, string context = KeeperContext) =>
        LogLines().Count(line => line.Contains($"Unerwarteter Fehler ({context}): System.InvalidOperationException: {token}", StringComparison.Ordinal));

    private static List<string> SummaryLines(string token) =>
        [.. LogLines().Where(line => line.Contains(" wiederholt: ", StringComparison.Ordinal) && line.Contains(token, StringComparison.Ordinal))];

    private static List<int> SummaryCounts(string context, string token) =>
        [.. SummaryLines(token).Select(line => SummaryLine().Match(line)).Where(match => match.Success && match.Groups[1].Value == context)
            .Select(match => int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))];

    [GeneratedRegex(@"Unerwarteter Fehler \(([^)]*)\) wiederholt: (\d+)× seit der letzten Zeile")]
    private static partial Regex SummaryLine();

    /// <summary>Pan isn't set up; while <see cref="Failing"/>, the look at it throws what nobody expected.</summary>
    private sealed class BrokenLook(string message) : IPanTransport
    {
        private int _failures;

        public volatile bool Failing = true;

        public int Failures => Volatile.Read(ref _failures);

        public (string Reason, bool Permanent)? Unavailable()
        {
            if (Failing)
            {
                Interlocked.Increment(ref _failures);
                throw new InvalidOperationException(message);
            }

            return ("Pan ist nicht eingerichtet", false);
        }

        public Task<PanLink> ConnectAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("nicht erwartet");
    }

    /// <summary>
    /// The fake clock, counting the keeper's waits for its next look: the keeper reports before it waits, so a new wait
    /// means its look, log lines and toast are done (the heartbeat's waits are shorter and not counted).
    /// </summary>
    private sealed class KeeperClock(TimeProvider inner) : TimeProvider
    {
        private int _keeperWaits;

        public int KeeperWaits => Volatile.Read(ref _keeperWaits);

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override long GetTimestamp() => inner.GetTimestamp();

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == PanClient.Retry)
            {
                Interlocked.Increment(ref _keeperWaits);
            }

            return timer;
        }

        /// <summary>Waits (real time, at most 10 s) for the keeper's next wait, then lets the UI thread show its toast.</summary>
        public async Task KeeperWaitingAsync(int after = 0)
        {
            for (var spin = 0; KeeperWaits <= after; spin++)
            {
                Assert.True(spin < 10_000, "der Verbindungshalter wartet nicht mehr auf seinen nächsten Blick");
                await Task.Delay(1);
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
