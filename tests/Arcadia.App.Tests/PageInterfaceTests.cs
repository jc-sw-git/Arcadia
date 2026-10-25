using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>
/// What every page shares through the page interface (#208): held buttons dropped when a page loses the top, run and
/// install states on the covers of every open page, and the input kind reaching the page that shows hints for it.
/// </summary>
public sealed class PageInterfaceTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T208-1")]
    public async Task X_held_in_the_search_stops_erasing_once_the_settings_cover_it_and_the_search_is_as_it_was_after_them()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;
        search.Type("hollowhollow");

        // Held, X erases one after another; the chip opens the settings while it is still down.
        arcadia.PressDown(UiAction.Search).Wait(TimeSpan.FromMilliseconds(700));
        var erased = search.Text;
        Assert.True(erased.Length < "hollowhollow".Length - 1, $"X gehalten soll mehrere Zeichen löschen, übrig: „{erased}“");
        arcadia.Click(arcadia.Main.OpenSettingsCommand);
        Assert.True(arcadia.Main.IsSettingsOpen);

        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Equal(erased, search.Text);
        arcadia.LetGo(UiAction.Search).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.Equal(erased, search.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T208-2")]
    public async Task An_install_in_the_launcher_shows_on_the_cover_on_the_hit_and_on_the_hits_detail_page_at_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(
        [
            LauncherSpec.With(
                KnownLaunchers.Gog,
                Installed(KnownLaunchers.Gog, "Iron Harbor", ArcadiaDriver.Now.AddDays(-30)),
                NotInstalled(KnownLaunchers.Gog, "River Raid")),
        ]);
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("raid");
        var hit = arcadia.Search!.FocusedResult!;
        Assert.Equal("River Raid", hit.Title);
        arcadia.Hold(UiAction.Settings, PastHoldTime);
        var detail = arcadia.Detail!;
        Assert.True(detail.IsFromSearch);

        arcadia.Installs.Set(hit.Game, new InstallProgress(InstallPhase.Downloading, 0.42));
        await arcadia.FollowInstallsAsync();

        var cover = Assert.Single(arcadia.Main.PageGames, tile => tile.Game.Key == hit.Game.Key);
        Assert.Equal(InstallPhase.Downloading, cover.InstallState?.Phase);
        Assert.Equal(InstallPhase.Downloading, Assert.Single(arcadia.Search!.Results, tile => tile.Game.Key == hit.Game.Key).InstallState?.Phase);
        Assert.Equal(InstallPhase.Downloading, detail.Current.Tile.InstallState?.Phase);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T208-3")]
    public async Task The_keyboard_of_a_text_in_the_settings_names_the_keys_after_a_key_press_and_the_buttons_after_a_pad_press()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = ["gog", "xbox", "amazon"] });
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        arcadia.FocusRow("launcher-keyword");
        arcadia.Press(UiAction.Accept);
        var keyboard = arcadia.Main.Settings!.Selected.Editor!.Keyboard;
        Assert.True(keyboard.UsesController);

        arcadia.Main.NoteKeyboard();
        Assert.False(keyboard.UsesController);
        arcadia.Press(UiAction.Right);
        Assert.True(keyboard.UsesController);
        arcadia.Main.NotePointer();
        Assert.False(keyboard.UsesController);
    }
}
