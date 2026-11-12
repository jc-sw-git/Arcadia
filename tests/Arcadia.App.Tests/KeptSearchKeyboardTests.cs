using Arcadia.App.Input;
using Arcadia.Core.Search;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The search keeps its on-screen keyboard between searches (#477), and each search starts with it as new.</summary>
public sealed class KeptSearchKeyboardTests
{
    private static readonly IReadOnlyList<LauncherSpec> ThousandGames =
    [
        LauncherSpec.With(KnownLaunchers.Gog, [.. Enumerable.Range(1, 1000).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D4}", DateTimeOffset.UnixEpoch))]),
    ];

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
}
