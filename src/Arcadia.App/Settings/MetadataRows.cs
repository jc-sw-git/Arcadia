using Arcadia.App.Services;
using Arcadia.Core.Metadata;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "METADATEN" of "Datenquellen" (#493): a row per launcher that delivers metadata, saying what it delivers
/// and, in its info text, where it comes from, the store's limit and what the store learns; "Fehlende Metadaten" with
/// the run's progress and its time left; "Alle Metadaten neu laden", which asks first and starts no second run.
/// </summary>
public sealed class MetadataRows
{
    public const string MissingKey = "metadata-missing";

    public const string ReloadKey = "metadata-reload";

    private readonly MetadataService _metadata;
    private readonly Action<SettingsQuestionViewModel?> _ask;
    private readonly InfoRowViewModel _missing;
    private readonly ActionRowViewModel _reload;
    private readonly Dictionary<string, InfoRowViewModel> _providers = new(StringComparer.Ordinal);

    // "Neu laden" was confirmed and its run hasn't shown yet: the button stays greyed out meanwhile.
    private bool _reloadAsked;

    /// <param name="ask">Shows the question over the page (the category's dialog), or null to close it.</param>
    public MetadataRows(MetadataService metadata, Action<SettingsQuestionViewModel?> ask)
    {
        _metadata = metadata;
        _ask = ask;
        _missing = new InfoRowViewModel(MissingKey, AppTexts.DataSourcesMetadataMissing)
        {
            Group = AppTexts.DataSourcesMetadataGroup,
            Info = [new InfoText(AppTexts.DataSourcesMetadataMissingInfo)],
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

    /// <summary>The group's rows; none while no launcher of the libraries delivers metadata.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => ProviderRows().ToList() is { Count: > 0 } providers ? [.. providers, _missing, _reload] : [];

    /// <summary>A run or the libraries changed; UI thread.</summary>
    public void Refresh()
    {
        var progress = _metadata.Progress;
        if (progress.Reload)
        {
            _reloadAsked = false;
        }

        _missing.Subtitle = MissingText(progress);
        _missing.IsDimmed = progress is { Missing: 0, Total: > 0 };
        _missing.Progress = progress is { Running: true, Total: > 0 } ? 100.0 * (progress.Total - progress.Missing) / progress.Total : null;

        var reloading = progress.Reload || _reloadAsked;
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

    private IEnumerable<SettingsRowViewModel> ProviderRows()
    {
        foreach (var (launcher, source) in _metadata.Providers)
        {
            if (!_providers.TryGetValue(launcher.Id, out var row))
            {
                _providers[launcher.Id] = row = new InfoRowViewModel("metadata-provider-" + launcher.Id, launcher.DisplayName)
                {
                    Group = AppTexts.DataSourcesMetadataGroup,
                };
            }

            row.Title = launcher.DisplayName;
            row.Subtitle = source.Summary;
            row.Info = [new InfoText(source.About)];
            yield return row;
        }
    }

    /// <summary>A on "Alle Metadaten neu laden": the question first; [A] starts the run, [B] or "Abbrechen" sends nothing.</summary>
    private void Ask()
    {
        if (_metadata.Progress.Reload || _reloadAsked)
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
                _reloadAsked = _metadata.ReloadAll();
                Refresh();
            },
            () => _ask(null)));
    }
}
