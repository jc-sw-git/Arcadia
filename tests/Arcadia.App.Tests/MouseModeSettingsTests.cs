using System.Text.RegularExpressions;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The groups "Mausmodus" and "Zurück zu Arcadia" in the category "Controller" (#64, #110).</summary>
public sealed class MouseModeSettingsTests
{
    private static readonly string[] Seconds = ["1 Sekunde", .. Enumerable.Range(2, 8).Select(n => $"{n} Sekunden")];

    [AvaloniaFact]
    [Trait("Testfall", "T64-7")]
    public async Task Mausmodus_hold_and_speed_change_with_the_controller_apply_at_once_and_stay_after_a_restart()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenController(arcadia);
        var mode = arcadia.FocusRow("mouse-mode");

        // Von Hand (default) → Automatisch → Aus → Von Hand, with ▶; ◀ goes back; A counts on like ▶.
        Assert.Equal("Von Hand", ValueOf(mode));
        Assert.StartsWith("Durch Halten von ⧉ + ≡ wechselst du in den Mausmodus.", FirstText(mode));
        arcadia.Press(UiAction.Right);
        Assert.Equal(MouseModeActivation.Automatic, arcadia.Settings.Current.MouseMode.Activation);
        Assert.StartsWith("Bei „Automatisch“ schaltet Arcadia automatisch in den Mausmodus", FirstText(mode));
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Aus", ValueOf(mode));
        Assert.Equal("Der Controller bleibt immer ein Controller.", FirstText(mode));
        arcadia.Press(UiAction.Left).Press(UiAction.Left);
        Assert.Equal("Von Hand", ValueOf(mode));
        Assert.Equal(MouseModeActivation.Manual, arcadia.Settings.Current.MouseMode.Activation);

        // Haltedauer from 3 to 1 with ◀, A counts up and after 9 starts again at 1.
        var hold = arcadia.FocusRow("mouse-mode-hold");
        arcadia.Press(UiAction.Left, times: 2);
        Assert.Equal(1, arcadia.Settings.Current.MouseMode.HoldSeconds);
        arcadia.Press(UiAction.Accept, times: 8);
        Assert.Equal("9 Sekunden", ValueOf(hold));
        Assert.Equal(9, arcadia.Settings.Current.MouseMode.HoldSeconds);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(1, arcadia.Settings.Current.MouseMode.HoldSeconds);
        arcadia.Press(UiAction.Left);
        Assert.Equal(9, arcadia.Settings.Current.MouseMode.HoldSeconds);

        // Zeigergeschwindigkeit to 1 and 9.
        var speed = arcadia.FocusRow("mouse-mode-speed");
        var start = arcadia.Settings.Current.MouseMode.PointerSpeed;
        arcadia.Press(UiAction.Left, times: start - 1);
        Assert.Equal("1", ValueOf(speed));
        Assert.Equal(1, arcadia.Settings.Current.MouseMode.PointerSpeed);
        arcadia.Press(UiAction.Right, times: 8);
        Assert.Equal(9, arcadia.Settings.Current.MouseMode.PointerSpeed);
        arcadia.Press(UiAction.Left, times: 4);

        arcadia.Press(UiAction.Back);
        using var restarted = await arcadia.RestartAsync();
        OpenController(restarted);
        Assert.Equal("Von Hand", ValueOf(restarted.FocusRow("mouse-mode")));
        Assert.Equal("9 Sekunden", ValueOf(restarted.FocusRow("mouse-mode-hold")));
        Assert.Equal("5", ValueOf(restarted.FocusRow("mouse-mode-speed")));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-8")]
    [Trait("Testfall", "T110-9")]
    public async Task So_funktioniert_es_shows_the_buttons_and_the_limits()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenController(arcadia);
        var help = HelpOf(arcadia.FocusRow("mouse-mode"));

        var keys = help.Blocks.OfType<InfoKey>().ToDictionary(key => key.Input, key => key.Effect);
        Assert.Equal("Mauszeiger", keys["LS"]);
        Assert.Equal("Linke Maustaste (gedrückt halten = ziehen)", keys["RT oder A"]);
        Assert.Equal("Rechte Maustaste", keys["LT oder X"]);
        Assert.Equal("Esc", keys["B"]);
        Assert.Equal("Bildschirmtastatur (B blendet sie aus)", keys["Y"]);
        Assert.Contains(help.Blocks, block => block is InfoHeading { Text: "GRENZEN" });
        var limits = help.Blocks.OfType<InfoBullet>().Select(bullet => bullet.Text).ToList();
        Assert.Contains(limits, limit => limit.Contains("Administratorrechten (UAC)", StringComparison.Ordinal));
        Assert.Contains(limits, limit => limit.Contains("als Administrator laufen", StringComparison.Ordinal));
        Assert.Contains(limits, limit => limit.Contains("exklusiven Vollbild", StringComparison.Ordinal) && limit.Contains("Bildschirmtastatur", StringComparison.Ordinal));

        // B closes the settings from there, not a dialog: the help is a box in the info panel since #15.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T110-9")]
    public async Task Zurueck_zu_Arcadia_holds_from_1_to_9_seconds_without_Sofort()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenController(arcadia);
        var hold = arcadia.FocusRow("return-with-view-hold");

        var shown = new List<string>();
        for (var i = 0; i < 9; i++)
        {
            shown.Add(ValueOf(hold));
            arcadia.Press(UiAction.Right);
        }

        Assert.Equal(Seconds, shown.Order(Comparer<string>.Create((a, b) => Array.IndexOf(Seconds, a) - Array.IndexOf(Seconds, b))));
        Assert.Equal(shown[0], ValueOf(hold));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-12")]
    public async Task Mausmodus_Aus_dims_the_other_rows_which_keep_their_values_for_when_it_comes_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenController(arcadia);
        arcadia.FocusRow("mouse-mode-speed");
        arcadia.Press(UiAction.Right);
        var before = arcadia.Settings.Current.MouseMode;
        var group = arcadia.Main.Settings!.Selected.Rows.Where(row => row.Group == "MAUSMODUS").ToList();
        Assert.Equal(7, group.Count);
        var shownBefore = group.OfType<ChoiceRowViewModel>().Select(row => row.ValueText).ToList();

        arcadia.FocusRow("mouse-mode");
        arcadia.Press(UiAction.Left);
        Assert.Equal(MouseModeActivation.Off, arcadia.Settings.Current.MouseMode.Activation);
        Assert.All(group.Skip(1), row => Assert.True(row.IsDimmed, $"„{row.Title}“ ist abgeblendet."));
        Assert.False(group[0].IsDimmed);
        Assert.Equal(shownBefore.Skip(1), group.OfType<ChoiceRowViewModel>().Skip(1).Select(row => row.ValueText));
        Assert.Equal(before with { Activation = MouseModeActivation.Off }, arcadia.Settings.Current.MouseMode);

        arcadia.Press(UiAction.Right);
        Assert.Equal(before, arcadia.Settings.Current.MouseMode);
        Assert.False(group.Single(row => row.Key == "mouse-mode-speed").IsDimmed);
        Assert.Equal(shownBefore, group.OfType<ChoiceRowViewModel>().Select(row => row.ValueText));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T64-15")]
    public async Task The_mouse_mode_boxes_name_View_and_Menu_by_their_symbols_only()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenController(arcadia);
        var rows = arcadia.Main.Settings!.Selected.Rows.Where(row => row.Group is "MAUSMODUS" or "ZURÜCK ZU ARCADIA").ToList();
        Assert.Equal(9, rows.Count);

        var texts = rows.SelectMany(ShownText.Of).ToList();
        Assert.DoesNotContain(texts, text => Regex.IsMatch(text, @"\b(Back|Start|View|Menu)\b"));
        Assert.Contains(texts, text => text.Contains("⧉ + ≡", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains("⧉ gedrückt halten", StringComparison.Ordinal));

        // The table of "So funktioniert es" names the other buttons too.
        var inputs = string.Join(' ', HelpOf(rows[0]).Blocks.OfType<InfoKey>().Select(key => key.Input));
        Assert.All(["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "LS", "RS", "⧉", "≡"], button =>
            Assert.Matches($@"(^|\s){Regex.Escape(button)}(\s|$)", inputs));
    }

    private static InfoSection HelpOf(SettingsRowViewModel mode) =>
        mode.Info.OfType<InfoSection>().Single(section => section.Title == "SO FUNKTIONIERT ES");

    private static string ValueOf(SettingsRowViewModel row) => Assert.IsAssignableFrom<ChoiceRowViewModel>(row).ValueText;

    private static string FirstText(SettingsRowViewModel row) => Assert.IsType<InfoText>(row.Info[0]).Text;

    private static void OpenController(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        Assert.Equal("Controller", arcadia.Main.Settings!.Selected.Name);
    }
}
