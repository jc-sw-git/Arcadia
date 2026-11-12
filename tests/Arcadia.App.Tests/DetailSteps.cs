using Arcadia.App.Input;
using Arcadia.App.ViewModels;

namespace Arcadia.App.Tests;

internal static class DetailSteps
{
    /// <summary>The row of this kind in any of the detail page's areas, not its entry in the quick selection (#500).</summary>
    public static DetailRowViewModel Row(this GameDetailPageViewModel page, GameActionKind kind) =>
        page.Areas.Where(area => !area.IsOverview).SelectMany(area => area.Rows).Single(row => row.Kind == kind);

    /// <summary>The area of this key, e.g. <see cref="DetailAreaViewModel.InfosKey"/>.</summary>
    public static DetailAreaViewModel Area(this GameDetailPageViewModel page, string key) => page.Areas.Single(area => area.Key == key);

    /// <summary>The quick selection's entries in "Übersicht" (#500), by kind.</summary>
    public static IEnumerable<GameActionKind> QuickKinds(this GameDetailPageViewModel page) => page.Overview.Rows.Select(row => row.Kind);

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
