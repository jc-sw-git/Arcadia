using Arcadia.Core.Artwork;

namespace Arcadia.App.Settings;

/// <summary>
/// "Alle Artworks neu laden" and "Von Hand gesetzte Artworks überschreiben" in the group "ARTWORK" of "Datenquellen" (#13,
/// #490): the reload asks first, since images set by hand go too while the switch is on; it is greyed out while a search
/// runs, and while it runs itself it reads "Neu laden abbrechen" (#506). Its progress shows in "Fehlende Cover und
/// Hintergründe" (<see cref="ArtworkSourceRows"/>).
/// </summary>
public sealed class ArtworkReloadRows
{
    public const string ReloadKey = "artwork-reload";

    public const string OverwriteKey = "artwork-overwrite";

    private readonly ArtworkService _artwork;
    private readonly SettingsChanges _changes;
    private readonly Action<SettingsQuestionViewModel?> _ask;
    private readonly ActionRowViewModel _reload;
    private readonly SwitchRowViewModel _overwrite;

    // "Neu laden" was confirmed and its run hasn't shown yet: the button stays greyed out meanwhile.
    private bool _reloadAsked;

    /// <param name="ask">Shows the question over the page (the category's dialog), or null to close it.</param>
    public ArtworkReloadRows(ArtworkService artwork, SettingsChanges changes, Action<SettingsQuestionViewModel?> ask)
    {
        _artwork = artwork;
        _changes = changes;
        _ask = ask;
        _reload = new ActionRowViewModel(ReloadKey, AppTexts.DataSourcesReload, AskOrCancel)
        {
            Group = AppTexts.DataSourcesArtworkGroup,
            ActionText = AppTexts.DataSourcesReloadAction,
            Info = [new InfoText(AppTexts.DataSourcesReloadInfo)],
        };
        _overwrite = new SwitchRowViewModel(
            OverwriteKey,
            AppTexts.DataSourcesOverwrite,
            () => changes.Current.OverwritesArtworkSetByHand,
            on =>
            {
                changes.Apply(s => s with { OverwritesArtworkSetByHand = on }, AppTexts.DataSourcesOverwrite);
                Refresh();
            })
        {
            // It only matters for the reload, so it sits right under it, indented (#560).
            Group = AppTexts.DataSourcesArtworkGroup,
            Indent = 1,
            Subtitle = AppTexts.DataSourcesOverwriteSubtitle,
            ValueInfos = [AppTexts.DataSourcesOverwriteOff, AppTexts.DataSourcesOverwriteOn],
            Info = [new InfoText(AppTexts.DataSourcesOverwriteInfo)],
        };
        Refresh();
    }

    public SettingsRowViewModel ReloadRow => _reload;

    public SettingsRowViewModel OverwriteRow => _overwrite;

    /// <summary>The switch or a run changed; UI thread.</summary>
    public void Refresh()
    {
        if (_artwork.Search.Running is not null)
        {
            _reloadAsked = false;
        }

        var reloading = _artwork.Search.Running is { Reload: true } || _reloadAsked;
        var searching = _artwork.Search.Running is { Reload: false };
        _reload.IsEnabled = !searching;
        _reload.IsDimmed = searching;
        _reload.IsWorking = reloading;
        _reload.ActionText = reloading ? AppTexts.DataSourcesReloadCancel : AppTexts.DataSourcesReloadAction;
        _reload.Subtitle = _changes.Current.OverwritesArtworkSetByHand ? AppTexts.DataSourcesReloadReplaces : AppTexts.DataSourcesReloadKeeps;
        _overwrite.Refresh();
    }

    /// <summary>A on "Alle Artworks neu laden": the question first, its text following the switch; while it runs, it stops.</summary>
    private void AskOrCancel()
    {
        if (_artwork.Search.Running is { Reload: true } || _reloadAsked)
        {
            _artwork.CancelSearch();
            _reloadAsked = false;
            Refresh();
            return;
        }

        if (_artwork.Search.Running is not null)
        {
            return;
        }

        var overwrite = _changes.Current.OverwritesArtworkSetByHand;
        var count = _artwork.GameCount;
        _ask(new SettingsQuestionViewModel(
            AppTexts.DataSourcesReloadAsk,
            overwrite ? AppTexts.DataSourcesReloadAskReplaces(count) : AppTexts.DataSourcesReloadAskKeeps(count),
            AppTexts.DataSourcesReloadAction,
            () =>
            {
                _ask(null);
                _reloadAsked = _artwork.ReloadAll(_changes.Current.OverwritesArtworkSetByHand);
                Refresh();
            },
            () => _ask(null)));
    }
}
