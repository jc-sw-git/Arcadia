namespace Arcadia.Core.Search;

/// <summary>A rectangle on the desktop in physical pixels, as Windows reports monitors, windows and the caret.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Intersects(ScreenRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// Where the mouse mode's on-screen keyboard (#110) goes: centred at the top or bottom edge of the monitor
/// the window in front is on, only as wide as its keys, so more of the window behind stays visible.
/// </summary>
public static class KeyboardPlacement
{
    /// <summary>
    /// The keyboard grows with the monitor in the same proportion as Arcadia's Big Picture UI, which is laid
    /// out at 1920×1080 (owner, 2026-10-22): on a 4K monitor twice as large in pixels.
    /// </summary>
    public static double ScaleFor(ScreenRect monitor) => Math.Min(monitor.Width / 1920.0, monitor.Height / 1080.0);

    /// <summary>The keyboard of <paramref name="width"/> × <paramref name="height"/> pixels at one edge of the work area, <paramref name="margin"/> away from it.</summary>
    public static ScreenRect At(ScreenRect workArea, int width, int height, int margin, bool top)
    {
        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = top ? workArea.Y + margin : workArea.Bottom - margin - height;
        return new ScreenRect(x, y, width, height);
    }

    /// <summary>
    /// Opens at the edge chosen last, unless the caret Windows knows would lie under the keyboard there and
    /// not at the other edge: the field being typed into must stay visible.
    /// </summary>
    public static bool OpensAtTop(bool lastAtTop, ScreenRect atTop, ScreenRect atBottom, ScreenRect? caret)
    {
        if (caret is not { } c)
        {
            return lastAtTop;
        }

        var (chosen, other) = lastAtTop ? (atTop, atBottom) : (atBottom, atTop);
        return chosen.Intersects(c) && !other.Intersects(c) ? !lastAtTop : lastAtTop;
    }
}
