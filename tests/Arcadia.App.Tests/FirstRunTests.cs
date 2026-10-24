using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The first start asks which launchers Arcadia uses (#139).</summary>
public sealed class FirstRunTests
{
    private static readonly IReadOnlyList<LauncherSpec> TwoFoundOneMissing =
    [
        new(KnownLaunchers.Gog, 31),
        new(KnownLaunchers.Xbox, 62),
        new(KnownLaunchers.Amazon, 6, IsInstalled: false),
    ];

    [AvaloniaFact]
    [Trait("Testfall", "T139-1")]
    public async Task A_fresh_install_asks_first_with_the_launchers_found_switched_on()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());

        var firstRun = Assert.IsType<Arcadia.App.Setup.FirstRunViewModel>(arcadia.Main.FirstRun);
        Assert.True(firstRun.IsReady);
        Assert.Equal(["GOG Galaxy", "Xbox"], firstRun.Found.Select(row => row.Name));
        Assert.All(firstRun.Found, row => Assert.True(row.IsOn));
        Assert.Equal(["Amazon Games"], firstRun.Missing.Select(row => row.Name));
        Assert.All(firstRun.Missing, row => Assert.False(row.IsOn));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T139-3")]
    public async Task Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());

        // Xbox (second row) off, then "Fertig" with ≡.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown && arcadia.Main.Tabs.Count > 0, "die Bibliothek soll nach \"Fertig\" erscheinen");

        Assert.Equal(["GOG Galaxy"], arcadia.Main.Tabs.Select(tab => tab.Name));

        using var restarted = await arcadia.RestartAsync();
        Assert.False(restarted.Main.IsFirstRunShown);
        Assert.Equal(["GOG Galaxy"], restarted.Main.Tabs.Select(tab => tab.Name));
    }
}
