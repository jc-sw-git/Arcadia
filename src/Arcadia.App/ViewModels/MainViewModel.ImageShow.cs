using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.App.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The window's part of "Bild ansehen" (#565): when it may run, what it shows, and the input that ends it. A press that
/// ends it is swallowed with its repeats and its release, so an A never starts the game behind it.
/// </summary>
public partial class MainViewModel
{
    // Small: the check only asks whether the image can be decoded at all.
    private const int ImageCheckWidth = 64;

    private readonly HashSet<UiAction> _swallowedByImageShow = [];

    /// <summary>"Bild ansehen" over the detail page.</summary>
    public ImageShowViewModel ImageShow { get; private set; } = null!;

    /// <summary>
    /// Any input reached the window (a key, the mouse, a controller button not mapped to an action): true if it ended
    /// "Bild ansehen" and must do nothing else; otherwise the wait for the show starts anew.
    /// </summary>
    public bool TakeInputForImageShow() => ImageShow.TakeInput();

    /// <summary>A click or the wheel: true also while the page is still fading back in, so it hits nothing under the image.</summary>
    public bool TakePointerInputForImageShow() => ImageShow.TakePointerInput();

    /// <summary>Arcadia's window came to the front or left it (#565: no timer and no animation in the background).</summary>
    public void SetInFront(bool inFront) => ImageShow.SetInFront(inFront);

    private void CreateImageShow()
    {
        ImageShow = new ImageShowViewModel(_time, action => Avalonia.Threading.Dispatcher.UIThread.Post(action), ImageShowMayRun, ImageShowScene, CanShowImageAsync, Random.Shared);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsDialogOpen) or nameof(IsDetailOpen))
            {
                ImageShow.Restart();
            }
        };
    }

    /// <summary>A press ends a running show and does nothing else; nor do its repeats.</summary>
    private bool SwallowedByImageShow(UiAction action, bool repeat)
    {
        if (repeat && _swallowedByImageShow.Contains(action))
        {
            return true;
        }

        if (ImageShow.TakeInput())
        {
            _swallowedByImageShow.Add(action);
            return true;
        }

        return false;
    }

    /// <summary>The release of a press that ended the show does nothing either (A acts on its release).</summary>
    private bool ReleaseSwallowedByImageShow(UiAction action) => _swallowedByImageShow.Remove(action);

    /// <summary>Only on the detail page with nothing over it.</summary>
    private bool ImageShowMayRun() => OpenPages().First() is GameDetailViewModel && !IsDialogOpen;

    /// <summary>The page's game and the others of its tab (with its filter) or of the search hits, each with a background.</summary>
    private ImageShowScene ImageShowScene()
    {
        if (Detail is not { } detail)
        {
            return new ImageShowScene(null, []);
        }

        var own = detail.Current.Tile.Game.Key;
        var images = detail.Games
            .Select(game => _artwork?.Of(game.Key)?.Hero is { } hero ? new ShownImage(game.Key, game.Title, hero) : null)
            .OfType<ShownImage>()
            .ToList();
        return new ImageShowScene(images.FirstOrDefault(image => image.GameKey == own), images.Where(image => image.GameKey != own).ToList());
    }

    private async Task<bool> CanShowImageAsync(ArtworkFile file)
    {
        using var bitmap = await Task.Run(() => ArtworkLoader.DecodeAsync(file, ImageCheckWidth));
        return bitmap is not null;
    }
}
