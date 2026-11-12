using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Abstractions;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The window's part of "Übersicht anpassen" (#500): the dialog over the detail page, and storing the quick selection at
/// once, so the page behind shows each tick.
/// </summary>
public partial class MainViewModel : ICustomizeOverviewHost
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomizeOverviewOpen))]
    public partial CustomizeOverviewViewModel? CustomizeOverview { get; private set; }

    public bool IsCustomizeOverviewOpen => CustomizeOverview is not null;

    void IGameActionsOwner.CustomizeOverview(GameTileViewModel tile, IReadOnlyList<QuickChoice> choices) =>
        CustomizeOverview = new CustomizeOverviewViewModel(tile, choices, _settings.Current.QuickSelection.Of(tile.Game.Key, DetailAreas.QuickDefaults), this);

    void ICustomizeOverviewHost.StoreQuickSelection(Game game, IReadOnlyList<string> ids) =>
        SaveQuickSelection(s => s.WithGame(game.Key, ids));

    void ICustomizeOverviewHost.ApplyQuickSelectionToAllGames(IReadOnlyList<string> ids) =>
        SaveQuickSelection(_ => QuickSelectionSettings.ForAllGames(ids));

    void ICustomizeOverviewHost.Close(CustomizeOverviewViewModel dialog)
    {
        if (CustomizeOverview == dialog)
        {
            CustomizeOverview = null;
        }
    }

    /// <summary>
    /// Holds at once, also when <c>settings.json</c> can't be written (then until Arcadia closes, as other settings); the
    /// shown page takes it over at once, whichever game it shows now.
    /// </summary>
    private void SaveQuickSelection(Func<QuickSelectionSettings, QuickSelectionSettings> change)
    {
        try
        {
            _settings.Update(s => s with { QuickSelection = change(s.QuickSelection) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Schnellauswahl nicht gespeichert: {ex.Message}");
            Toast.Show(HealthSeverity.Error, AppTexts.SettingNotSaved);
        }

        Detail?.RebuildActions();
    }
}
