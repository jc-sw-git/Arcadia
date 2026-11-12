using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Metadata;

namespace Arcadia.App.ViewModels;

/// <summary>Main's part of the games' metadata (#493): the run's errors, a reload that failed as a whole, what the views show.</summary>
public partial class MainViewModel
{
    /// <summary>The metadata the views show of a game; <see cref="MergedMetadata.None"/> without the service.</summary>
    public MergedMetadata MetadataOf(string gameKey) => _metadata?.Of(gameKey) ?? MergedMetadata.None;

    /// <summary>Raised on the UI thread when the metadata of these games changed.</summary>
    public event EventHandler<IReadOnlyCollection<string>>? MetadataChanged;

    /// <summary>
    /// Follows the run from the start: an unexpected error is logged and toasted, "Alle Metadaten neu laden" failing as a
    /// whole gets one warning (#43); a background run that reaches no store says nothing. Reads the cache at once.
    /// </summary>
    private void WatchMetadata(MetadataService metadata)
    {
        metadata.Changed += (_, keys) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Detail?.ShowMetadata(keys);
            MetadataChanged?.Invoke(this, keys);
        });
        metadata.Failed += (_, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => AppErrors.Report(e, AppTexts.ErrorContextMetadata));
        metadata.ReloadFailed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Toast.Show(HealthSeverity.Warning, AppTexts.DataSourcesMetadataReloadNoConnection));
        metadata.Open();
    }
}
