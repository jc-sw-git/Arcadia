using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Tests.Artwork;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #683 in "Datenquellen", tab "Artwork": without internet "Jetzt suchen" and "Alle Artworks neu laden" warn at once and
/// start nothing; a connection lost during the player's search ends it with the same warning.
/// </summary>
public sealed partial class DataSourcesTests
{
    private static int NoInternetToasts(ArcadiaDriver arcadia) =>
        arcadia.Main.Toast.History.Entries.Count(e => e.Text == AppTexts.NoInternetConnection && e.Level == HealthSeverity.Warning);

    [AvaloniaFact]
    [Trait("TestCase", "T683-6")]
    public async Task Without_internet_Jetzt_suchen_and_Alle_Artworks_neu_laden_warn_at_once_and_start_nothing()
    {
        var (arcadia, images) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await SettledAsync(arcadia);
        var asked = arcadia.SteamGridDb.Count;
        var downloads = images.Requests.Count;
        arcadia.Connection.Online = false;

        var missing = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ArtworkSourceRows.MissingKey));
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);
        Assert.Equal(1, NoInternetToasts(arcadia));
        Assert.Equal((AppTexts.DataSourcesSearchNow, false, false), (missing.ActionText, missing.IsWorking, missing.HasProgress));

        var reload = Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(ArtworkReloadRows.ReloadKey));
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);
        Assert.Equal(2, NoInternetToasts(arcadia));
        Assert.Equal((AppTexts.DataSourcesReloadAction, false), (reload.ActionText, reload.IsWorking));

        Assert.Equal((asked, downloads), (arcadia.SteamGridDb.Count, images.Requests.Count));

        // Back online, the same press searches.
        arcadia.Connection.Online = true;
        arcadia.FocusRow(ArtworkSourceRows.MissingKey);
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);
        Assert.True(arcadia.SteamGridDb.Count > asked);
        Assert.Equal(2, NoInternetToasts(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T683-7")]
    public async Task A_connection_lost_during_Jetzt_suchen_ends_it_with_the_same_warning_and_not_the_steamgriddb_one()
    {
        var (arcadia, _) = await StartAsync();
        using var _ = arcadia;
        OpenDataSources(arcadia);
        await TypeKeyAsync(arcadia, FakeSteamGridDb.Key);
        await SettledAsync(arcadia);

        // The check before the search still finds the internet; then SteamGridDB fails and the check after it doesn't.
        arcadia.SteamGridDb.Offline = true;
        arcadia.Connection.Next.Enqueue(true);
        arcadia.Connection.Online = false;
        arcadia.FocusRow(ArtworkSourceRows.MissingKey);
        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);
        await arcadia.WaitUntilAsync(() => NoInternetToasts(arcadia) == 1, "Warnung");

        Assert.True(arcadia.Artwork.Search.Result!.Offline);
        Assert.DoesNotContain(arcadia.Main.Toast.History.Entries, e => e.Text == AppTexts.DataSourcesSearchNoConnection);
    }
}
