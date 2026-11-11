using Arcadia.App.Input;
using Arcadia.App.ViewModels;

namespace Arcadia.App.Tests;

internal static class DetailSteps
{
    /// <summary>The row of this kind in any of the detail page's areas.</summary>
    public static DetailRowViewModel Row(this GameDetailPageViewModel page, GameActionKind kind) =>
        page.Areas.SelectMany(area => area.Rows).Single(row => row.Kind == kind);

    /// <summary>
    /// From the buttons down into the rail, down to the area <paramref name="areaKey"/>, A into its rows and down to the
    /// row of <paramref name="kind"/>, as the user would.
    /// </summary>
    public static DetailRowViewModel FocusDetailRow(this ArcadiaDriver arcadia, string areaKey, GameActionKind kind)
    {
        var page = arcadia.Detail!.Current;
        Assert.Equal(DetailZone.Buttons, page.Zone);
        arcadia.Press(UiAction.Down);
        for (var steps = 0; page.OpenArea.Key != areaKey; steps++)
        {
            Assert.True(steps < page.Areas.Count, $"Bereich {areaKey} nicht erreicht");
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        for (var steps = 0; page.FocusedRow?.Kind != kind; steps++)
        {
            Assert.True(steps < page.OpenArea.Rows.Count, $"Zeile {kind} nicht erreicht");
            arcadia.Press(UiAction.Down);
        }

        return page.FocusedRow!;
    }
}
