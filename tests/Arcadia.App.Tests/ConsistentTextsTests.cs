using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Reworded texts with placeholders as the running app fills them (#339): a count in singular and plural, a game's
/// name in „…“ quotes, nothing left of a placeholder or a key.
/// </summary>
public sealed class ConsistentTextsTests
{
    private static void AssertWhole(string? text)
    {
        Assert.NotNull(text);
        Assert.DoesNotContain("{", text, StringComparison.Ordinal);
        Assert.DoesNotContain("}", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T339-4")]
    public async Task A_game_with_an_update_starts_with_its_name_in_quotes_and_counts_say_Spiel_and_Spiele()
    {
        var withUpdate = TestGames.Installed(KnownLaunchers.Gog, "Hollow Crown", installedAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)) with { UpdateAvailable = true };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, withUpdate), new LauncherSpec(KnownLaunchers.Xbox, 3)]);

        arcadia.Starts.Stage = LaunchStage.Starting;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.Contains("wird gestartet", StringComparison.Ordinal) == true, "der Start-Hinweis");
        Assert.Equal("„Hollow Crown“ wird gestartet – GOG Galaxy installiert eventuell vorher das Update …", arcadia.Main.Toast.Text);
        AssertWhole(arcadia.Main.Toast.Text);

        arcadia.Press(UiAction.Settings);
        var launchers = Assert.IsType<LauncherSettingsViewModel>(arcadia.Main.Settings!.Selected);
        var gog = launchers.Rows.OfType<LauncherRowViewModel>().Single(row => row.Key == KnownLaunchers.Gog.Id);
        var xbox = launchers.Rows.OfType<LauncherRowViewModel>().Single(row => row.Key == KnownLaunchers.Xbox.Id);
        Assert.StartsWith("1 Spiel · ", gog.StatusText, StringComparison.Ordinal);
        Assert.StartsWith("3 Spiele · ", xbox.StatusText, StringComparison.Ordinal);
        AssertWhole(gog.StatusText);
        AssertWhole(xbox.StatusText);
    }
}
