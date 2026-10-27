using System.Runtime.InteropServices;
using Arcadia.App.Controls;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>
/// The mouse mode's feedback (#64) over everything on the primary monitor: the edge fills while
/// Back + Start are held, flashes when the mode switches, and "Mausmodus an/aus" shows for two
/// seconds; while View is held, "Zurück zu Arcadia" shows with a frame that fills. Shown only for
/// that, so it costs a running game nothing the rest of the time.
/// </summary>
/// <remarks>
/// It never takes the focus (no activation, no taskbar entry) and lets clicks through everywhere
/// but its edge and label: the window's region covers only those, so the rest of the screen stays
/// usable while it shows. Windows doesn't show it over games in exclusive full screen.
/// </remarks>
public partial class MouseModeOverlay : Window
{
    /// <summary>Let go early, the edge runs back this fast.</summary>
    private static readonly TimeSpan RunBackTime = TimeSpan.FromMilliseconds(250);

    /// <summary>The flash (<c>ScreenEdgeFill.flash</c> in the theme) has faded out by then; the edge goes before the animation ends.</summary>
    private static readonly TimeSpan FlashTime = TimeSpan.FromMilliseconds(450);

    /// <summary>"Mausmodus an/aus" shows this long; then nothing stays.</summary>
    private static readonly TimeSpan LabelTime = TimeSpan.FromSeconds(2);

    /// <summary>A notice (e.g. Pan's, #243) is a sentence or two, read while a game runs: it stays longer.</summary>
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(6);

    /// <summary>The area at the top centre the label may take (device-independent pixels); the region lets it through.</summary>
    private const double LabelAreaWidth = 1200;

    private const double LabelAreaHeight = 260;

    // Counts what was shown, so a hide timed for something earlier leaves a newer show alone.
    private int _generation;
    private bool _holding;
    private bool _returnHolding;

    // Counts the label's texts, so the timer of a notice shown beside a hold hides only that notice.
    private int _noticeGeneration;

    public MouseModeOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Back + Start are held: the edge fills over <paramref name="fillTime"/>, the rest of the hold time.</summary>
    public void ShowHold(TimeSpan fillTime)
    {
        _generation++;
        _holding = true;
        HideReturnBox();
        Edge.Classes.Remove("flash");
        Label.IsVisible = false;
        Edge.Transitions = null;
        Edge.Progress = 0;
        Edge.IsVisible = true;

        // Shown first: a transition runs only on a control that is on screen, otherwise it jumps to its end.
        Appear();
        Edge.Transitions = [new DoubleTransition { Property = ScreenEdgeFill.ProgressProperty, Duration = fillTime }];
        Edge.Progress = 1;
    }

    /// <summary>Back or Start was let go before the hold time: the edge runs back, then the overlay goes.</summary>
    public void CancelHold()
    {
        if (!_holding)
        {
            return;
        }

        _holding = false;
        var generation = ++_generation;
        Edge.Transitions = [new DoubleTransition { Property = ScreenEdgeFill.ProgressProperty, Duration = RunBackTime, Easing = new CubicEaseOut() }];
        Edge.Progress = 0;
        DispatcherTimer.RunOnce(() => DisappearIfStill(generation), RunBackTime);
    }

    /// <summary>The mode switched: <paramref name="text"/> at the top centre, after a flash of the full edge if it was held.</summary>
    public void ShowSwitched(string text, bool flash)
    {
        _holding = false;
        var generation = ++_generation;
        HideReturnBox();
        Edge.Transitions = null;
        Edge.Classes.Remove("flash");
        if (flash)
        {
            Edge.Progress = 1;
            Edge.IsVisible = true;
            Edge.Classes.Add("flash");
            DispatcherTimer.RunOnce(() => EndFlashIfStill(generation), FlashTime);
        }
        else
        {
            Edge.IsVisible = false;
        }

        ShowLabel(text, notice: false);
        DispatcherTimer.RunOnce(() => DisappearIfStill(generation), LabelTime);
    }

    /// <summary>
    /// A notice while Arcadia is in the background (#243 part B2: Pan's, where a toast would go unseen), at the top
    /// centre for a few seconds. A hold the player is watching goes on (its fill shows how long is left): the notice
    /// shows beside it and goes by itself (QE review of PR #264).
    /// </summary>
    public void ShowNotice(string text)
    {
        if (_holding || _returnHolding)
        {
            // Below "Zurück zu Arcadia", which stands where the label would.
            ShowLabel(text, notice: true, belowReturnBox: _returnHolding);
            var notice = _noticeGeneration;
            DispatcherTimer.RunOnce(() => HideNoticeIfStill(notice), NoticeTime);
            return;
        }

        _holding = false;
        var generation = ++_generation;
        HideReturnBox();
        Edge.Transitions = null;
        Edge.Classes.Remove("flash");
        Edge.IsVisible = false;
        ShowLabel(text, notice: true);
        DispatcherTimer.RunOnce(() => DisappearIfStill(generation), NoticeTime);
    }

    private void HideNoticeIfStill(int notice)
    {
        // Only this notice: a newer one, or the label of a switch since, stays.
        if (notice == _noticeGeneration && Label.Classes.Contains("notice"))
        {
            Label.IsVisible = false;
        }
    }

    private void ShowLabel(string text, bool notice, bool belowReturnBox = false)
    {
        _noticeGeneration++;
        LabelText.Text = text;
        Label.Classes.Set("notice", notice);
        Label.Classes.Set("belowReturnBox", belowReturnBox);
        Label.IsVisible = true;
        Appear();
    }

    /// <summary>View is held in the background: "Zurück zu Arcadia", its frame filling over <paramref name="fillTime"/>.</summary>
    public void ShowReturnHold(TimeSpan fillTime)
    {
        _generation++;
        _holding = false;
        _returnHolding = true;
        Edge.IsVisible = false;
        Label.IsVisible = false;
        ReturnEdge.Transitions = null;
        ReturnEdge.Progress = 0;
        ReturnBox.IsVisible = true;
        Appear();
        ReturnEdge.Transitions = [new DoubleTransition { Property = ScreenEdgeFill.ProgressProperty, Duration = fillTime }];
        ReturnEdge.Progress = 1;
    }

    /// <summary>View was let go early: the frame runs back, then the box goes.</summary>
    public void CancelReturnHold()
    {
        if (!_returnHolding)
        {
            return;
        }

        _returnHolding = false;
        var generation = ++_generation;
        ReturnEdge.Transitions = [new DoubleTransition { Property = ScreenEdgeFill.ProgressProperty, Duration = RunBackTime, Easing = new CubicEaseOut() }];
        ReturnEdge.Progress = 0;
        DispatcherTimer.RunOnce(() => DisappearIfStill(generation), RunBackTime);
    }

    /// <summary>Arcadia is coming to the front: nothing to announce any more.</summary>
    public void EndReturnHold()
    {
        if (!_returnHolding)
        {
            return;
        }

        _returnHolding = false;
        DisappearIfStill(++_generation);
    }

    private void HideReturnBox()
    {
        _returnHolding = false;
        ReturnBox.IsVisible = false;
        ReturnEdge.Transitions = null;
        ReturnEdge.Progress = 0;
    }

    private void Appear()
    {
        if (IsVisible)
        {
            return;
        }

        // Some systems flag no screen as primary; then the first one shows it.
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is not null)
        {
            Position = screen.Bounds.Position;
            Width = screen.Bounds.Width / screen.Scaling;
            Height = screen.Bounds.Height / screen.Scaling;
        }

        Show();
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle && screen is not null)
        {
            KeepOutOfTheWay(handle.Handle, screen.Bounds.Width, screen.Bounds.Height, screen.Scaling);
        }
    }

    private void EndFlashIfStill(int generation)
    {
        if (generation == _generation)
        {
            Edge.IsVisible = false;
            Edge.Classes.Remove("flash");
        }
    }

    private void DisappearIfStill(int generation)
    {
        if (generation != _generation)
        {
            return;
        }

        Edge.Transitions = null;
        Edge.Progress = 0;
        Edge.Classes.Remove("flash");
        Label.IsVisible = false;
        HideReturnBox();

        // Windows shows a hidden window's last frame again on the next Show, so the overlay hides only
        // after an empty frame was drawn; otherwise the last "Mausmodus an/aus" flickers up at the next hold.
        RequestAnimationFrame(_ => RequestAnimationFrame(_ =>
        {
            if (generation == _generation)
            {
                Hide();
            }
        }));
    }

    /// <summary>No activation, no taskbar or Alt+Tab entry, and a region of only the edge and the label area, so clicks pass everywhere else.</summary>
    private void KeepOutOfTheWay(IntPtr window, int width, int height, double scaling)
    {
        const int ExtendedStyle = -20;
        const long NoActivate = 0x08000000, ToolWindow = 0x00000080;
        SetWindowLongPtr(window, ExtendedStyle, (IntPtr)((long)GetWindowLongPtr(window, ExtendedStyle) | NoActivate | ToolWindow));

        // The region's edge is a little wider than the line, so its antialiased rim isn't cut.
        var edge = (int)Math.Ceiling((Edge.StrokeThickness + 2) * scaling);
        var labelWidth = Math.Min(width, (int)(LabelAreaWidth * scaling));
        var labelLeft = (width - labelWidth) / 2;
        var region = CreateRectRgn(0, 0, width, height);
        var inside = CreateRectRgn(edge, edge, width - edge, height - edge);
        var label = CreateRectRgn(labelLeft, 0, labelLeft + labelWidth, (int)(LabelAreaHeight * scaling));
        const int Difference = 4, Union = 2;
        CombineRgn(region, region, inside, Difference);
        CombineRgn(region, region, label, Union);
        DeleteObject(inside);
        DeleteObject(label);

        // On success Windows owns the region; only a failed call leaves it to us.
        if (SetWindowRgn(window, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial IntPtr GetWindowLongPtr(IntPtr window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    private static partial int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr gdiObject);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
