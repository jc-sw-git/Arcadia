using System.Runtime.InteropServices;
using Arcadia.App.Services;
using Arcadia.Core.Search;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Arcadia.App.Views;

/// <summary>
/// The window of the mouse mode's on-screen keyboard (#110): over everything, on the monitor of the window in
/// front, at its top or bottom edge, and never active, so the field it types into keeps the focus, even
/// when a key is clicked. Windows doesn't show it over games in exclusive full screen.
/// </summary>
public partial class MouseModeKeyboardWindow : Window
{
    /// <summary>The keyboard's size and its distance to the screen's edge, in the 1920×1080 layout.</summary>
    private const double DesignWidth = 1646;

    private const double DesignHeight = 384;
    private const double DesignMargin = 24;

    /// <summary>The corners the window's region cuts; a little less than the drawn ones, so their soft rim stays.</summary>
    private const double RegionCornerRadius = 12;

    private Screen? _screen;

    // The window in front when the keyboard last went to the top.
    private IntPtr _foregroundWhenRaised;

    public MouseModeKeyboardWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsWindows())
        {
            // From the start, not after the first show: a window that was active once could take the focus on a click.
            Win32Properties.AddWindowStylesCallback(this, (style, extendedStyle) => (style, extendedStyle | NoActivate | ToolWindow));
            Win32Properties.AddWndProcHookCallback(this, KeepInactive);
        }
    }

    /// <summary>
    /// Shows the keyboard on the monitor of the window in front: at the edge chosen last, or at the other one
    /// when the caret Windows knows lies under it there. Returns the edge it took (true: the top).
    /// </summary>
    public bool Open(bool lastAtTop)
    {
        var foreground = ForegroundWindow.Bounds();
        _screen = (foreground is { } bounds ? Screens.ScreenFromPoint(new PixelPoint(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2))) : null)
            ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        var atTop = KeyboardPlacement.OpensAtTop(lastAtTop, Placed(top: true), Placed(top: false), ForegroundWindow.Caret());
        MoveTo(atTop);
        return atTop;
    }

    /// <summary>Moves the keyboard to the top or bottom edge of the monitor it opened on.</summary>
    public void MoveTo(bool atTop)
    {
        var rect = Placed(atTop);
        if (!IsVisible)
        {
            Show();
        }

        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
        {
            const uint NoActivateFlag = 0x10, ShowWindow = 0x40;
            SetWindowPos(handle.Handle, TopMost, rect.X, rect.Y, rect.Width, rect.Height, NoActivateFlag | ShowWindow);
            _foregroundWhenRaised = GetForegroundWindow();
            var radius = (int)Math.Round(RegionCornerRadius * rect.Height / DesignHeight);
            var region = CreateRoundRectRgn(0, 0, rect.Width + 1, rect.Height + 1, radius * 2, radius * 2);
            if (SetWindowRgn(handle.Handle, region, true) == 0)
            {
                DeleteObject(region);
            }
        }
        else
        {
            var scaling = _screen?.Scaling ?? 1;
            Position = new PixelPoint(rect.X, rect.Y);
            Width = rect.Width / scaling;
            Height = rect.Height / scaling;
        }
    }

    /// <summary>
    /// A window made topmost after the keyboard (a program's "always on top", a notification) would cover it, so each
    /// time another window comes to the front the keyboard goes to the top again. The Start menu, Windows' search and
    /// the other shell flyouts lie in a band above every program's window that only signed programs installed under
    /// Program Files reach (UIAccess); while one of them is open it still covers the keyboard.
    /// </summary>
    public void StayOnTop()
    {
        if (!OperatingSystem.IsWindows() || !IsVisible || TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        var foreground = GetForegroundWindow();
        if (foreground == _foregroundWhenRaised)
        {
            return;
        }

        _foregroundWhenRaised = foreground;
        const uint NoSize = 0x1, NoMove = 0x2, NoActivateFlag = 0x10;
        SetWindowPos(handle.Handle, TopMost, 0, 0, 0, 0, NoSize | NoMove | NoActivateFlag);
    }

    /// <summary>The keyboard at one edge of the monitor's work area (the taskbar stays visible), sized like the search.</summary>
    private ScreenRect Placed(bool top)
    {
        if (_screen is null)
        {
            return new ScreenRect(0, 0, (int)DesignWidth, (int)DesignHeight);
        }

        var monitor = _screen.Bounds;
        var scale = KeyboardPlacement.ScaleFor(new ScreenRect(monitor.X, monitor.Y, monitor.Width, monitor.Height));
        var work = _screen.WorkingArea;
        return KeyboardPlacement.At(
            new ScreenRect(work.X, work.Y, work.Width, work.Height),
            (int)Math.Round(DesignWidth * scale),
            (int)Math.Round(DesignHeight * scale),
            (int)Math.Round(DesignMargin * scale),
            top);
    }

    private static readonly IntPtr TopMost = new(-1);

    private const uint NoActivate = 0x08000000;
    private const uint ToolWindow = 0x00000080;

    /// <summary>A click must not activate the keyboard: Windows asks first, and "no" keeps the focus where it is.</summary>
    private static IntPtr KeepInactive(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const uint MouseActivate = 0x0021;
        const int NoActivateAnswer = 3;
        if (message == MouseActivate)
        {
            handled = true;
            return NoActivateAnswer;
        }

        return IntPtr.Zero;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int cornerWidth, int cornerHeight);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr gdiObject);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
