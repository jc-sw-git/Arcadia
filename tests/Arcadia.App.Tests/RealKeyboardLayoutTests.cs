using Arcadia.App.Input;
using Arcadia.Input;
using Avalonia.Input;

namespace Arcadia.App.Tests;

/// <summary>
/// The keyboard controls follow the player's real layout (#588): keys count by their place, hints show the letter
/// printed there. The layouts are made-up tables of what Windows reports, by QWERTY place.
/// </summary>
[Collection(RealKeyboardLayoutTests.Name)]
public sealed class RealKeyboardLayoutTests : IDisposable
{
    public const string Name = "Keyboard layout";

    // French AZERTY: A↔Q and Z↔W swapped, M's place prints a comma (no letter).
    private static readonly Dictionary<char, char> Azerty = new() { ['A'] = 'q', ['Q'] = 'a', ['W'] = 'z', ['Z'] = 'w', ['M'] = ',' };

    // German QWERTZ: only Y and Z are swapped, none of the cluster's keys.
    private static readonly Dictionary<char, char> Qwertz = new() { ['Y'] = 'z', ['Z'] = 'y' };

    public void Dispose() => KeyLabels.Use(UsQwerty);

    [Theory]
    [Trait("TestCase", "T588-1")]
    [InlineData(Key.Z, PhysicalKey.W, UiAction.Up)]
    [InlineData(Key.Q, PhysicalKey.A, UiAction.Left)]
    [InlineData(Key.S, PhysicalKey.S, UiAction.Down)]
    [InlineData(Key.D, PhysicalKey.D, UiAction.Right)]
    [InlineData(Key.A, PhysicalKey.Q, UiAction.PreviousPage)]
    [InlineData(Key.E, PhysicalKey.E, UiAction.NextPage)]
    [InlineData(Key.F, PhysicalKey.F, UiAction.Search)]
    [InlineData(Key.V, PhysicalKey.V, UiAction.Status)]
    [InlineData(Key.Space, PhysicalKey.Space, UiAction.Accept)]
    [InlineData(Key.Escape, PhysicalKey.Escape, UiAction.Back)]
    public void On_azerty_the_keys_act_by_their_place(Key printed, PhysicalKey place, UiAction expected) =>
        Assert.Equal(expected, UiActionMap.FromKey(KeyPosition.Of(printed, place), KeyModifiers.None));

    [Fact]
    [Trait("TestCase", "T588-1")]
    public void On_azerty_the_key_printed_w_does_nothing_and_shift_a_turns_the_tab()
    {
        Assert.Null(UiActionMap.FromKey(KeyPosition.Of(Key.W, PhysicalKey.Z), KeyModifiers.None));
        Assert.Equal(UiAction.PreviousTab, UiActionMap.FromKey(KeyPosition.Of(Key.A, PhysicalKey.Q), KeyModifiers.Shift));
    }

    [Fact]
    [Trait("TestCase", "T588-1")]
    public void On_azerty_the_hints_show_the_letters_printed_there()
    {
        KeyLabels.Use(letter => Azerty.TryGetValue(letter, out var shown) ? shown : char.ToLowerInvariant(letter));

        Assert.Equal("ZQSD", KeyLabels.Printed(KeyNames.Of("D-Pad")!));
        Assert.Equal("A", KeyLabels.Printed(KeyNames.Of("LT")!));
        Assert.Equal("E", KeyLabels.Printed(KeyNames.Of("RT")!));
        Assert.Equal("⇧ A", KeyLabels.Printed(KeyNames.Of("LB")!));
        Assert.Equal("Z|S", KeyLabels.Printed("W|S"));
        Assert.Equal("M", KeyLabels.Printed("M"));
    }

    [Fact]
    [Trait("TestCase", "T588-2")]
    public void Qwertz_and_qwerty_keep_todays_keys_and_hints()
    {
        KeyLabels.Use(letter => Qwertz.TryGetValue(letter, out var shown) ? shown : letter);
        Assert.Equal("WASD", KeyLabels.Printed("WASD"));
        Assert.Equal("⇧ Q", KeyLabels.Printed("⇧ Q"));

        KeyLabels.Use(UsQwerty);
        Assert.Equal("WASD", KeyLabels.Printed("WASD"));
        foreach (var button in KeyNames.ByButton.Values)
        {
            Assert.Equal(button.Key, KeyPosition.Of(button.Key, PhysicalKeyAt(button.Key)));
        }
    }

    [Fact]
    [Trait("TestCase", "T588-3")]
    public void A_layout_change_updates_every_hint_once()
    {
        var changes = 0;
        void Count(object? sender, EventArgs e) => changes++;
        InputHints.Changed += Count;
        try
        {
            KeyLabels.Use(letter => Azerty.TryGetValue(letter, out var shown) ? shown : letter);
            KeyLabels.Use(letter => Azerty.TryGetValue(letter, out var shown) ? shown : letter);
            Assert.Equal(1, changes);

            KeyLabels.Use(UsQwerty);
            Assert.Equal(2, changes);
        }
        finally
        {
            InputHints.Changed -= Count;
        }
    }

    [Fact]
    [Trait("TestCase", "T588-4")]
    public void Key_names_and_controller_buttons_in_a_text_stay_as_they_are()
    {
        KeyLabels.Use(letter => Azerty.TryGetValue(letter, out var shown) ? shown : letter);

        Assert.Equal(AppTexts.KeySpace, KeyLabels.Printed(AppTexts.KeySpace));
        Assert.Equal("Esc", KeyLabels.Printed("Esc"));
        Assert.Equal("⌫", KeyLabels.Printed("⌫"));
        Assert.Equal("Vorherige Seite (A, Bild ↑ oder Umschalt+←)", KeyLabels.PrintedInBrackets("Vorherige Seite (Q, Bild ↑ oder Umschalt+←)"));
        Assert.Equal("Vorheriger Launcher (Umschalt+A)", KeyLabels.PrintedInBrackets("Vorheriger Launcher (Umschalt+Q)"));
        Assert.Equal("Filter und Sortierung (L3)", KeyLabels.PrintedInBrackets("Filter und Sortierung (L3)"));
        Assert.Equal("Weitere Treffer (RT, Bild ↓, Mausrad)", KeyLabels.PrintedInBrackets("Weitere Treffer (RT, Bild ↓, Mausrad)"));
        Assert.Equal("Abbrechen (B)", KeyLabels.PrintedInBrackets("Abbrechen (B)"));
    }

    [Fact]
    [Trait("TestCase", "T588-5")]
    public void A_key_that_prints_no_letter_keeps_its_qwerty_letter_and_its_place()
    {
        KeyLabels.Use(letter => letter == 'F' ? null : letter == 'G' ? '´' : letter);

        Assert.Equal("F", KeyLabels.Printed("F"));
        Assert.Equal("G", KeyLabels.Printed("G"));
        Assert.Equal(UiAction.Search, UiActionMap.FromKey(KeyPosition.Of(Key.None, PhysicalKey.F), KeyModifiers.None));
    }

    [Fact]
    [Trait("TestCase", "T588-6")]
    public void The_digit_row_picks_the_launcher_tab_by_its_place()
    {
        // AZERTY's digit row prints & é " … unshifted; the place still counts.
        Assert.Equal(0, UiActionMap.TabNumber(KeyPosition.Of(Key.None, PhysicalKey.Digit1), KeyModifiers.None));
        Assert.Equal(9, UiActionMap.TabNumber(KeyPosition.Of(Key.None, PhysicalKey.Digit0), KeyModifiers.None));
    }

    private static char? UsQwerty(char letter) => letter;

    private static PhysicalKey PhysicalKeyAt(Key key) => key switch
    {
        >= Key.A and <= Key.Z => PhysicalKey.A + (key - Key.A),
        Key.Space => PhysicalKey.Space,
        Key.Escape => PhysicalKey.Escape,
        Key.Enter => PhysicalKey.Enter,
        _ => PhysicalKey.None,
    };
}

[CollectionDefinition(RealKeyboardLayoutTests.Name, DisableParallelization = true)]
public sealed class RealKeyboardLayoutCollection;
