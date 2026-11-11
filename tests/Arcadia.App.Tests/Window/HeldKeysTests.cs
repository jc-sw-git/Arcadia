using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace Arcadia.App.Tests;

/// <summary>
/// A held key repeats as Windows repeats it, a KeyDown after another without a KeyUp, and stops at the end like a held
/// button (#476). The default library's tabs: Gog, Xbox, Amazon.
/// </summary>
public sealed class HeldKeysTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T476-9")]
    public async Task Held_Shift_E_turns_the_tabs_and_stops_at_the_last_one_until_pressed_again()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();

        arcadia.KeyDown(Key.E, KeyModifiers.Shift).KeyDown(Key.E, KeyModifiers.Shift);
        Assert.Equal(KnownLaunchers.Amazon.Id, arcadia.Arcadia.SelectedTab!.Launcher.Id);

        arcadia.KeyDown(Key.E, KeyModifiers.Shift).KeyDown(Key.E, KeyModifiers.Shift);
        Assert.Equal(KnownLaunchers.Amazon.Id, arcadia.Arcadia.SelectedTab!.Launcher.Id);

        arcadia.KeyUp(Key.E, KeyModifiers.Shift).Key(Key.E, KeyModifiers.Shift);
        Assert.Equal(KnownLaunchers.Gog.Id, arcadia.Arcadia.SelectedTab!.Launcher.Id);
    }
}
