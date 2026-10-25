using Arcadia.App.Input;
using Arcadia.App.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>Arcadia's own updates in "Über Arcadia" (#23), with GitHub's releases faked.</summary>
public sealed class UpdateTests
{
    [AvaloniaFact]
    [Trait("Testfall", "T23-2")]
    public async Task Without_a_newer_release_the_search_says_Arcadia_is_up_to_date_and_offers_no_update()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(releases: new FakeReleases("0.1.0", ("0.1.0", ["Erste Version"])));
        var about = OpenAbout(arcadia);
        Assert.Equal(["arcadia-version", "arcadia-check"], about.Rows.Select(row => row.Key));

        arcadia.Press(UiAction.Down);
        Assert.Equal("Nach Updates suchen", arcadia.Main.Settings!.FocusedRow?.Title);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Updates is { IsChecking: false, LastChecked: not null }, "die Suche nach Updates ist fertig");
        arcadia.Wait(TimeSpan.FromMilliseconds(50));

        Assert.StartsWith("Arcadia ist auf dem neuesten Stand.", about.Rows[0].Subtitle);
        Assert.DoesNotContain(about.Rows, row => row.Title == "Jetzt aktualisieren");
        Assert.False(arcadia.Main.Status.HasArcadiaUpdate);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T23-4")]
    public async Task What_is_new_lists_the_changes_of_every_release_after_the_running_one_and_B_goes_back()
    {
        var releases = new FakeReleases(
            "0.1.0",
            ("0.3.0", ["Suche nach Genre"]),
            ("0.2.1", ["Absturz beim Start behoben"]),
            ("0.2.0", ["Spotify-Tab", "Neue Sortierung"]),
            ("0.1.0", ["Erste Version"]));
        using var arcadia = await ArcadiaDriver.StartAsync(releases: releases);
        var about = OpenAbout(arcadia);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => about.Rows.Any(row => row.Key == "arcadia-changelog"), "„Was ist neu“ erscheint");

        Assert.True(arcadia.Main.Status.HasArcadiaUpdate);
        arcadia.Press(UiAction.Down, times: 2);
        var changelog = arcadia.Main.Settings!.FocusedRow!;
        Assert.Equal("Was ist neu", changelog.Title);
        Assert.Equal("Neu bis Version 0.3.0", changelog.Subtitle);
        var shown = changelog.Info.Cast<InfoRelease>().ToList();
        Assert.Equal(["Version 0.3.0", "Version 0.2.1", "Version 0.2.0"], shown.Select(release => release.VersionText));
        Assert.Equal(["Suche nach Genre", "Absturz beim Start behoben", "Spotify-Tab", "Neue Sortierung"], shown.SelectMany(release => release.Changes));

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    /// <summary>≡, then LB to the last category.</summary>
    private static SettingsCategoryViewModel OpenAbout(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.PreviousTab);
        var about = arcadia.Main.Settings!.Selected;
        Assert.Equal("Über Arcadia", about.Name);
        return about;
    }
}
