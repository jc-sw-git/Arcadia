using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>Games not installed and installations running in the launcher (#10, #41).</summary>
public sealed class InstallTests
{
    private static readonly Game Bravo = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");

    /// <summary>Alpha installed, Bravo not, in this order; the focus starts on Alpha.</summary>
    private static readonly LauncherSpec Library = LauncherSpec.With(
        KnownLaunchers.Gog,
        TestGames.Installed(KnownLaunchers.Gog, "Alpha", installedAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        Bravo);

    [AvaloniaFact]
    [Trait("Testfall", "T10-3")]
    public async Task The_menu_of_a_game_not_installed_offers_installing_and_shows_a_running_installation_instead()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Library]);
        arcadia.Press(UiAction.Right).Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!;

        Assert.Equal("BRAVO", menu.Title);
        Assert.Equal(["Installieren", "Details anzeigen", "Zu Favoriten hinzufügen", "In GOG Galaxy öffnen"], menu.Actions.Select(action => action.Label));
        Assert.True(menu.Actions[0].IsEnabled);
        Assert.True(menu.Actions[0].IsFocused);

        arcadia.Press(UiAction.Back);
        arcadia.Installs.Set(Bravo, new InstallProgress(InstallPhase.Downloading, 0.42));
        await arcadia.FollowInstallsAsync();
        arcadia.Press(UiAction.Options);
        menu = arcadia.Main.GameMenu!;

        Assert.Equal(["Wird installiert · 42\u00A0%", "Details anzeigen", "Zu Favoriten hinzufügen", "In GOG Galaxy öffnen"], menu.Actions.Select(action => action.Label));
        Assert.False(menu.Actions[0].IsEnabled);
        Assert.False(menu.Actions[0].IsFocused);

        // A on the cover can't request it a second time either.
        arcadia.Press(UiAction.Back).Press(UiAction.Accept);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.StartsWith("„Bravo“ wird bereits installiert", arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T41-9")]
    public async Task A_game_waiting_in_the_queue_shows_the_hourglass_instead_of_the_spinner_on_the_cover_and_in_the_preview()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Library]);
        arcadia.Press(UiAction.Right);

        arcadia.Installs.Set(Bravo, new InstallProgress(InstallPhase.Queued, QueuePosition: 2));
        await arcadia.FollowInstallsAsync();

        // Cover and preview show the focused tile.
        var tile = arcadia.Main.FocusedGame!;
        Assert.Equal("Bravo", tile.Title);
        Assert.True(tile.IsInstallQueued);
        Assert.False(tile.IsInstallSpinning);
        Assert.Equal("Wartet · 2", tile.InstallShortText);
        Assert.Equal("In der Warteschlange · Platz 2", tile.InstallStatusText);

        // Its turn comes: the spinner replaces the hourglass.
        arcadia.Installs.Set(Bravo, new InstallProgress(InstallPhase.Downloading, 0.1));
        await arcadia.FollowInstallsAsync();
        tile = arcadia.Main.FocusedGame!;
        Assert.False(tile.IsInstallQueued);
        Assert.True(tile.IsInstallSpinning);
    }
}
