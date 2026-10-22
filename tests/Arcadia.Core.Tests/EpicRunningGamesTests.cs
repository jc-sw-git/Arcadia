using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests;

public sealed class EpicRunningGamesTests : IDisposable
{
    // Lines as in the owner's Epic log (see EpicLauncherLogTests).
    private const string Head = "[2026.10.09-15.50.00:000][  0]LogInit: Log file open, 10/09/26 17:50:00\n";
    private const string LoopHeroStarted = "[2026.10.09-15.57.42:000][813]LogPortalPublishTime: Started tracking playtime for ff50f85ed609454e80ac46d9496da34d:9c7c10e8e1a648f8a9e35f28a1d45900:LoopHero\n";
    private const string LoopHeroStopped = "[2026.10.09-15.58.02:910][225]LogPortalPublishTime: Stopped tracking playtime for ff50f85ed609454e80ac46d9496da34d:9c7c10e8e1a648f8a9e35f28a1d45900:LoopHero\n";
    private const string ChessStarted = "[2026.10.09-16.10.48:340][256]LogPortalPublishTime: Started tracking playtime for 0e9ae0e756524ed49a27435c9c3c3e1c:9a84503f4c494b91b565ba67f00c4fb1:Chess\n";

    private static readonly DateTimeOffset LoopHeroStart = new(2026, 10, 9, 15, 57, 42, TimeSpan.Zero);
    private static readonly DateTimeOffset ChessStart = new(2026, 10, 9, 16, 10, 48, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-epic-running-" + Guid.NewGuid().ToString("N"));
    private readonly string _log;

    public EpicRunningGamesTests()
    {
        Directory.CreateDirectory(_root);
        _log = Path.Combine(_root, "EpicGamesLauncher.log");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_game_runs_from_its_start_line_until_its_stop_line()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted);

        Assert.Equal([new RunningGame("LoopHero", LoopHeroStart)], monitor.Poll());

        File.AppendAllText(_log, LoopHeroStopped);
        Assert.Empty(monitor.Poll());
    }

    [Fact]
    public void Several_games_may_run_at_once()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted + ChessStarted);

        Assert.Equal([new RunningGame("LoopHero", LoopHeroStart), new RunningGame("Chess", ChessStart)], monitor.Poll());
    }

    [Fact]
    public void Reads_only_what_the_launcher_added_since_the_last_poll()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted);
        monitor.Poll();

        // Had the poll read the log from the start again, the old start line would still count.
        File.AppendAllText(_log, LoopHeroStopped + ChessStarted);

        Assert.Equal([new RunningGame("Chess", ChessStart)], monitor.Poll());
    }

    [Fact]
    public void A_line_still_being_written_waits_for_its_line_break()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted[..60]);
        Assert.Empty(monitor.Poll());

        File.AppendAllText(_log, LoopHeroStarted[60..]);
        Assert.Equal([new RunningGame("LoopHero", LoopHeroStart)], monitor.Poll());
    }

    [Fact]
    public void Windows_line_breaks_are_read_alike()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, (Head + LoopHeroStarted).Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal([new RunningGame("LoopHero", LoopHeroStart)], monitor.Poll());
    }

    [Fact]
    public void A_new_log_after_a_launcher_restart_is_read_from_its_start()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted + ChessStarted);
        monitor.Poll();

        // The launcher crashed with Loop Hero open; its new log starts at another time and is longer than the old one.
        File.WriteAllText(_log, Head.Replace("15.50.00", "16.20.00", StringComparison.Ordinal) + ChessStarted + ChessStarted + ChessStarted);

        Assert.Equal([new RunningGame("Chess", ChessStart)], monitor.Poll());
    }

    [Fact]
    public void A_shorter_log_is_a_new_one()
    {
        var monitor = new EpicRunningGames(_root);
        File.WriteAllText(_log, Head + LoopHeroStarted);
        monitor.Poll();

        File.WriteAllText(_log, Head);

        Assert.Empty(monitor.Poll());
    }

    [Fact]
    public void Without_a_log_no_game_runs()
    {
        var monitor = new EpicRunningGames(_root);
        Assert.Empty(monitor.Poll());

        File.WriteAllText(_log, Head + LoopHeroStarted);
        Assert.Single(monitor.Poll());

        File.Delete(_log);
        Assert.Empty(monitor.Poll());
    }

    [Fact]
    public void Lines_longer_than_one_read_are_put_together()
    {
        var monitor = new EpicRunningGames(_root);
        var noise = "[2026.10.09-15.51.00:000][  1]LogHttp: " + new string('x', 200_000) + "\n";
        File.WriteAllText(_log, Head + noise + LoopHeroStarted + noise);

        Assert.Equal([new RunningGame("LoopHero", LoopHeroStart)], monitor.Poll());
    }
}
