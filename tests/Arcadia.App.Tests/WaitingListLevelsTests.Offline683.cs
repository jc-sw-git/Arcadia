using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>#683: an own term in "Warten auf deine Auswahl" without internet warns at once and searches nothing.</summary>
public sealed partial class WaitingListLevelsTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T683-10")]
    public async Task Without_internet_an_own_term_warns_and_says_steamgriddb_nicht_erreichbar_at_once()
    {
        var (arcadia, list) = await OpenAsync();
        using var _ = arcadia;
        FortySagas(arcadia);
        var asked = arcadia.SteamGridDb.Count;
        arcadia.Connection.Online = false;

        var editing = EditTerm(arcadia, list);
        editing.Write("Echo Saga");
        editing.Editor.Take();
        await WaitingListTests.ShownAsync(list);

        var hits = list.Hits!;
        Assert.Equal((AppTexts.WaitingUnreachable, true, false), (hits.Message, hits.HasMessage, hits.IsChecking));
        Assert.Equal(1, arcadia.Main.Toast.History.Entries.Count(e => e.Text == AppTexts.NoInternetConnection && e.Level == HealthSeverity.Warning));
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }
}
