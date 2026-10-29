using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// A in the library (T10-4): a tap starts the game on the release, a hold shows the frame after about 0.2 s and opens
/// the detail page after 1 s without letting go; letting go in between does nothing.
/// </summary>
public sealed class GameStartTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PastFeedback = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ShortOfHoldTime = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T10-4")]
    public async Task A_tapped_starts_the_focused_game_through_its_launcher_on_the_release()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var tile = arcadia.FocusedGame!;
        var game = tile.Game;

        arcadia.PressDown(UiAction.Accept).Wait(Tap);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.False(tile.IsHolding);

        // The start is requested synchronously on the release; this is what lets the other tests assert "no start"
        // without waiting for the thread pool.
        arcadia.LetGo(UiAction.Accept);
        Assert.True(tile.IsStarting);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");

        Assert.Equal([game.Key], arcadia.Starts.Keys);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T10-4")]
    [Trait("TestCase", "T11-1")]
    public async Task A_held_shows_the_frame_then_opens_the_detail_page_after_a_second_while_still_held_and_starts_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var tile = arcadia.FocusedGame!;

        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(tile.IsHolding);
        Assert.False(arcadia.Main.IsDetailOpen);

        arcadia.Wait(ShortOfHoldTime - PastFeedback);
        Assert.False(arcadia.Main.IsDetailOpen);

        arcadia.Wait(PastHoldTime - ShortOfHoldTime);
        var detail = Assert.IsType<Arcadia.App.ViewModels.GameDetailViewModel>(arcadia.Detail);
        Assert.Equal(tile.Game.Key, detail.Current.Tile.Game.Key);
        Assert.Equal("GOG Galaxy", detail.TabName);
        Assert.Equal("3 von 31", detail.PositionText);

        arcadia.LetGo(UiAction.Accept);
        Assert.Same(detail, arcadia.Detail);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T10-4")]
    public async Task A_let_go_between_the_frame_and_the_details_does_nothing_also_later()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;

        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(tile.IsHolding);

        arcadia.LetGo(UiAction.Accept);
        Assert.False(tile.IsHolding);

        // Well past the moment the details would have opened: no timer of the cancelled hold may still fire.
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T278-8")]
    public async Task A_game_its_launcher_doesnt_start_after_an_update_is_started_once_more_then_shows_play_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;
        var game = tile.Game;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 1, "der Launcher soll das Spiel starten");

        // The launcher updates the game first, then doesn't start it (Steam, owner's PC 2026-10-28).
        arcadia.Installs.Set(game, new InstallProgress(InstallPhase.Downloading));
        await arcadia.FollowInstallsAsync();
        await arcadia.FollowRunningGamesAsync();
        arcadia.Installs.Set(game, null);
        await arcadia.FollowInstallsAsync();
        await arcadia.FollowRunningGamesAsync();

        arcadia.Time.Advance(RunningGamesTracker.StartAgainAfterInstall + TimeSpan.FromSeconds(1));
        await arcadia.FollowRunningGamesAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 2, "Arcadia soll das Spiel nach dem Update noch einmal starten");
        Assert.Equal([game.Key, game.Key], arcadia.Starts.Keys);
        Assert.True(arcadia.FocusedGame!.IsStarting);

        arcadia.Time.Advance(RunningGamesTracker.StartTimeout + TimeSpan.FromSeconds(1));
        await arcadia.FollowRunningGamesAsync();
        Assert.False(arcadia.FocusedGame!.IsRunningOrStarting);
        Assert.Equal("Spielen", arcadia.FocusedGame.PrimaryActionText);
        Assert.Equal(2, arcadia.Starts.Keys.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T27-1")]
    public async Task A_start_that_fails_says_so_leaves_Arcadia_running_and_A_tries_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.Starts.Failure = new IOException("Die Datei ist gesperrt.");

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.Contains("konnte nicht gestartet werden") == true, "ein Hinweis soll den Fehlstart nennen");
        Assert.Equal($"„{tile.Title}“ konnte nicht gestartet werden: Die Datei ist gesperrt.", arcadia.Main.Toast.Text);
        Assert.False(arcadia.FocusedGame!.IsRunningOrStarting);
        Assert.Equal("Spielen", arcadia.FocusedGame.PrimaryActionText);

        // The launcher works again; the second A starts the game.
        arcadia.Starts.Failure = null;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 2, "A soll den Start erneut versuchen");
        Assert.Equal([tile.Game.Key, tile.Game.Key], arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T27-3")]
    public async Task An_unexpected_error_goes_with_its_stack_trace_into_the_log_a_hint_names_the_log_and_the_controller_still_works()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;
        var marker = $"Unerwartet {Guid.NewGuid():N}";
        arcadia.Starts.Failure = new NotSupportedException(marker);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.Contains("arcadia.log") == true, "ein Hinweis soll das Log nennen");
        Assert.Equal($"„{tile.Title}“ konnte wegen eines unerwarteten Fehlers nicht gestartet werden – Details stehen in arcadia.log.", arcadia.Main.Toast.Text);

        var entry = ArcadiaDriver.Log.Split('\n').SkipWhile(line => !line.Contains(marker)).Take(3).ToList();
        Assert.True(entry.Count == 3, $"Der Fehler fehlt in arcadia.log: {marker}");
        Assert.Contains(nameof(NotSupportedException), entry[0]);
        Assert.Contains(" at ", entry[1]);

        // Arcadia goes on with the controller alone: on to the next installed game, which starts.
        Assert.False(tile.IsRunningOrStarting);
        do
        {
            arcadia.Press(UiAction.Right);
        }
        while (!arcadia.FocusedGame!.IsInstalled);

        var next = arcadia.FocusedGame;
        Assert.NotSame(tile, next);
        arcadia.Starts.Failure = null;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 2, "A soll das nächste Spiel starten");
        Assert.Equal([tile.Game.Key, next.Game.Key], arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-20")]
    public async Task A_demo_game_shows_the_demo_hint_instead_of_an_error()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 31, IsDemo: true)]);

        // One demo game runs; another one is started.
        var index = arcadia.PageGames.ToList().FindIndex(tile => tile.IsInstalled && !tile.IsRunningOrStarting);
        arcadia.Press(UiAction.Right, times: index);
        var tile = arcadia.FocusedGame!;

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.StartsWith("Demo-Spiel") == true, "der Demo-Hinweis soll erscheinen");
        Assert.Equal($"Demo-Spiel: Hier würde jetzt „{tile.Title}“ gestartet.", arcadia.Main.Toast.Text);
        await arcadia.WaitUntilAsync(() => !arcadia.FocusedGame!.IsRunningOrStarting, "die Kachel soll wieder „Spielen“ zeigen");
    }
}
