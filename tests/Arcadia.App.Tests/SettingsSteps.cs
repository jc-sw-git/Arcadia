using Arcadia.App.Input;
using Arcadia.App.Settings;

namespace Arcadia.App.Tests;

internal static class SettingsSteps
{
    /// <summary>Up or down with the D-pad to the row <paramref name="key"/> of the shown category, as the user would.</summary>
    public static SettingsRowViewModel FocusRow(this ArcadiaDriver arcadia, string key)
    {
        var settings = arcadia.Main.Settings!;
        var rows = settings.Selected.Rows;
        var target = rows.Select(row => row.Key).ToList().IndexOf(key);
        Assert.True(target >= 0, $"Zeile {key} nicht gefunden");
        for (var steps = 0; settings.FocusedRow?.Key != key; steps++)
        {
            Assert.True(steps < rows.Count, $"Zeile {key} nicht erreicht");
            arcadia.Press(rows.IndexOf(settings.FocusedRow!) < target ? UiAction.Down : UiAction.Up);
        }

        return settings.FocusedRow!;
    }
}
