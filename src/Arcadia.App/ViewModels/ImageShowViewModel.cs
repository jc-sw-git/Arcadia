using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>What "Bild ansehen" may show now: the page's own game and the other games of its tab, each with a background.</summary>
/// <param name="Own">The shown game's background; null when it has none (the show then starts with another game's).</param>
public sealed record ImageShowScene(ShownImage? Own, IReadOnlyList<ShownImage> Others);

/// <summary>
/// "Bild ansehen" (#565): after <see cref="IdleTime"/> without input on the detail page the page fades out and its game's
/// background shows whole; every <see cref="IdleTime"/> a random other game's background of the tab blends in
/// (<see cref="ImageShowOrder"/>). Any input ends it and is swallowed by the window (<see cref="TakeInput"/>). Two layers
/// take turns, so the next image blends over the one before. Only while Arcadia is in front: in the background no timer
/// runs and nothing moves.
/// </summary>
public sealed partial class ImageShowViewModel : ViewModelBase, IDisposable
{
    private readonly Func<bool> _mayRun;
    private readonly Func<ImageShowScene> _scene;
    private readonly Func<ArtworkFile, Task<bool>> _canShow;
    private readonly Action<Action> _post;
    private readonly ImageShowOrder _order;
    private readonly ITimer _timer;
    private readonly ITimer _clear;
    private bool _inFront = true;
    private bool _isArmed;

    // Counts the shows, so a next image still being checked when the show ended or restarted is dropped.
    private int _run;

    /// <param name="mayRun">The show may run now: the detail page is on top and no dialog is open. Asked on every input, so it must be cheap.</param>
    /// <param name="scene">The games to show, asked only when the show starts or turns to the next image.</param>
    /// <param name="canShow">Whether an image can be decoded; one that can't is skipped.</param>
    public ImageShowViewModel(TimeProvider time, Action<Action> post, Func<bool> mayRun, Func<ImageShowScene> scene, Func<ArtworkFile, Task<bool>> canShow, Random random)
    {
        _post = post;
        _mayRun = mayRun;
        _scene = scene;
        _canShow = canShow;
        _order = new ImageShowOrder(random);
        _timer = time.CreateTimer(_ => _post(OnTimer), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _clear = time.CreateTimer(_ => _post(ClearImages), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Without input this long the show starts; each image stays this long.</summary>
    public static TimeSpan IdleTime { get; } = TimeSpan.FromSeconds(10);

    /// <summary>How long the page takes to fade out and back in (the view's transition).</summary>
    public static TimeSpan FadeTime { get; } = TimeSpan.FromSeconds(1);

    /// <summary>The show covers the window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresent))]
    public partial bool IsShowing { get; private set; }

    /// <summary>The show is shown or still fading out; otherwise the window draws nothing of it.</summary>
    public bool IsPresent => IsShowing || FirstImage is not null || SecondImage is not null;

    /// <summary>The show ended because Arcadia left the front: it goes at once, without fading.</summary>
    [ObservableProperty]
    public partial bool EndsAtOnce { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresent))]
    public partial ShownImage? FirstImage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresent))]
    public partial ShownImage? SecondImage { get; private set; }

    /// <summary>The first layer is in front; the other blends in when it turns.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown))]
    public partial bool ShowsFirst { get; private set; } = true;

    /// <summary>The image in front, whose title shows bottom left.</summary>
    public ShownImage? Shown => ShowsFirst ? FirstImage : SecondImage;

    /// <summary>
    /// The player did something (a button, a key, the mouse): a running show ends, fading back to the page, and true
    /// says the window must swallow the input; otherwise the wait starts anew.
    /// </summary>
    public bool TakeInput()
    {
        var wasShowing = IsShowing;
        if (wasShowing)
        {
            End(atOnce: false);
        }

        Arm();
        return wasShowing;
    }

    /// <summary>
    /// A click or the wheel: like <see cref="TakeInput"/>, but swallowed also while the page is still fading back in, so a
    /// click right after the mouse moved doesn't hit a button under the fading image.
    /// </summary>
    public bool TakePointerInput()
    {
        var wasPresent = IsPresent;
        return TakeInput() || wasPresent;
    }

    /// <summary>The page on top or a dialog changed: the wait starts anew, or stops where the show can't start.</summary>
    public void Restart()
    {
        if (IsShowing && !_mayRun())
        {
            End(atOnce: false);
        }

        Arm();
    }

    /// <summary>Arcadia came to the front or left it: in the background the show ends at once and no timer runs.</summary>
    public void SetInFront(bool inFront)
    {
        _inFront = inFront;
        if (!inFront && IsShowing)
        {
            End(atOnce: true);
        }

        Arm();
    }

    private void Arm()
    {
        _isArmed = _inFront && _mayRun();
        _timer.Change(_isArmed ? IdleTime : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void OnTimer()
    {
        if (!_isArmed || !_inFront || !_mayRun())
        {
            return;
        }

        var scene = _scene();
        if (IsShowing)
        {
            ShowNextAsync(scene, _run).Forget(AppTexts.ErrorContextImageShow);
        }
        else
        {
            Start(scene);
        }
    }

    private void Start(ImageShowScene scene)
    {
        _run++;
        _clear.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (scene.Own is { } own)
        {
            Begin(own);
        }
        else
        {
            // Without an image of its own the show begins with another game's; with none at all it doesn't start.
            BeginWithAnotherAsync(scene, _run).Forget(AppTexts.ErrorContextImageShow);
        }
    }

    private void Begin(ShownImage image)
    {
        (FirstImage, SecondImage, ShowsFirst) = (image, null, true);
        OnPropertyChanged(nameof(Shown));
        EndsAtOnce = false;
        IsShowing = true;
        AppLog.Write($"Bild ansehen: {image.Title}");
        _timer.Change(IdleTime, Timeout.InfiniteTimeSpan);
    }

    private async Task BeginWithAnotherAsync(ImageShowScene scene, int run)
    {
        if (await NextShowableAsync(scene, current: null) is { } image && run == _run && !IsShowing && _isArmed)
        {
            Begin(image);
        }
    }

    private async Task ShowNextAsync(ImageShowScene scene, int run)
    {
        var next = await NextShowableAsync(scene, Shown?.GameKey);
        if (run != _run || !IsShowing)
        {
            return;
        }

        if (next is not null)
        {
            // Into the layer behind, which then comes in front and blends in once it is decoded (the view's style), so the
            // image before stays until then. Emptied first, so a layer that still holds this image loads it anew and fades.
            if (ShowsFirst)
            {
                SecondImage = null;
                SecondImage = next;
            }
            else
            {
                FirstImage = null;
                FirstImage = next;
            }

            ShowsFirst = !ShowsFirst;
            AppLog.Write($"Bild ansehen: {next.Title}");
        }

        _timer.Change(IdleTime, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The next image of the order that can be decoded; one that can't is skipped (it counts as shown this round).</summary>
    private async Task<ShownImage?> NextShowableAsync(ImageShowScene scene, string? current)
    {
        var pool = scene.Others;
        for (var tries = 0; tries < pool.Count; tries++)
        {
            if (_order.Next(pool, current) is not { } next)
            {
                return null;
            }

            if (await _canShow(next.File))
            {
                return next;
            }
        }

        return null;
    }

    private void End(bool atOnce)
    {
        _run++;
        EndsAtOnce = atOnce;
        IsShowing = false;
        AppLog.Write(atOnce ? "Bild ansehen: beendet (Arcadia im Hintergrund)" : "Bild ansehen: beendet (Eingabe)");

        // The images go once the page is back, so their bitmaps don't stay in memory under it.
        if (atOnce)
        {
            ClearImages();
        }
        else
        {
            _clear.Change(FadeTime, Timeout.InfiniteTimeSpan);
        }
    }

    private void ClearImages()
    {
        if (!IsShowing)
        {
            (FirstImage, SecondImage, ShowsFirst) = (null, null, true);
            OnPropertyChanged(nameof(Shown));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _clear.Dispose();
    }
}
