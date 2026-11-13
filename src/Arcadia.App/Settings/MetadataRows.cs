using Arcadia.App.Services;
using Arcadia.Core.Metadata;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "METADATEN" of the tab "Metadaten" (#493): "Fehlende Metadaten" with the run's progress and its time left,
/// and "Jetzt suchen", which asks for what is missing without a scan (#538); "Alle Metadaten neu laden", which asks first
/// and starts no second run. What each launcher delivers is in the info box of "Launcher-Daten" (<see cref="MetadataSourceRows"/>).
/// </summary>
public sealed class MetadataRows
{
    public const string MissingKey = "metadata-missing";

    public const string ReloadKey = "metadata-reload";

    private readonly MetadataService _metadata;
    private readonly Action<SettingsQuestionViewModel?> _ask;
    private readonly ActionRowViewModel _missing;
    private readonly ActionRowViewModel _reload;

    // "Jetzt suchen" was pressed and the run's progress hasn't changed since: the row says it works at once.
    private MetadataProgress? _progressAtAsk;

    /// <param name="ask">Shows the question over the page (the category's dialog), or null to close it.</param>
    public MetadataRows(MetadataService metadata, Action<SettingsQuestionViewModel?> ask)
    {
        _metadata = metadata;
        _ask = ask;
        _missing = new ActionRowViewModel(MissingKey, AppTexts.DataSourcesMetadataMissing, SearchNow)
        {
            Group = AppTexts.DataSourcesMetadataGroup,
            ActionText = AppTexts.DataSourcesSearchNow,
            Info = [new InfoText(AppTexts.DataSourcesMetadataMissingInfo), new InfoText(AppTexts.DataSourcesMetadataMissingInfoNow)],
        };
        _reload = new ActionRowViewModel(ReloadKey, AppTexts.DataSourcesMetadataReload, Ask)
        {
            Group = AppTexts.DataSourcesMetadataGroup,
            ActionText = AppTexts.DataSourcesMetadataReloadAction,
            Subtitle = AppTexts.DataSourcesMetadataReloadSubtitle,
            Info = [new InfoText(AppTexts.DataSourcesMetadataReloadInfo)],
        };
        Refresh();
    }

    public IReadOnlyList<SettingsRowViewModel> Rows => [_missing, _reload];

    /// <summary>A run or the libraries changed; UI thread.</summary>
    public void Refresh()
    {
        var progress = _metadata.Progress;
        if (!ReferenceEquals(progress, _progressAtAsk))
        {
            _progressAtAsk = null;
        }

        var searching = progress.Running || _progressAtAsk is not null;
        _missing.Subtitle = MissingText(progress);
        // Greyed when nothing is missing ("Alle Metadaten geladen"); while a run goes, the row works instead.
        _missing.IsEnabled = !searching && progress.Missing > 0;
        _missing.IsDimmed = !searching && progress.Missing == 0;
        _missing.IsWorking = searching;
        _missing.Progress = progress is { Running: true, Total: > 0 } ? 100.0 * (progress.Total - progress.Missing) / progress.Total : null;

        var reloading = progress.Reload;
        _reload.IsEnabled = !reloading && _metadata.GameCount > 0;
        _reload.IsDimmed = !_reload.IsEnabled;
        _reload.IsWorking = reloading;
        _reload.Subtitle = reloading ? AppTexts.DataSourcesMetadataReloadRunning : AppTexts.DataSourcesMetadataReloadSubtitle;
    }

    /// <summary>"412 von 1.000, noch etwa 20 Minuten" while a run goes, the counts alone while none does.</summary>
    public static string MissingText(MetadataProgress progress) => progress switch
    {
        { Total: 0 } => AppTexts.DataSourcesMetadataMissingNotYet,
        { Missing: 0 } => AppTexts.DataSourcesMetadataMissingNone,
        { Remaining: { } left } when left < TimeSpan.FromMinutes(1) => AppTexts.DataSourcesMetadataMissingUnderAMinute(missing: Formatting.Count(progress.Missing), total: Formatting.Count(progress.Total)),
        { Remaining: { } left } when left < TimeSpan.FromMinutes(1.5) => AppTexts.DataSourcesMetadataMissingOneMinute(missing: Formatting.Count(progress.Missing), total: Formatting.Count(progress.Total)),
        { Remaining: { } left } => AppTexts.DataSourcesMetadataMissingMinutes(missing: Formatting.Count(progress.Missing), total: Formatting.Count(progress.Total), minutes: Formatting.Count((int)Math.Round(left.TotalMinutes))),
        _ => AppTexts.DataSourcesMetadataMissingCount(missing: Formatting.Count(progress.Missing), total: Formatting.Count(progress.Total)),
    };

    /// <summary>
    /// A on "Fehlende Metadaten": the stores and IGDB are asked for what is missing now, as after a scan; a game that failed
    /// for want of a network is asked again. Nothing starts before the first scan or while a run goes.
    /// </summary>
    private void SearchNow()
    {
        var progress = _metadata.Progress;
        if (progress.Running || _progressAtAsk is not null || !_metadata.FetchMissing())
        {
            return;
        }

        _progressAtAsk = progress;
        Refresh();
    }

    /// <summary>A on "Alle Metadaten neu laden": the question first; [A] starts the run, [B] or "Abbrechen" sends nothing.</summary>
    private void Ask()
    {
        if (_metadata.Progress.Reload)
        {
            return;
        }

        _ask(new SettingsQuestionViewModel(
            AppTexts.DataSourcesMetadataReloadAsk,
            AppTexts.DataSourcesMetadataReloadAskText(Formatting.Count(_metadata.GameCount)),
            AppTexts.DataSourcesMetadataReloadAction,
            () =>
            {
                _ask(null);
                _metadata.ReloadAll();
                Refresh();
            },
            () => _ask(null)));
    }
}
