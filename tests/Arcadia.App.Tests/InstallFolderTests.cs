using System.Diagnostics;
using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>
/// The focused game's install folder is checked off the UI thread (#227): a game uninstalled in its launcher while Arcadia
/// wasn't looking is noticed, and a disk that doesn't answer never holds up the focus.
/// </summary>
public sealed class InstallFolderTests
{
    private static readonly LauncherInfo Gog = KnownLaunchers.Gog;

    [AvaloniaFact]
    [Trait("TestCase", "T227-1")]
    public async Task A_focused_game_whose_folder_is_gone_leads_to_one_rescan_that_shows_it_uninstalled()
    {
        var (games, spec) = Library("Alpha", "Bravo", "Charlie");
        using var arcadia = await ArcadiaDriver.StartAsync([spec]);
        await arcadia.FolderChecksAnsweredAsync();
        var scans = Scans(arcadia);

        // Uninstalled in GOG Galaxy while Arcadia wasn't looking, long enough after the start that a rescan isn't skipped.
        arcadia.Wait(TimeSpan.FromSeconds(10));
        arcadia.Folders.Remove(games[1].InstallDirectory!);
        games[1] = NotInstalled(Gog, "Bravo");
        arcadia.Press(UiAction.Right);

        await arcadia.WaitUntilAsync(() => arcadia.FocusedGame is { Title: "Bravo", Game.IsInstalled: false }, "Bravo als nicht installiert");
        await arcadia.FolderChecksAnsweredAsync();
        Assert.Equal(scans + 1, Scans(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T227-2")]
    public async Task A_disk_that_does_not_answer_holds_up_no_focus_move_and_rapid_moves_leave_one_check_waiting()
    {
        var (_, spec) = Library("Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot");
        using var arcadia = await ArcadiaDriver.StartAsync([spec]);
        await arcadia.FolderChecksAnsweredAsync();
        var checks = arcadia.Folders.Checks;

        arcadia.Folders.Hang();
        var watch = Stopwatch.StartNew();
        arcadia.Press(UiAction.Right, times: 5);
        watch.Stop();

        Assert.Equal("Foxtrot", arcadia.FocusedGame?.Title);
        Assert.InRange(watch.ElapsedMilliseconds, 0, 1000);

        // Bravo's check hangs; of Charlie to Foxtrot only the last waits behind it.
        await arcadia.WaitUntilAsync(() => arcadia.Folders.Checks == checks + 1, "Bravos Prüfung läuft");
        arcadia.Folders.Answer();
        await arcadia.FolderChecksAnsweredAsync();
        Assert.Equal(checks + 2, arcadia.Folders.Checks);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T227-3")]
    public async Task A_late_answer_for_a_game_that_lost_the_focus_starts_no_rescan()
    {
        var (games, spec) = Library("Alpha", "Bravo", "Charlie");
        using var arcadia = await ArcadiaDriver.StartAsync([spec]);
        await arcadia.FolderChecksAnsweredAsync();
        arcadia.Wait(TimeSpan.FromSeconds(10));
        var scans = Scans(arcadia);

        arcadia.Folders.Hang();
        arcadia.Folders.Remove(games[1].InstallDirectory!);
        arcadia.Press(UiAction.Right).Press(UiAction.Right);
        arcadia.Folders.Answer();
        await arcadia.FolderChecksAnsweredAsync();

        Assert.Equal("Charlie", arcadia.FocusedGame?.Title);
        Assert.Equal(scans, Scans(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T227-4")]
    public async Task A_late_answer_after_the_focus_moved_to_an_uninstalled_game_starts_no_rescan()
    {
        var (games, spec) = Library("Alpha", "Bravo", "Charlie");
        games[2] = NotInstalled(Gog, "Charlie");
        using var arcadia = await ArcadiaDriver.StartAsync([spec]);
        await arcadia.FolderChecksAnsweredAsync();
        arcadia.Wait(TimeSpan.FromSeconds(10));
        var scans = Scans(arcadia);

        // Charlie has no folder to check, so Bravo's answer stays the last one asked for; it no longer has the focus.
        arcadia.Folders.Hang();
        arcadia.Folders.Remove(games[1].InstallDirectory!);
        arcadia.Press(UiAction.Right).Press(UiAction.Right);
        arcadia.Folders.Answer();
        await arcadia.FolderChecksAnsweredAsync();

        Assert.Equal("Charlie", arcadia.FocusedGame?.Title);
        Assert.Equal(scans, Scans(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T227-5")]
    public async Task A_check_that_fails_is_reported_and_the_check_waiting_behind_it_still_runs()
    {
        var (games, spec) = Library("Alpha", "Bravo", "Charlie");
        using var arcadia = await ArcadiaDriver.StartAsync([spec]);
        await arcadia.FolderChecksAnsweredAsync();
        var checks = arcadia.Folders.Checks;

        arcadia.Folders.Hang();
        arcadia.Folders.Fail(games[1].InstallDirectory!);
        arcadia.Press(UiAction.Right).Press(UiAction.Right);
        arcadia.Folders.Answer();
        await arcadia.FolderChecksAnsweredAsync();

        Assert.Equal(checks + 2, arcadia.Folders.Checks);
        var log = File.ReadAllText(Path.Combine(Arcadia.App.Services.AppLog.Directory, "arcadia.log"));
        // By the folder's name: the log writes the user profile of a full path as %USERPROFILE%.
        Assert.Contains($"Test: {Path.GetFileName(games[1].InstallDirectory)} can't be read.", log);
    }

    /// <summary>Installed games, each in a folder of its own that exists.</summary>
    private static (List<Game> Games, LauncherSpec Spec) Library(params string[] titles)
    {
        var games = titles
            .Select(title => Installed(Gog, title, ArcadiaDriver.Now.AddDays(-30)) with
            {
                InstallDirectory = Directory.CreateTempSubdirectory($"arcadia-227-{title}-").FullName,
            })
            .ToList();
        return (games, new LauncherSpec(Gog, games.Count, Library: games));
    }

    private static int Scans(ArcadiaDriver arcadia) => ((FixedScanner)arcadia.Launchers.Single().Scanner).Scans;
}
