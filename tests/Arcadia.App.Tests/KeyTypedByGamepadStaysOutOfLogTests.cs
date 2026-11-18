using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Input;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #640: a key typed with the gamepad on the on-screen keyboard must not be readable from arcadia.log, neither from the
/// "Gamepad:" lines nor from the press-to-frame ("Bild:") lines. Made-up launcher and secret; no window.
/// </summary>
public sealed class KeyTypedByGamepadStaysOutOfLogTests
{
    private static readonly LauncherInfo Shop = new("shop", "Shop", "shop", null);

    [AvaloniaFact]
    [Trait("TestCase", "T640-1")]
    public async Task Typing_a_secret_on_the_on_screen_keyboard_leaves_no_button_in_the_log()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Shop, [])],
            new AppSettings { PanEnabled = true, ActiveLaunchers = [Shop.Id] });
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 6 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        // Control: outside of typing the presses are logged as before.
        var before = ArcadiaDriver.Log.Length;
        arcadia.PressButton(GamepadButton.DPadDown).ReleaseButton(GamepadButton.DPadDown);
        Assert.Contains("Gamepad: DPadDown → Down", ArcadiaDriver.Log[before..], StringComparison.Ordinal);

        arcadia.FocusRow(IgdbAccessRows.SecretRowKey);
        arcadia.PressButton(GamepadButton.A).ReleaseButton(GamepadButton.A);
        var editor = arcadia.Main.Settings!.Selected.Editor;
        Assert.NotNull(editor);
        var keyboard = editor.Keyboard;

        var typingStart = ArcadiaDriver.Log.Length;
        foreach (var character in FakeIgdb.Secret)
        {
            var target = keyboard.Keys.Where(k => !k.IsSkipped && k.IsCharacter && k.Label == character.ToString()).OrderBy(k => k.Key.Left).First();
            foreach (var direction in OnScreenKeys.PathTo(keyboard, target))
            {
                Tap(arcadia, direction);
            }

            Tap(arcadia, UiAction.Accept);
        }

        Assert.Equal(FakeIgdb.Secret, editor.Text);
        var typed = ArcadiaDriver.Log[typingStart..];
        Assert.DoesNotContain("Gamepad:", typed, StringComparison.Ordinal);
        Assert.DoesNotContain("Bild:", typed, StringComparison.Ordinal);
    }

    private static void Tap(ArcadiaDriver arcadia, UiAction action)
    {
        var button = action switch
        {
            UiAction.Up => GamepadButton.DPadUp,
            UiAction.Down => GamepadButton.DPadDown,
            UiAction.Left => GamepadButton.DPadLeft,
            UiAction.Right => GamepadButton.DPadRight,
            UiAction.Accept => GamepadButton.A,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "not a key move"),
        };
        arcadia.PressButton(button).ReleaseButton(button);
    }
}
