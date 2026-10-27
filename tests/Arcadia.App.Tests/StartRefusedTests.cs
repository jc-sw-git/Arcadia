using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// A start the launcher aborts or never reports (#277): "Wird gestartet …" ends, a toast says why, and an aborted start
/// brings Arcadia to the front. The launcher is a fake that reports through <c>IRunningGamesMonitor</c> as Steam does.
/// </summary>
public sealed class StartRefusedTests
{
    private const string NoLicense = "Das angemeldete Steam-Konto besitzt dieses Spiel nicht.";

    private static Task<ArcadiaDriver> StartAsync() => ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Steam, 6, Running: [])]);

    [AvaloniaFact]
    [Trait("TestCase", "T277-1")]
    public async Task A_start_the_launcher_refuses_ends_at_the_next_poll_with_its_reason_and_brings_Arcadia_to_the_front()
    {
        using var arcadia = await StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");
        await arcadia.FollowRunningGamesAsync();
        Assert.True(tile.IsStarting);

        arcadia.IsArcadiaInFront = false;
        arcadia.RefuseInLauncher(tile.Game, NoLicense, "AppError_5");
        await arcadia.FollowRunningGamesAsync();

        Assert.Equal($"„{tile.Title}“: {NoLicense}", arcadia.Main.Toast.Text);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Equal("Spielen", tile.PrimaryActionText);
        Assert.Equal([tile.Game.Key], arcadia.BroughtToFrontFor);
        Assert.Contains("AppError_5", ArcadiaDriver.Log, StringComparison.Ordinal);

        // A tries again.
        arcadia.IsArcadiaInFront = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count == 2, "A soll den Start erneut versuchen");
        Assert.True(tile.IsStarting);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T277-5")]
    public async Task A_refusal_without_a_known_reason_names_the_launcher()
    {
        using var arcadia = await StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");

        arcadia.RefuseInLauncher(tile.Game, reason: null, "AppError_28");
        await arcadia.FollowRunningGamesAsync();

        Assert.Equal($"„{tile.Title}“: Steam hat den Start abgebrochen.", arcadia.Main.Toast.Text);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Equal([tile.Game.Key], arcadia.BroughtToFrontFor);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T277-3")]
    public async Task A_normal_start_runs_untouched_by_an_older_refusal_of_the_same_game()
    {
        using var arcadia = await StartAsync();
        var tile = arcadia.FocusedGame!;

        // Steam aborted a start of the game a while before Arcadia asked; its line is read only now.
        arcadia.RefuseInLauncher(tile.Game, NoLicense, "AppError_5");
        arcadia.Wait(TimeSpan.FromSeconds(3));
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");
        var startToast = arcadia.Main.Toast.Text;
        await arcadia.FollowRunningGamesAsync();
        Assert.True(tile.IsStarting);

        arcadia.RunInLauncher(tile.Game);
        await arcadia.FollowRunningGamesAsync();

        Assert.True(tile.IsRunning);
        Assert.Equal("Zurück ins Spiel", tile.PrimaryActionText);
        Assert.Equal(startToast, arcadia.Main.Toast.Text);
        Assert.Empty(arcadia.BroughtToFrontFor);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T277-4")]
    public async Task A_start_nothing_is_heard_of_ends_after_the_timeout_with_a_toast_and_leaves_Arcadia_where_it_is()
    {
        using var arcadia = await StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");

        arcadia.Time.Advance(RunningGamesTracker.StartTimeout);
        await arcadia.FollowRunningGamesAsync();
        Assert.True(tile.IsStarting);

        arcadia.Time.Advance(TimeSpan.FromSeconds(2));
        await arcadia.FollowRunningGamesAsync();

        Assert.Equal($"„{tile.Title}“ wurde nicht gestartet.", arcadia.Main.Toast.Text);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Equal("Spielen", tile.PrimaryActionText);
        Assert.Empty(arcadia.BroughtToFrontFor);
    }
}
