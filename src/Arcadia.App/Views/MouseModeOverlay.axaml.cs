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
/// seconds. Shown only for that, so it costs a running game nothing the rest of the time.
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

    /// <summary>"Mausmodus an/aus" shows this long; then nothing stays.</summary>
    private static readonly TimeSpan LabelTime = TimeSpan.FromSeconds(2);

    /// <summary>The area at the top centre the label may take (device-independent pixels); the region lets it through.</summary>
    private const double LabelAreaWidth = 1200;

    private const double LabelAreaHeight = 220;

    // Counts what was shown, so a hide timed for something earlier leaves a newer show alone.
    private int _generation;
    private bool _holding;

    public MouseModeOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Back + Start are held: the edge fills over <paramref name="fillTime"/>, the rest of the hold time.</summary>
    public void ShowHold(TimeSpan fillTime)
    {
        _generation++;
        _holding = true;
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
        Edge.Transitions = null;
        Edge.Classes.Remove("flash");
        if (flash)
        {
            Edge.Progress = 1;
            Edge.IsVisible = true;
            Edge.Classes.Add("flash");
        }
        else
        {
            Edge.IsVisible = false;
        }

        LabelText.Text = text;
        Label.IsVisible = true;
        Appear();
        DispatcherTimer.RunOnce(() => DisappearIfStill(generation), LabelTime);
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

    private void DisappearIfStill(int generation)
    {
        if (generation != _generation)
        {
            return;
        }

        Hide();
        Edge.Transitions = null;
        Edge.Progress = 0;
        Edge.Classes.Remove("flash");
        Label.IsVisible = false;
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
