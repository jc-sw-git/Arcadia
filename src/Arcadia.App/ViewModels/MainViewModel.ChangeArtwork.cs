using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The window's part of "Artwork ändern" (#490): opening the modal over the page, and what the modal asks of it.</summary>
public partial class MainViewModel
{
    /// <summary>"Artwork ändern" (#490), over the library, the search or the detail page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChangeArtworkOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial ChangeArtworkViewModel? ChangeArtwork { get; private set; }

    public bool IsChangeArtworkOpen => ChangeArtwork is not null;

    partial void OnChangeArtworkChanged(ChangeArtworkViewModel? oldValue, ChangeArtworkViewModel? newValue) => oldValue?.Dispose();

    void IGameActionsOwner.ChangeArtwork(GameTileViewModel tile)
    {
        if (_artwork is not null)
        {
            ChangeArtwork = new ChangeArtworkViewModel(tile, _artwork, this);
        }
    }

    void IChangeArtworkHost.Close(ChangeArtworkViewModel modal)
    {
        if (ChangeArtwork == modal)
        {
            ChangeArtwork = null;
        }
    }

    void IChangeArtworkHost.OpenDataSources(ChangeArtworkViewModel modal)
    {
        ((IChangeArtworkHost)this).Close(modal);
        OpenDataSources(modal.Notice.RowKey);
    }

    Task<string?> IChangeArtworkHost.PickImageAsync(string title) =>
        Dialogs?.PickFileAsync(title, AppTexts.ArtworkImagesType, ImageExtensions) ?? Task.FromResult<string?>(null);

    void IChangeArtworkHost.Warn(string message) => Toast.Show(HealthSeverity.Warning, message);

    private static readonly string[] ImageExtensions = ["png", "jpg", "jpeg", "webp"];
}
