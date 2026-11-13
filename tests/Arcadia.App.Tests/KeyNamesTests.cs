using Arcadia.App.Input;
using Arcadia.Input;

namespace Arcadia.App.Tests;

/// <summary>A text names the key that really does what the button does while the keyboard is in use (#539).</summary>
public sealed class KeyNamesTests
{
    public static TheoryData<string, GamepadButton> Buttons => new()
    {
        { "A", GamepadButton.A },
        { "B", GamepadButton.B },
        { "X", GamepadButton.X },
        { "Y", GamepadButton.Y },
        { "LB", GamepadButton.LeftShoulder },
        { "RB", GamepadButton.RightShoulder },
        { "LT", GamepadButton.LeftTrigger },
        { "RT", GamepadButton.RightTrigger },
        { "L3", GamepadButton.LeftStickClick },
        { "R3", GamepadButton.RightStickClick },
        { "⧉", GamepadButton.View },
        { "≡", GamepadButton.Menu },
        { "D-Pad", GamepadButton.DPadUp },
    };

    [Theory]
    [MemberData(nameof(Buttons))]
    [Trait("TestCase", "T539-11")]
    public void The_key_named_for_a_button_does_what_the_button_does(string name, GamepadButton button)
    {
        var key = KeyNames.ByButton[name];

        Assert.Equal(UiActionMap.FromGamepad(button), UiActionMap.FromKey(key.Key, key.Modifiers));
    }

    [Fact]
    [Trait("TestCase", "T539-11")]
    public void Every_button_with_a_key_is_checked() =>
        Assert.Equal(Buttons.Select(row => row.Data.Item1).Order(), KeyNames.ByButton.Keys.Order());

    [Fact]
    [Trait("TestCase", "T539-11")]
    public void A_button_no_key_stands_for_keeps_its_badge() => Assert.Null(KeyNames.Of("Guide"));
}
