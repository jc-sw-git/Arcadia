using Arcadia.Core.Artwork;

namespace Arcadia.App.Settings;

/// <summary>
/// "Alle Artworks neu laden" and "Von Hand gesetzte Artworks überschreiben" in the group "ARTWORK" of "Datenquellen" (#13,
/// #490): the reload asks first, since images set by hand go too while the switch is on; it is greyed out while a search
/// or a reload runs. Its progress shows in "Fehlende Cover und Hintergründe" (<see cref="ArtworkSourceRows"/>).
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
        _reload = new ActionRowViewModel(ReloadKey, AppTexts.DataSourcesReload, Ask)
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
            Group = AppTexts.DataSourcesArtworkGroup,
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

        var running = _artwork.Search.Running is not null || _reloadAsked;
        _reload.IsEnabled = !running;
        _reload.IsDimmed = running;
        _reload.Subtitle = _changes.Current.OverwritesArtworkSetByHand ? AppTexts.DataSourcesReloadReplaces : AppTexts.DataSourcesReloadKeeps;
        _overwrite.Refresh();
    }

    /// <summary>A on "Alle Artworks neu laden": the question first, its text following the switch.</summary>
    private void Ask()
    {
        if (_artwork.Search.Running is not null || _reloadAsked)
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
