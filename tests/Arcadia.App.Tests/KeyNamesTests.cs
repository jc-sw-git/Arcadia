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

    /// <summary>
    /// A view's hint names its key by hand (<c>Pad="LB" Keys="⇧ Q"</c>); each such key is the one <see cref="KeyNames"/> gives,
    /// so a key named in a text and on a badge can't drift apart. A "/" in Keys gives a key next to it (e.g. "Klick").
    /// </summary>
    [Fact]
    [Trait("TestCase", "T539-11")]
    public void Every_key_a_view_names_for_a_button_is_the_one_KeyNames_gives()
    {
        var views = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var pair = new System.Text.RegularExpressions.Regex("Pad=\"(?<pad>[^\"{]+)\"\\s+Keys=\"(?<keys>[^\"{]+)\"");
        var wrong = new List<string>();
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (System.Text.RegularExpressions.Match match in pair.Matches(File.ReadAllText(file)))
            {
                var pads = match.Groups["pad"].Value.Split('|');
                var keys = match.Groups["keys"].Value.Split('|');
                for (var i = 0; i < pads.Length && i < keys.Length; i++)
                {
                    if (KeyNames.Of(pads[i]) is { } key && keys[i].Split(" / ")[0] != key && !OwnKeyInContext.Contains((pads[i], keys[i])))
                    {
                        wrong.Add($"{Path.GetFileName(file)}: {pads[i]} = {keys[i]}, KeyNames: {key}");
                    }
                }
            }
        }

        Assert.Empty(wrong);
    }

    /// <summary>While a text is written the real keyboard types, so X (erase on the on-screen keyboard) is ⌫ there, not F.</summary>
    private static readonly HashSet<(string Pad, string Key)> OwnKeyInContext = [("X", "⌫")];

    // Found from this source file, not from the test binaries: CI runs each shard from a copy of them outside the repo.
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Arcadia.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException($"Arcadia.slnx not found above {thisFile}.");
    }
}
