using System.Diagnostics;
using Arcadia.App.Input;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Tests.Artwork;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

sealed class GcListener : System.Diagnostics.Tracing.EventListener
{
    public readonly System.Collections.Concurrent.ConcurrentQueue<string> Seen = new();
    protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Informational, (System.Diagnostics.Tracing.EventKeywords)0x1);
    }
    protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
    {
        if (e.EventName is "GCStart_V2" or "GCStart") Seen.Enqueue($"{e.EventName} depth {e.Payload![1]} reason {e.Payload[2]} type {e.Payload[3]}");
    }
}

public sealed class ZzStall
{
    [AvaloniaFact]
    public async Task Stall()
    {
        var games = Enumerable.Range(1, 40).Select(i => WaitingListTests.GameOf($"g{i}", $"Game {i}")).ToArray();
        var images = new FakeImageServer();
        using var first = await ArcadiaWindow.ShowAsync([LauncherSpec.With(WaitingListTests.Other, games)], WaitingListTests.Settings, imageServer: images);
        var entries = new List<string>();
        for (var i = 1; i <= 40; i++)
        {
            var ids = Enumerable.Range(0, 8).Select(k => 10000 + i * 10 + k).ToList();
            foreach (var id in ids.Take(3))
            {
                var s = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                first.Arcadia.SteamGridDb.Cover("game", s, $"https://cdn.example/grid/{s}.png", 600, 900);
                images.Serve($"https://cdn.example/grid/{s}.png", TestImages.Png(600, 900));
            }

            var hits = string.Join(", ", ids.Select(id => $$"""{ "id": {{id}}, "name": "Game {{i}}" }"""));
            entries.Add($$""" "other:g{{i}}": { "state": "Waiting", "position": {{i}}, "hits": [ {{hits}} ] } """);
        }

        Directory.CreateDirectory(Path.Combine(first.Arcadia.Folder, "artwork"));
        await File.WriteAllTextAsync(Path.Combine(first.Arcadia.Folder, "artwork", NameMatchStore.FileName), "{" + string.Join(",", entries) + "}");
        using var many = await first.RestartAsync();
        await many.Arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        await WaitingListTests.SettledAsync(many.Arcadia);
        many.Arcadia.SteamGridDb.Delay = TimeSpan.FromMilliseconds(80);
        var list = WaitingListTests.OpenList(many.Arcadia);
        many.Settle();

        if (Environment.GetEnvironmentVariable("ARC_LIB") == "1") { many.Arcadia.Main.HandleGamepad(UiAction.Back); many.Settle(); }
        using var gcl = new GcListener();
        if (Environment.GetEnvironmentVariable("ARC_MODE") == "hold")
        {
            var hlog = new System.Text.StringBuilder();
            var done = new List<double>();
            for (var run = 0; run < 6; run++)
            {
                var gc0 = GC.GetTotalPauseDuration();
                var tasks = new List<Task<double>>();
                var start = Stopwatch.StartNew();
                for (var k = 0; k < 15; k++)
                {
                    var tcs = new TaskCompletionSource<double>();
                    var posted = Stopwatch.StartNew();
                    await Task.Run(() => Dispatcher.UIThread.Post(() =>
                    {
                        many.Arcadia.Main.HandleGamepad(UiAction.Down, repeat: k > 0);
                        // like the app's latency: until the next frame is done
                        many.Window.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(() => tcs.SetResult(posted.Elapsed.TotalMilliseconds), DispatcherPriority.Background));
                    }));
                    tasks.Add(tcs.Task);
                    await Task.Delay(100);
                }
                many.Arcadia.Main.Release(UiAction.Down);
                var r = await Task.WhenAll(tasks);
                done.AddRange(r);
                hlog.AppendLine($"run {run}: max {r.Max():0.0} mean {r.Average():0.0} gc {(GC.GetTotalPauseDuration() - gc0).TotalMilliseconds:0}");
                await Task.Delay(1500);
            }
            hlog.AppendLine($"all: mean {done.Average():0.0} p90 {done.Order().ElementAt(done.Count * 9 / 10):0.0} max {done.Max():0.0}");
            File.WriteAllText("/tmp/claude-0/shots/hold.txt", hlog.ToString());
            return;
        }

        if (Environment.GetEnvironmentVariable("ARC_MODE") == "press")
        {
            var waits = new List<double>();
            var plog = new System.Text.StringBuilder();
            var restMs = int.Parse(Environment.GetEnvironmentVariable("ARC_REST") ?? "250", System.Globalization.CultureInfo.InvariantCulture);
            for (var step = 0; step < 25; step++)
            {
                await Task.Delay(restMs);
                var done = new TaskCompletionSource<double>();
                var posted = Stopwatch.StartNew();
                var gc0 = GC.GetTotalPauseDuration();
                await Task.Run(() => Dispatcher.UIThread.Post(() =>
                {
                    var wait = posted.Elapsed.TotalMilliseconds;
                    many.Arcadia.Main.HandleGamepad(UiAction.Down);
                    many.Arcadia.Main.Release(UiAction.Down);
                    done.SetResult(wait);
                }));
                var w = await done.Task;
                waits.Add(w);
                plog.AppendLine($"step {step}: press waited {w:0.0} ms (gc {(GC.GetTotalPauseDuration() - gc0).TotalMilliseconds:0.0}), heap {GC.GetTotalMemory(false) / 1048576} MB, gc2 {GC.CollectionCount(2)}");
            }

            plog.AppendLine($"wait mean {waits.Average():0.0} p90 {waits.Order().ElementAt(22):0.0} max {waits.Max():0.0}");
            plog.AppendLine(string.Join("\n", gcl.Seen.GroupBy(x => x).Select(g => $"{g.Count()}x {g.Key}")));
            File.WriteAllText("/tmp/claude-0/shots/press.txt", plog.ToString());
            return;
        }

        if (Environment.GetEnvironmentVariable("ARC_SLL") == "1") System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
        var press = new List<double>();
        var block = new List<double>();
        var log = new System.Text.StringBuilder();
        for (var step = 0; step < 25; step++)
        {
            var watch = Stopwatch.StartNew();
            many.Arcadia.Main.HandleGamepad(UiAction.Down);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            many.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            press.Add(watch.Elapsed.TotalMilliseconds);
            many.Arcadia.Main.Release(UiAction.Down);

            // The focus rests: checks and images arrive; the longest stretch the UI thread is busy delays the next press.
            var rest = Stopwatch.StartNew();
            var longest = 0.0;
            while (rest.ElapsedMilliseconds < 600)
            {
                var chunk = Stopwatch.StartNew();
                var gc0 = GC.GetTotalPauseDuration();
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Default);
                var gcMs = (GC.GetTotalPauseDuration() - gc0).TotalMilliseconds;
                var jobs = chunk.Elapsed.TotalMilliseconds;
                many.Window.UpdateLayout();
                var lay = chunk.Elapsed.TotalMilliseconds - jobs;
                Dispatcher.UIThread.RunJobs();
                var rest2 = chunk.Elapsed.TotalMilliseconds - jobs - lay;
                if (jobs + lay + rest2 > 40) System.IO.File.AppendAllText("/tmp/claude-0/shots/dbg.txt", $"--split gc {gcMs:0.0} normal+ {jobs:0.0} layout {lay:0.0} lowprio {rest2:0.0}\n");
                jobs += lay + rest2;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var total = chunk.Elapsed.TotalMilliseconds;
                if (total > 60)
                {
                    System.IO.File.AppendAllText("/tmp/claude-0/shots/dbg.txt", $"--chunk jobs {jobs:0.0}\n"); log.AppendLine($"   chunk jobs {jobs:0.0} render {total - jobs:0.0}");
                }

                longest = Math.Max(longest, total);
                await Task.Delay(4);
            }

            block.Add(longest);
            log.AppendLine($"   gc counts {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} heap {GC.GetTotalMemory(false) / 1048576} MB loh? {GC.GetGCMemoryInfo().GenerationInfo[3].SizeAfterBytes / 1048576} MB server {System.Runtime.GCSettings.IsServerGC} mode {System.Runtime.GCSettings.LatencyMode}");
            log.AppendLine($"step {step}: press {press[^1]:0.0} ms, longest UI block while resting {longest:0.0} ms, hits {list.Hits!.Hits.Count}");
        }

        log.AppendLine($"press mean {press.Average():0.0} max {press.Max():0.0}; block mean {block.Average():0.0} max {block.Max():0.0}");
        File.WriteAllText("/tmp/claude-0/shots/stall.txt", log.ToString());
    }
}
