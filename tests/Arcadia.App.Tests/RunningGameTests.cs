using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>Running games (#95) with Arcadia's demo data, in which one game of each launcher runs.</summary>
public sealed class RunningGameTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("Testfall", "T95-8")]
    public async Task In_the_demo_data_one_game_per_launcher_runs_and_offers_Zurueck_ins_Spiel_but_no_uninstall()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(ArcadiaDriver.DefaultLaunchers.Select(spec => spec with { IsDemo = true }).ToList());

        // It runs since 25 minutes before Arcadia started.
        var since = $"Läuft seit {ArcadiaDriver.Now.AddMinutes(-25).ToLocalTime():HH:mm}";
        foreach (var tab in arcadia.Main.Tabs.ToList())
        {
            var running = RunningTilesOf(arcadia).ToList();
            var tile = Assert.Single(running);
            Assert.Equal(tab, arcadia.Main.SelectedTab);
            Assert.Equal("Läuft", tile.RunStateShortText);
            Assert.Equal(since, tile.RunStateText);
            Assert.Equal("Zurück ins Spiel", tile.PrimaryActionText);
            arcadia.Press(UiAction.NextTab);
        }

        // On the last tab's running game: its menu.
        var game = FocusRunningGame(arcadia);
        arcadia.Press(UiAction.Options);
        var uninstall = arcadia.Main.GameMenu!.Actions.Single(action => action.Kind == GameActionKind.Uninstall);
        Assert.Equal("Deinstallieren …", uninstall.Label);
        Assert.False(uninstall.IsEnabled);
        Assert.Equal("Erst wenn das Spiel beendet ist", uninstall.DisabledReason);
        arcadia.Press(UiAction.Back);

        // Its detail page.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Main.Detail!.Current;
        Assert.Equal(game, page.Tile.Title);
        Assert.Equal(since.ToUpperInvariant(), page.RunStateUpper);
        Assert.Equal("Zurück ins Spiel", page.TopRow[0].ButtonLabel);
        Assert.True(page.TopRow[0].IsFocused);
        Assert.False(page.Uninstall!.IsEnabled);
        Assert.Equal("Erst wenn das Spiel beendet ist", page.Uninstall.DisabledReason);

        // A on "Zurück ins Spiel": a demo game has no window.
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.StartsWith("Arcadia findet kein Fenster") == true, "der Hinweis auf das fehlende Fenster soll erscheinen");
        Assert.Equal($"Arcadia findet kein Fenster von „{game}“ – vielleicht startet es noch oder läuft ohne Fenster.", arcadia.Main.Toast.Text);
        Assert.Empty(arcadia.Starts.Keys);
    }

    /// <summary>The running games of the selected tab, page by page; ends on the page it started on.</summary>
    private static IEnumerable<GameTileViewModel> RunningTilesOf(ArcadiaDriver arcadia)
    {
        var pages = arcadia.Main.PageButtons.Count(button => button.IsPage);
        var running = new List<GameTileViewModel>();
        for (var page = 0; page < Math.Max(pages, 1); page++)
        {
            running.AddRange(arcadia.Main.PageGames.Where(tile => tile.IsRunning));
            arcadia.Press(UiAction.NextPage);
        }

        arcadia.Press(UiAction.PreviousPage, times: pages);
        return running;
    }

    /// <summary>Turns the pages of the selected tab until the running game shows and focuses it; returns its title.</summary>
    private static string FocusRunningGame(ArcadiaDriver arcadia)
    {
        while (!arcadia.Main.PageGames.Any(tile => tile.IsRunning))
        {
            arcadia.Press(UiAction.NextPage);
        }

        var index = arcadia.Main.PageGames.ToList().FindIndex(tile => tile.IsRunning);
        arcadia.Press(UiAction.Right, times: index);
        return arcadia.Main.FocusedGame!.Title;
    }
}
