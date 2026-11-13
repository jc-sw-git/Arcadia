using Arcadia.App.Input;
using Arcadia.App.Views;
using Arcadia.Core.Search;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// The search opens without building its controls again (#477): building the on-screen keyboard's and the hits' controls
/// took most of the half second between X and the search on screen; binding the kept ones to the new search takes milliseconds.
/// </summary>
public sealed class SearchSpeedTests
{
    private static readonly IReadOnlyList<LauncherSpec> ThousandGames =
    [
        LauncherSpec.With(KnownLaunchers.Gog, [.. Enumerable.Range(1, 1000).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D4}", DateTimeOffset.UnixEpoch))]),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T477-1")]
    public async Task Opening_the_search_again_keeps_the_keys_and_hits_and_binds_them_to_the_new_search()
    {
        using var window = await ArcadiaWindow.ShowAsync(ThousandGames);
        window.Press(UiAction.Search);
        window.Arcadia.Main.TypeInSearch("spiel");
        window.Settle();
        var keys = Keys(window);
        Assert.NotEmpty(keys);

        // B puts the keyboard in, which shows the grid with its places for more hits; the next B closes the search.
        window.Press(UiAction.Back);
        var hits = Hits(window);
        Assert.NotEmpty(hits);
        window.Press(UiAction.Back);
        Assert.Null(window.Arcadia.Search);
        window.Press(UiAction.Search);
        Assert.Equal(keys, Keys(window));
        Assert.Equal(string.Empty, window.Arcadia.Search!.Text);

        window.Arcadia.Main.TypeInSearch("spiel 0777");
        window.Settle();
        Assert.Equal(hits, Hits(window));
        var search = window.Arcadia.Search!;
        Assert.Equal(search.Results.Select(tile => tile.Title), hits.Take(search.Results.Count).Select(hit => ToolTip.GetTip(hit)));
        Assert.Equal("Spiel 0777", ToolTip.GetTip(hits[0]));
    }

    /// <summary>The keyboard is built right after the library shows, so even the first X finds its keys there.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T477-5")]
    public async Task The_keyboard_is_built_once_the_library_shows_before_the_first_search()
    {
        using var window = await ArcadiaWindow.ShowAsync(ThousandGames);
        Assert.Null(window.Arcadia.Search);
        var keys = window.Named<OnScreenKeyboardView>("SearchKeyboard").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("boardKey")).ToList();
        Assert.NotEmpty(keys);

        window.Press(UiAction.Search);
        Assert.Equal(keys, Keys(window));
    }

    /// <summary>
    /// An [R3] switch keeps every key that stands where it stood and only changes what it types: building all the keys'
    /// controls again made a switch take most of a second (#553).
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T553-23")]
    public async Task A_layout_switch_keeps_the_keys_that_stand_where_they_stood()
    {
        using var window = await ArcadiaWindow.ShowAsync(ThousandGames);
        window.Press(UiAction.Search);
        var keyboard = window.Arcadia.Search!.Keyboard;
        foreach (var code in new[] { "US", "FR", "ES", "DE" })
        {
            var before = Keys(window);
            var views = keyboard.Keys.ToList();
            window.Press(UiAction.KeyboardLayout);
            while (keyboard.LayoutMenu.Items.Single(item => item.IsFocused).Layout.Code != code)
            {
                window.Press(UiAction.Down);
            }

            window.Press(UiAction.Accept);
            Assert.Equal(code, keyboard.Layout.Code);
            var kept = keyboard.Keys.Count(views.Contains);
            Assert.True(kept >= keyboard.Keys.Count - 4, $"{code}: nur {kept} von {keyboard.Keys.Count} Tasten behalten.");
            Assert.Equal(kept, Keys(window).Count(before.Contains));
        }
    }

    private static List<Button> Keys(ArcadiaWindow window) =>
        window.Window.GetVisualDescendants().OfType<OnScreenKeyboardView>().Single(view => view.DataContext == window.Arcadia.Search!.Keyboard)
            .GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("boardKey")).ToList();

    private static List<Button> Hits(ArcadiaWindow window) =>
        window.Named<ItemsControl>("SearchResults").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("resultTile")).ToList();
}
