using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Input;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>#489: the settings category "Datenquellen" as drawn (T489-1) and the library while SteamGridDB answers slowly (T489-24).</summary>
public sealed class DataSourcesViewTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T489-1")]
    [Trait("TestCase", "T489-14")]
    public async Task The_category_draws_its_groups_indents_greyed_rows_and_progress_and_a_click_moves_a_source()
    {
        using var window = await ArcadiaWindow.ShowAsync([WithStoreIds(3)]);
        var arcadia = window.Arcadia;
        await arcadia.Artwork.WhenIdleAsync();
        Open(window);

        Assert.Equal(
            [AppTexts.DataSourcesSteamGridDbGroup, AppTexts.DataSourcesOrderGroup, AppTexts.DataSourcesArtworkGroup],
            window.GroupHeadings().Select(heading => heading.Text));
        var key = window.RowButton(window.Row(SteamGridDbKeyRows.KeyRowKey));
        foreach (var indented in new[] { "steamgriddb-paste", "steamgriddb-page", "steamgriddb-test" })
        {
            Assert.True(window.Bounds(window.RowButton(window.Row(indented))).Left > window.Bounds(key).Left + 10, $"{indented} ist nicht eingerückt.");
        }

        Assert.Contains("unavailable", window.RowButton(window.Row("steamgriddb-test")).Classes);
        Assert.Contains("unavailable", window.RowButton(window.Row(ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb))).Classes);
        arcadia.FocusRow(SteamGridDbKeyRows.KeyRowKey);
        window.Settle();
        Assert.Contains(window.SettingsFooter(), hint => hint.EndsWith(AppTexts.DataSourcesKeyEnter, StringComparison.Ordinal));
        Assert.Empty(ProgressBars(window));

        // A run that waits for SteamGridDB: the missing row draws its progress bar.
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        arcadia.SteamGridDb.Delay = TimeSpan.FromSeconds(30);
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: true));
        await arcadia.WaitUntilAsync(() => arcadia.Artwork.Search.Running is not null, "Suche läuft");
        await arcadia.WaitUntilAsync(() => { window.Settle(); return ProgressBars(window).Count == 1; }, "Fortschrittsbalken");
        var bar = Assert.Single(ProgressBars(window));
        Assert.True(window.Bounds(bar).Width > 200, "Der Fortschrittsbalken ist zu schmal.");

        // A click on "2. SteamGridDB" moves it up, as A does.
        window.Click(window.RowButton(window.Row(ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb))));
        Assert.Equal([ArtworkSourceKind.SteamGridDb, ArtworkSourceKind.Launcher], arcadia.Settings.Current.Artwork.Order);
        Assert.Equal(ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb), arcadia.Main.Settings!.FocusedRow!.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T489-24")]
    public async Task While_every_steamgriddb_answer_takes_two_seconds_the_library_and_the_settings_stay_fast()
    {
        using var window = await ArcadiaWindow.ShowAsync([WithStoreIds(1000)]);
        var arcadia = window.Arcadia;
        await arcadia.Artwork.WhenIdleAsync();
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        arcadia.SteamGridDb.Delay = TimeSpan.FromSeconds(2);
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: false));
        await arcadia.WaitUntilAsync(() => arcadia.SteamGridDb.Count > 1, "SteamGridDB gefragt");

        arcadia.LatencyLines.Clear();
        for (var i = 0; i < 10; i++)
        {
            var button = i % 2 == 0 ? GamepadButton.DPadRight : GamepadButton.DPadLeft;
            arcadia.PressButton(button).ReleaseButton(button);
            window.Settle();
        }

        var moves = arcadia.LatencyLines.Select(TotalOf).Order().ToList();
        Assert.Equal(10, moves.Count);
        Assert.True(moves[moves.Count / 2] < 50, $"Fokuswechsel im Mittel {moves[moves.Count / 2]:0.0} ms, während SteamGridDB antwortet.");

        // The settings page and its progress row stay operable meanwhile.
        Open(window);
        Assert.NotNull(arcadia.Artwork.Search.Running);
        arcadia.FocusRow("artwork-crop");
        arcadia.Press(Input.UiAction.Right);
        Assert.Equal(CropAllowance.Much, arcadia.Settings.Current.Artwork.Crop);
        Assert.NotNull(arcadia.Artwork.Search.Running);
    }

    private static void Open(ArcadiaWindow window)
    {
        window.Press(Input.UiAction.Settings);
        for (var i = 0; i < 4 && window.Arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            window.Press(Input.UiAction.NextTab);
        }

        Assert.IsType<DataSourcesSettingsViewModel>(window.Arcadia.Main.Settings!.Selected);
        window.Settle().WaitForTransitions();
    }

    private static List<ProgressBar> ProgressBars(ArcadiaWindow window) =>
        [.. window.Named<ItemsControl>("SettingsRows").GetVisualDescendants().OfType<ProgressBar>().Where(bar => bar.Classes.Contains("rowProgress") && bar.IsEffectivelyVisible)];

    private static double TotalOf(string line) =>
        double.Parse(System.Text.RegularExpressions.Regex.Match(line, @"nach (?<total>\d+\.\d) ms").Groups["total"].Value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>GOG games without launcher images that SteamGridDB can be asked for by a made-up Steam id each.</summary>
    private static LauncherSpec WithStoreIds(int count) =>
        LauncherSpec.With(KnownLaunchers.Gog, [.. Enumerable.Range(1, count).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:D4}", DateTimeOffset.UnixEpoch))])
            with { Artwork = new StoreIds() };

    private sealed class StoreIds : ILauncherArtwork, IArtworkLookup
    {
        public IArtworkLookup Look() => this;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => [];

        public ArtworkStoreId? StoreIdOf(Game game) => new(ArtworkStoreId.Steam, "42" + Math.Abs(game.Id.GetHashCode(StringComparison.Ordinal)));
    }
}
