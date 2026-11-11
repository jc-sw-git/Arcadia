using Arcadia.App.Input;
using Arcadia.App.Views;
using Arcadia.Core.Search;
using Arcadia.Launchers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

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

    /// <summary>The kept keyboard starts each search as a new one did: focus on its first key, shift off, nothing typed.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T477-2")]
    public async Task A_search_opened_again_starts_with_the_keyboard_as_new()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(ThousandGames);
        arcadia.Press(UiAction.Search);
        var keyboard = arcadia.Search!.Keyboard;
        var firstKey = keyboard.FocusedKey;

        // Two keys to the right, type one, shift on.
        arcadia.Press(UiAction.Right, times: 2).Press(UiAction.Accept).Press(UiAction.PreviousTab);
        Assert.NotSame(firstKey, keyboard.FocusedKey);
        Assert.Equal(KeyLevel.Shift, keyboard.Level);
        Assert.NotEqual(string.Empty, arcadia.Search.Text);

        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        arcadia.Press(UiAction.Search);

        Assert.Same(keyboard, arcadia.Search!.Keyboard);
        Assert.Same(firstKey, keyboard.FocusedKey);
        Assert.Single(keyboard.Keys, key => key.IsFocused);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        Assert.Equal(string.Empty, arcadia.Search.Text);
        Assert.True(arcadia.Search.IsKeyboardOut);

        // The keys type into the new search, not into the closed one.
        arcadia.Press(UiAction.Accept);
        Assert.Equal(firstKey.Label, arcadia.Search.Text);
    }

    private static List<Button> Keys(ArcadiaWindow window) =>
        window.Window.GetVisualDescendants().OfType<OnScreenKeyboardView>().Single(view => view.DataContext == window.Arcadia.Search!.Keyboard)
            .GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("boardKey")).ToList();

    private static List<Button> Hits(ArcadiaWindow window) =>
        window.Named<ItemsControl>("SearchResults").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("resultTile")).ToList();
}
